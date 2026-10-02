#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA
{
	public sealed class Renderer : IDisposable
	{
		enum RenderType { None, World, UI }

		public SpriteRenderer WorldSpriteRenderer { get; }
		public RgbaSpriteRenderer WorldRgbaSpriteRenderer { get; }
		public RgbaColorRenderer WorldRgbaColorRenderer { get; }
		public IRenderer[] WorldRenderers = [];
		public RgbaColorRenderer RgbaColorRenderer { get; }
		public SpriteRenderer SpriteRenderer { get; }
		public RgbaSpriteRenderer RgbaSpriteRenderer { get; }

		public bool WindowHasInputFocus => Window.HasInputFocus;
		public bool WindowIsSuspended => Window.IsSuspended;

		public IReadOnlyDictionary<string, SpriteFont> Fonts;

		internal IPlatformWindow Window { get; }
		internal IGraphicsContext Context { get; }

		internal int TempVertexBufferSize { get; }
		internal int TempIndexBufferSize { get; }

		readonly IVertexBuffer<Vertex> tempVertexBuffer;
		readonly IIndexBuffer quadIndexBuffer;
		readonly Stack<Rectangle> scissorState = [];
		readonly ITexture bufferSnapshot;

		IFrameBuffer screenBuffer;
		Sprite screenSprite;

		IFrameBuffer worldBuffer;
		Sheet worldSheet;
		Sprite worldSprite;
		Size lastMaximumViewportSize;
		Vector2 worldSpriteScale = Vector2.One;

		public Size WorldFrameBufferSize => worldSheet.Size;
		public int WorldDownscaleFactor { get; private set; } = 1;

		/// <summary>
		/// Copies and returns the currently rendered state as a temporary texture.
		/// </summary>
		public ITexture GetRenderBufferSnapshot()
		{
			var size = renderType == RenderType.World ? worldSheet.Size : Window.SurfaceSize.NextPowerOf2();
			bufferSnapshot.SetDataFromReadBuffer(new Rectangle(int2.Zero, size));
			return bufferSnapshot;
		}

		SheetBuilder fontSheetBuilder;
		float fontScale;
		readonly IPlatform platform;

		float depthMargin;

		Size lastBufferSize = new(-1, -1);
		Size lastResolution;
		Size lastNativeResolution;
		Size lastSurfaceSize;
		float lastWindowScale;

		Rectangle lastWorldViewport;
		ITexture currentPaletteTexture;
		int currentPaletteHeight = 0;
		IBatchRenderer currentBatchRenderer;
		RenderType renderType = RenderType.None;

		public Renderer(IPlatform platform, GraphicSettings graphicSettings, int vertexBatchSize)
		{
			this.platform = platform;
			var resolution = GetResolution(graphicSettings);

			TempVertexBufferSize = vertexBatchSize - vertexBatchSize % 4;
			TempIndexBufferSize = TempVertexBufferSize / 4 * 6;

			Window = platform.CreateWindow(new Size(resolution.Width, resolution.Height),
				graphicSettings.Mode, graphicSettings.UIScale, TempVertexBufferSize, TempIndexBufferSize,
				graphicSettings.VideoDisplay, graphicSettings.GLProfile);

			Context = Window.Context;
			lastResolution = Resolution;
			lastNativeResolution = NativeResolution;
			lastSurfaceSize = Window.SurfaceSize;
			lastWindowScale = WindowScale;

			tempVertexBuffer = Context.CreateEmptyVertexBuffer<Vertex>(TempVertexBufferSize);
			quadIndexBuffer = Context.CreateIndexBuffer(Util.CreateQuadIndices(TempIndexBufferSize / 6));

			var combinedBindings = new CombinedShaderBindings();
			WorldSpriteRenderer = new SpriteRenderer(this, tempVertexBuffer, quadIndexBuffer, Context.CreateShader(combinedBindings));
			WorldRgbaSpriteRenderer = new RgbaSpriteRenderer(WorldSpriteRenderer);
			WorldRgbaColorRenderer = new RgbaColorRenderer(WorldSpriteRenderer);
			SpriteRenderer = new SpriteRenderer(this, tempVertexBuffer, quadIndexBuffer, Context.CreateShader(combinedBindings));
			RgbaSpriteRenderer = new RgbaSpriteRenderer(SpriteRenderer);
			RgbaColorRenderer = new RgbaColorRenderer(SpriteRenderer);

			bufferSnapshot = Context.CreateTexture();
		}

		static Size GetResolution(GraphicSettings graphicsSettings)
		{
			var size = (graphicsSettings.Mode == WindowMode.Windowed)
				? graphicsSettings.WindowedSize
				: graphicsSettings.FullscreenSize;
			return new Size(size.X, size.Y);
		}

		/// <summary>
		/// Changes the UI scale. Fonts, chrome and the world buffer are updated immediately so that the UI can be laid out
		/// again right away (see Ui.Relayout).
		/// </summary>
		public void SetUIScale(float scale)
		{
			Window.SetScaleModifier(scale);
			ApplyWindowScale(Window.EffectiveWindowScale);
			lastResolution = Resolution;
			lastWindowScale = WindowScale;
		}

		void ApplyWindowScale(float effectiveScale)
		{
			// Recalculate downscaling factor for the new window scale (once a world has set up its buffer)
			if (lastMaximumViewportSize.Width > 0 && lastMaximumViewportSize.Height > 0)
				SetMaximumViewportSize(lastMaximumViewportSize);

			ChromeProvider.SetDPIScale(effectiveScale);

			if (Fonts != null && fontScale != effectiveScale)
			{
				// Live window resizing can change the fitted UI scale every frame. Release the old glyphs'
				// atlas too, otherwise every scale change permanently appends more textures.
				Flush();
				fontSheetBuilder.Reset();
				foreach (var f in Fonts)
					f.Value.SetScale(effectiveScale);

				fontScale = effectiveScale;
			}
		}

		public void InitializeFonts(ModData modData)
		{
			if (Fonts != null)
				foreach (var font in Fonts.Values)
					font.Dispose();
			using (new PerfTimer("SpriteFonts"))
			{
				fontSheetBuilder?.Dispose();
				fontSheetBuilder = new SheetBuilder(SheetType.BGRA, modData.Manifest.RendererConstants.FontSheetSize);
				fontScale = Window.EffectiveWindowScale;
				byte[] ReadFile(string file) => modData.DefaultFileSystem.Open(file).ReadAllBytes();
				Fonts = modData.GetOrCreate<Fonts>().FontList.ToDictionary(x => x.Key,
					x => new SpriteFont(
						platform, x.Value.Font, ReadFile(x.Value.Font),
						x.Value.Size, x.Value.Ascender, Window.EffectiveWindowScale, fontSheetBuilder,
						x.Value.PixelFaces.Select(f => PixelFace.Parse(platform, f, ReadFile)).ToArray(), new PixelFontMetrics
						{
							Bold = x.Value.PixelBold,
							CapRatio = x.Value.PixelCapRatio,
							AdvanceRatio = x.Value.PixelAdvanceRatio,
							WidthLimit = x.Value.PixelWidthLimit,
							SizeTolerance = x.Value.PixelSizeTolerance,
							MinCapHeight = x.Value.PixelMinCapHeight
						}));

				foreach (var f in modData.GetOrCreate<Fonts>().FontList)
					if (f.Value.PixelLargerThan != null)
						Fonts[f.Key].SetLargerThan(Fonts.TryGetValue(f.Value.PixelLargerThan, out var other) ? other
							: throw new InvalidDataException($"Font {f.Key}: PixelLargerThan font `{f.Value.PixelLargerThan}` does not exist."));
			}

			var oldResolution = Resolution;
			SetUIScale(modData.GetOrCreate<WorldViewportSizes>().ClampUIScale(Game.Settings.Graphics.UIScale, NativeResolution));
			if (oldResolution != Resolution)
				Ui.Relayout(oldResolution);
		}

		void UpdateWindowGeometry()
		{
			if (lastNativeResolution == NativeResolution && lastSurfaceSize == Window.SurfaceSize && lastWindowScale == WindowScale)
				return;

			var oldResolution = lastResolution;
			var oldWindowScale = lastWindowScale;
			var scale = Game.ModData != null
				? Game.ModData.GetOrCreate<WorldViewportSizes>().ClampUIScale(Game.Settings.Graphics.UIScale, NativeResolution)
				: UIScale;

			if (scale != UIScale || lastWindowScale != WindowScale)
				SetUIScale(scale);
			else if (lastMaximumViewportSize.Width > 0 && lastMaximumViewportSize.Height > 0)
				SetMaximumViewportSize(lastMaximumViewportSize);

			lastNativeResolution = NativeResolution;
			lastSurfaceSize = Window.SurfaceSize;
			lastWindowScale = WindowScale;
			lastResolution = Resolution;

			// Font raster metrics can change even when fitted logical dimensions stay identical.
			if (oldResolution != Resolution || oldWindowScale != WindowScale)
				Ui.Relayout(oldResolution);
		}

		public void SetDepthMargin(float depthMargin)
		{
			this.depthMargin = depthMargin;
		}

		void BeginFrame()
		{
			Context.Clear();
			ChromeProvider.CollectRetiredSheets();

			var surfaceSize = Window.SurfaceSize;
			var surfaceBufferSize = surfaceSize.NextPowerOf2();

			if (screenSprite == null || screenSprite.Sheet.Size != surfaceBufferSize)
			{
				screenBuffer?.Dispose();

				// Render the screen into a frame buffer to simplify reading back screenshots
				screenBuffer = Context.CreateFrameBuffer(surfaceBufferSize, Color.FromArgb(0xFF, 0, 0, 0));
				screenSprite = null;
			}

			if (screenSprite == null || surfaceSize.Width != screenSprite.Bounds.Width || -surfaceSize.Height != screenSprite.Bounds.Height)
			{
				var screenSheet = new Sheet(SheetType.BGRA, screenBuffer.Texture);

				// Flip sprite in Y to match OpenGL's bottom-left origin
				var screenBounds = Rectangle.FromLTRB(0, surfaceSize.Height, surfaceSize.Width, 0);
				screenSprite = new Sprite(screenSheet, screenBounds, TextureChannel.RGBA);
			}

			// In HiDPI windows we follow Apple's convention of defining window coordinates as for standard resolution windows
			// but to have a higher resolution backing surface with more than 1 texture pixel per viewport pixel.
			// We must convert the surface buffer size to a viewport size - in general this is NOT just the window size
			// rounded to the next power of two, as the NextPowerOf2 calculation is done in the surface pixel coordinates
			var scale = Window.EffectiveWindowScale;
			var bufferSize = new Size((int)(surfaceBufferSize.Width / scale), (int)(surfaceBufferSize.Height / scale));
			if (lastBufferSize != bufferSize)
			{
				SpriteRenderer.SetViewportParams(bufferSize, 1, 0f, int2.Zero);
				lastBufferSize = bufferSize;
			}
		}

		public void SetMaximumViewportSize(Size size)
		{
			// Aim to render the world into a framebuffer at 1:1 scaling which is then up/downscaled using a custom
			// filter to provide crisp scaling and avoid rendering glitches when the depth buffer is used and samples don't match.
			// This approach does not scale well to large sizes, first saturating GPU fill rate and then crashing when
			// reaching the framebuffer size limits (typically 16k). We therefore clamp the maximum framebuffer size to
			// twice the window surface size, which strikes a reasonable balance between rendering quality and performance.
			// Mods that use the depth buffer must instead limit their artwork resolution or maximum zoom-out levels.
			Size worldBufferSize;
			if (depthMargin == 0)
			{
				var surfaceSize = Window.SurfaceSize;
				worldBufferSize = new Size(Math.Min(size.Width, 2 * surfaceSize.Width), Math.Min(size.Height, 2 * surfaceSize.Height)).NextPowerOf2();
			}
			else
				worldBufferSize = size.NextPowerOf2();

			if (worldSprite == null || worldSheet.Size != worldBufferSize)
			{
				worldBuffer?.Dispose();

				// If enableWorldFrameBufferDownscale and the world is more than twice the size of the final output size do we allow it to be downsampled!
				worldBuffer = Context.CreateFrameBuffer(worldBufferSize);

				// Pixel art scaling mode is a customized bilinear sampling
				worldBuffer.Texture.ScaleFilter = TextureScaleFilter.Linear;
				worldSheet = new Sheet(SheetType.BGRA, worldBuffer.Texture);

				// Invalidate cached state to force a shader update
				lastWorldViewport = Rectangle.Empty;
				worldSprite = null;
			}

			lastMaximumViewportSize = size;
		}

		/// <summary>
		/// Starts rendering the world into the world frame buffer.
		/// <paramref name="zoom"/> is the number of native window pixels per world pixel (the viewport zoom); it is
		/// used to scale the world buffer onto the screen exactly, so whole zooms stay pixel-perfect at any UI scale.
		/// A value of 0 derives it from the viewport size instead.
		/// </summary>
		public void BeginWorld(Vector2 viewportLocation, Size viewportSize, float zoom = 0f)
		{
			if (renderType != RenderType.None)
				throw new InvalidOperationException($"BeginWorld called with renderType = {renderType}, expected RenderType.None.");

			BeginFrame();

			if (worldSheet == null)
				throw new InvalidOperationException("BeginWorld called before SetMaximumViewportSize has been set.");

			if (zoom <= 0f)
				zoom = (float)Window.NativeWindowSize.Width / Math.Max(1, viewportSize.Width);

			// The world sprite is cheap to rebuild, and depends on the scroll position, zoom, window size and UI scale.
			var centerLocation = int2.FromVector(viewportLocation);

			// Downscale world rendering if needed to fit within the framebuffer
			var vw = viewportSize.Width;
			var vh = viewportSize.Height;
			var bw = worldSheet.Size.Width;
			var bh = worldSheet.Size.Height;
			WorldDownscaleFactor = 1;
			while (vw / WorldDownscaleFactor > bw || vh / WorldDownscaleFactor > bh)
				WorldDownscaleFactor++;

			// Add 2 pixels: one for the interpixel 0-0.99 fractional offset and one because the
			// viewport size is truncated, so that the buffer always covers the far window edges.
			var s = new Size(Math.Min(vw / WorldDownscaleFactor + 2, bw), Math.Min(vh / WorldDownscaleFactor + 2, bh));

			// Window surface pixels per world buffer pixel.
			var surfaceScale = zoom * WorldDownscaleFactor * screenSprite.Bounds.Width / Math.Max(1, Window.NativeWindowSize.Width);

			// Snap the sub-pixel scroll offset to whole surface pixels so that pixel edges stay in a fixed
			// phase with the screen at every zoom: this avoids shimmering and swimming seams while panning.
			var fractionalOffset = (centerLocation.ToVector2() - viewportLocation) / WorldDownscaleFactor;
			fractionalOffset = Vector2.Round(fractionalOffset * surfaceScale) / surfaceScale;

			worldSprite = new Sprite(worldSheet, new Rectangle(int2.Zero, s), 0, fractionalOffset.AsVector3(), TextureChannel.RGBA);

			// The UI pass maps its (whole number sized) viewport onto the surface buffer, so at fractional UI scales one
			// UI unit is not exactly EffectiveWindowScale surface pixels: use the real ratio to keep the world pixel-exact.
			var surfaceBufferSize = Window.SurfaceSize.NextPowerOf2();
			worldSpriteScale = new Vector2(
				surfaceScale * lastBufferSize.Width / surfaceBufferSize.Width,
				surfaceScale * lastBufferSize.Height / surfaceBufferSize.Height);

			worldBuffer.Bind();
			var rect = new Rectangle(centerLocation, viewportSize);
			if (lastWorldViewport != rect)
			{
				var topLeft = centerLocation - viewportSize.ToInt2() / 2;
				WorldSpriteRenderer.SetViewportParams(worldSheet.Size, WorldDownscaleFactor, depthMargin, topLeft);
				lastWorldViewport = rect;
			}

			renderType = RenderType.World;
		}

		public void BeginUI()
		{
			if (renderType == RenderType.World)
			{
				// Complete world rendering
				Flush();
				worldBuffer.Unbind();

				// Render the world buffer into the UI buffer at exactly the viewport zoom (UI units per world buffer pixel).
				// The pixel art filter keeps whole zooms nearest-neighbour sharp, gives fractional zooms a one pixel
				// anti-aliased edge (sharp bilinear) and area-averages when zoomed out.
				screenBuffer.Bind();
				var bufferScale = new Vector3(worldSpriteScale, 1f);

				SpriteRenderer.EnablePixelArtScaling(true);
				RgbaSpriteRenderer.DrawSprite(worldSprite, Vector3.Zero, bufferScale);
				Flush();
				SpriteRenderer.EnablePixelArtScaling(false);
			}
			else
			{
				// World rendering was skipped
				BeginFrame();
				screenBuffer.Bind();
			}

			// Snap UI geometry to whole device pixels so that fractional UI scales stay crisp
			SpriteRenderer.PixelSnapScale = Window.EffectiveWindowScale;

			renderType = RenderType.UI;
		}

		public void SetPalette(HardwarePalette palette)
		{
			// Note: palette.Texture and palette.ColorShifts are updated at the same time
			// so we only need to check one of the two to know whether we must update the textures
			// also compare heights in case new palettes have been added
			if (palette.Texture == currentPaletteTexture && palette.Height == currentPaletteHeight)
				return;

			Flush();
			currentPaletteTexture = palette.Texture;
			currentPaletteHeight = palette.Height;

			SpriteRenderer.SetPalette(palette);
			WorldSpriteRenderer.SetPalette(palette);

			foreach (var r in WorldRenderers)
				r.SetPalette(palette);
		}

		public void EndFrame(IInputHandler inputHandler)
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			SpriteRenderer.PixelSnapScale = 0;

			screenBuffer.Unbind();

			// Render the compositor buffers to the screen
			// HACK / PERF: Fudge the coordinates to cover the actual window while keeping the buffer viewport parameters
			// This saves us two redundant (and expensive) SetViewportParams each frame
			RgbaSpriteRenderer.DrawSprite(screenSprite, new Vector3(0, lastBufferSize.Height, 0),
				new Vector3(lastBufferSize.Width / screenSprite.Size.X, -lastBufferSize.Height / screenSprite.Size.Y, 1f));
			Flush();

			Window.PumpInput(inputHandler);
			Context.Present();

			renderType = RenderType.None;
			UpdateWindowGeometry();
		}

		public void DrawBatch<T>(IVertexBuffer<T> vertices, IShader shader,
			int firstVertex, int numVertices, PrimitiveType type)
			where T : struct
		{
			vertices.Bind();
			shader.Bind();
			Context.DrawPrimitives(type, firstVertex, numVertices);
			PerfHistory.Increment("batches", 1);
		}

		public void DrawQuadBatch<T>(IVertexBuffer<T> vertices, IIndexBuffer indices, IShader shader, int numIndices, int start)
			where T : struct
		{
			vertices.Bind();
			indices.Bind();
			shader.Bind();
			Context.DrawElements(numIndices, start);
			PerfHistory.Increment("batches", 1);
		}

		public void Flush()
		{
			CurrentBatchRenderer = null;
		}

		public Size Resolution => Window.EffectiveWindowSize;
		public Size NativeResolution => Window.NativeWindowSize;
		public float WindowScale => Window.EffectiveWindowScale;
		public float NativeWindowScale => Window.NativeWindowScale;

		/// <summary>The UI scale currently applied (may be smaller than the Graphics.UIScale setting if the window is too small for it).</summary>
		public float UIScale => Window.EffectiveWindowScale / Window.NativeWindowScale;

		public GLProfile GLProfile => Window.GLProfile;
		public GLProfile[] SupportedGLProfiles => Window.SupportedGLProfiles;

		public interface IBatchRenderer { void Flush(); }

		public IBatchRenderer CurrentBatchRenderer
		{
			get => currentBatchRenderer;

			set
			{
				if (currentBatchRenderer == value)
					return;
				currentBatchRenderer?.Flush();
				currentBatchRenderer = value;
			}
		}

		public IFrameBuffer CreateFrameBuffer(Size s)
		{
			return Context.CreateFrameBuffer(s);
		}

		public IShader CreateShader(IShaderBindings bindings)
		{
			return Context.CreateShader(bindings);
		}

		public IVertexBuffer<T> CreateVertexBuffer<T>(T[] data, bool dynamic) where T : struct
		{
			return Context.CreateVertexBuffer(data, dynamic);
		}

		public void EnableScissor(Rectangle rect)
		{
			// Must remain inside the current scissor rect
			if (scissorState.Count > 0)
				rect = Rectangle.Intersect(rect, scissorState.Peek());

			Flush();

			if (renderType == RenderType.World)
			{
				var r = Rectangle.FromLTRB(
					rect.Left / WorldDownscaleFactor,
					rect.Top / WorldDownscaleFactor,
					(rect.Right + WorldDownscaleFactor - 1) / WorldDownscaleFactor,
					(rect.Bottom + WorldDownscaleFactor - 1) / WorldDownscaleFactor);
				worldBuffer.EnableScissor(r);
			}
			else
				Context.EnableScissor(rect.X, rect.Y, rect.Width, rect.Height);

			scissorState.Push(rect);
		}

		public void DisableScissor()
		{
			scissorState.Pop();
			Flush();

			if (renderType == RenderType.World)
			{
				// Restore previous scissor rect
				if (scissorState.Count > 0)
				{
					var rect = scissorState.Peek();
					var r = Rectangle.FromLTRB(
						rect.Left / WorldDownscaleFactor,
						rect.Top / WorldDownscaleFactor,
						(rect.Right + WorldDownscaleFactor - 1) / WorldDownscaleFactor,
						(rect.Bottom + WorldDownscaleFactor - 1) / WorldDownscaleFactor);
					worldBuffer.EnableScissor(r);
				}
				else
					worldBuffer.DisableScissor();
			}
			else
			{
				// Restore previous scissor rect
				if (scissorState.Count > 0)
				{
					var rect = scissorState.Peek();
					Context.EnableScissor(rect.X, rect.Y, rect.Width, rect.Height);
				}
				else
					Context.DisableScissor();
			}
		}

		public void EnableDepthBuffer()
		{
			Flush();
			Context.EnableDepthBuffer();
		}

		public void DisableDepthBuffer()
		{
			Flush();
			Context.DisableDepthBuffer();
		}

		public void ClearDepthBuffer()
		{
			Flush();
			Context.ClearDepthBuffer();
		}

		public void EnableAntialiasingFilter()
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			SpriteRenderer.EnablePixelArtScaling(true);
		}

		public void DisableAntialiasingFilter()
		{
			if (renderType != RenderType.UI)
				throw new InvalidOperationException($"EndFrame called with renderType = {renderType}, expected RenderType.UI.");

			Flush();
			SpriteRenderer.EnablePixelArtScaling(false);
		}

		public void GrabWindowMouseFocus()
		{
			Window.GrabWindowMouseFocus();
		}

		public void ReleaseWindowMouseFocus()
		{
			Window.ReleaseWindowMouseFocus();
		}

		public void SaveScreenshot(string path)
		{
			// Pull the data from the Texture directly to prevent the sheet from buffering it
			var src = screenBuffer.Texture.GetData();
			var srcWidth = screenSprite.Sheet.Size.Width;
			var destWidth = screenSprite.Bounds.Width;
			var destHeight = -screenSprite.Bounds.Height;

			ThreadPool.QueueUserWorkItem(_ =>
			{
				// Extract the screen rect from the (larger) backing surface
				var dest = new byte[4 * destWidth * destHeight];
				for (var y = 0; y < destHeight; y++)
					Array.Copy(src, 4 * y * srcWidth, dest, 4 * y * destWidth, 4 * destWidth);

				new Png(dest, SpriteFrameType.Bgra32, destWidth, destHeight).Save(path);
			});
		}

		public void Dispose()
		{
			worldBuffer?.Dispose();
			screenBuffer.Dispose();
			bufferSnapshot.Dispose();
			tempVertexBuffer.Dispose();
			quadIndexBuffer.Dispose();
			fontSheetBuilder?.Dispose();
			if (Fonts != null)
				foreach (var font in Fonts.Values)
					font.Dispose();
			Window.Dispose();
		}

		public void SetVSyncEnabled(bool enabled)
		{
			Window.Context.SetVSyncEnabled(enabled);
		}

		public string GetClipboardText()
		{
			return Window.GetClipboardText();
		}

		public bool SetClipboardText(string text)
		{
			return Window.SetClipboardText(text);
		}

		public bool TryOpenUrl(string url)
		{
			return Window.TryOpenUrl(url);
		}

		public string GLVersion => Context.GLVersion;

		public int DisplayCount => Window.DisplayCount;

		public int CurrentDisplay => Window.CurrentDisplay;
	}
}
