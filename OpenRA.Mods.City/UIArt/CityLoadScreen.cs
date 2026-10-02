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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common.LoadScreens;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.UIArt
{
	/// <summary>
	/// The OpenCity boot load screen (design/iso/ui/screens/loading-1280x720-1x.png): the darkened city picture, the
	/// pixel logo with its tagline plate and an RCT2 loading window with a progress bar, the current task and a tip.
	/// Everything is drawn procedurally at the device scale (the chrome does not exist yet while the mod loads). The mod
	/// reports no real progress while it loads, so the bar is a running marker.
	/// </summary>
	public sealed class CityLoadScreen : BlankLoadScreen
	{
		[FluentReference]
		const string Loading = "loadscreen-loading";

		[FluentReference]
		const string LoadingTitle = "menu-loading-title";

		[FluentReference]
		const string Tips = "menu-loading-tips";

		[FluentReference]
		const string Tagline = "label-city-tagline";

		[FluentReference]
		const string TipCount = "menu-loading-tip-count";

		const int WindowWidth = 560, WindowHeight = 118, WindowMargin = 56;

		/// <summary>Seconds each loading message stays on screen.</summary>
		const double MessageSeconds = 1.4;

		Dictionary<string, string> info;
		Stopwatch lastUpdate;
		Stopwatch clock;
		float scale;
		Sheet sheet;
		Sprite logo;
		CityLoadBackdrop backdrop;
		CityChromeStyle style;
		string[] messages = [];
		string[] tips = [];
		int firstMessage;
		int frames;
		int tip;
		string title, tagline;

		public override void Init(Manifest manifest, IReadOnlyFileSystem fileSystem)
		{
			base.Init(manifest, fileSystem);
			info = manifest.LoadScreen.ToDictionary(my => my.Value);
			messages = FluentProvider.GetMessage(Loading).Split(',').Select(x => x.Trim()).ToArray();
			tips = FluentProvider.GetMessage(Tips).Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
			title = FluentProvider.GetMessage(LoadingTitle);
			tagline = FluentProvider.GetMessage(Tagline);
			var random = new Random();
			firstMessage = random.Next(Math.Max(1, messages.Length));
			tip = random.Next(Math.Max(1, tips.Length));
			backdrop = new CityLoadBackdrop(fileSystem);
			style = new CityChromeStyle(fileSystem, info.GetValueOrDefault("Style", "city|uistyle.yaml"));
		}

		public override void Display()
		{
			// Limit load screens to at most 20 FPS
			if (Game.Renderer == null || (lastUpdate != null && lastUpdate.Elapsed.TotalSeconds < 0.05))
				return;

			lastUpdate ??= Stopwatch.StartNew();
			clock ??= Stopwatch.StartNew();
			var r = Game.Renderer;
			if (sheet == null || scale != r.WindowScale)
				CreateSprites(r.WindowScale);

			r.BeginUI();
			var res = r.Resolution;
			WidgetUtils.FillRectWithColor(new Rectangle(0, 0, res.Width, res.Height), Color.FromArgb(10, 12, 28));
			backdrop.Draw(r);

			var logoX = (res.Width - CityChromeGenerator.LogoWidth) / 2f;
			var logoTop = Math.Max(8, (res.Height - WindowHeight - WindowMargin - CityChromeGenerator.LogoHeight - 40) / 3f);
			r.RgbaSpriteRenderer.DrawSprite(logo, new Vector3(PixelSnap.Snap(logoX, scale), PixelSnap.Snap(logoTop, scale), 0));

			if (r.Fonts != null)
			{
				DrawTagline(r, res.Width / 2f, logoTop + CityChromeGenerator.LogoHeight + 6);
				DrawWindow(r, (res.Width - WindowWidth) / 2f, res.Height - WindowHeight - WindowMargin);
			}

			r.EndFrame(new NullInputHandler());
			lastUpdate.Restart();

			// Debug aid for the headless UI checks: OPENCITY_LOADSHOT=<file.png> saves the 3rd frame of the load screen.
			if (++frames == 3 && Environment.GetEnvironmentVariable("OPENCITY_LOADSHOT") is { Length: > 0 } path)
				r.SaveScreenshot(path);
		}

		void Fill(float x, float y, float w, float h, Rgba c)
		{
			var b = Color.FromArgb((int)c.A, (int)c.R, (int)c.G, (int)c.B);
			var s = scale;
			var x0 = PixelSnap.Snap(x, s);
			var y0 = PixelSnap.Snap(y, s);
			var x1 = Math.Max(x0 + 1 / s, PixelSnap.Snap(x + w, s));
			var y1 = Math.Max(y0 + 1 / s, PixelSnap.Snap(y + h, s));
			Game.Renderer.RgbaColorRenderer.FillRect(new Vector3(x0 - 0.5f, y0 - 0.5f, 0), new Vector3(x1 - 0.5f, y1 - 0.5f, 0), b);
		}

		float Bevel => Math.Max(1, (int)Math.Floor(scale + 0.001f)) / scale;

		Rgba Brown(int i) { return style.Ramp("brown", i); }

		void DrawTagline(Renderer r, float cx, float y)
		{
			var font = r.Fonts["Bold"];
			var size = font.Measure(tagline);
			var b = Bevel;
			var w = size.X + 16;
			const int H = 22;
			var x = cx - w / 2;
			Fill(x - 2 * b, y - 2 * b, w + 4 * b, H + 4 * b, Rgba.Parse("#1a0c04"));
			Fill(x, y, w, H, Rgba.Parse("#7a3a10"));
			Fill(x, y, w, b, Rgba.Parse("#c27a30"));
			var ty = MathF.Round(y + (H - size.Y - font.TopOffset) / 2f);
			font.DrawTextWithShadow(
				tagline, new Vector2(MathF.Round(x + 8), ty), Color.FromArgb(0xff, 0xe9, 0xa8),
				Color.FromArgb(0x2a, 0x12, 0x04), Color.FromArgb(0x2a, 0x12, 0x04), 1);
		}

		void DrawWindow(Renderer r, float x, float y)
		{
			var b = Bevel;
			const int W = WindowWidth;
			const int H = WindowHeight;
			Fill(x - b, y - b, W + 2 * b, H + 2 * b, Brown(0));
			Fill(x, y, W, H, Brown(5));
			Fill(x, y, W, b, Brown(7));
			Fill(x, y, b, H, Brown(7));
			Fill(x, y + H - b, W, b, Brown(2));
			Fill(x + W - b, y, b, H, Brown(2));

			var ink = Color.FromArgb(0x14, 0x0e, 0x08);
			var muted = style.Ramp("brown", 1);
			var bold = r.Fonts["Bold"];
			var tiny = r.Fonts["Tiny"];
			var ix = x + 10;
			const int Iw = W - 20;
			var ty = y + 10;
			bold.DrawText(title, new Vector2(MathF.Round(ix), MathF.Round(ty)), ink);

			// The progress trough with a marker that runs back and forth
			var by = ty + bold.Measure(title).Y + 6;
			const float Bh = 14f;
			Fill(ix, by, Iw, Bh, Brown(0));
			Fill(ix + b, by + b, Iw - 2 * b, Bh - 2 * b, Rgba.Parse("#2a1a0c"));
			const float Segment = 4f;
			var count = (int)((Iw - 4 * b) / (Segment + b));
			var t = clock.Elapsed.TotalSeconds * 14;
			var span = count + 12;
			var head = (int)(t % (2 * span));
			head = head < span ? head : 2 * span - head;
			for (var i = 0; i < count; i++)
			{
				var d = head - 6 - i;
				if (d < -6 || d > 6)
					continue;

				var shade = Math.Abs(d) <= 2 ? 6 : Math.Abs(d) <= 4 ? 5 : 4;
				Fill(ix + 2 * b + i * (Segment + b), by + 2 * b, Segment, Bh - 4 * b, style.Ramp("green", shade));
			}

			var message = messages.Length == 0 ? "" : messages[(firstMessage + (int)(clock.Elapsed.TotalSeconds / MessageSeconds)) % messages.Length];
			var my = by + Bh + 5;
			tiny.DrawText(message, new Vector2(MathF.Round(ix), MathF.Round(my)), ink);

			// The tip well
			var wy = my + tiny.Measure("A").Y + 8;
			var wh = y + H - 10 - wy;
			Fill(ix, wy, Iw, wh, Brown(7));
			Fill(ix, wy, Iw, b, Brown(2));
			Fill(ix, wy, b, wh, Brown(2));
			Fill(ix, wy + wh - b, Iw, b, Brown(7));
			Fill(ix + Iw - b, wy, b, wh, Brown(7));
			Fill(ix + b, wy + b, Iw - 2 * b, wh - 2 * b, Brown(6));
			if (tips.Length > 0)
			{
				var text = tips[tip % tips.Length];
				const int Width = Iw - 16;
				var lines = Wrap(tiny, text, Width);
				for (var i = 0; i < lines.Count && i < 3; i++)
					tiny.DrawText(lines[i], new Vector2(MathF.Round(ix + 8), MathF.Round(wy + 6 + i * (tiny.Measure("A").Y + 2))), ink);

				var counter = FluentProvider.GetMessage(TipCount, "n", tip % tips.Length + 1, "total", tips.Length);
				var cs = tiny.Measure(counter);
				tiny.DrawText(
					counter, new Vector2(MathF.Round(ix + Iw - cs.X - 6), MathF.Round(wy + wh - cs.Y - 4)),
					Color.FromArgb((int)muted.A, (int)muted.R, (int)muted.G, (int)muted.B));
			}
		}

		static List<string> Wrap(SpriteFont font, string text, int width)
		{
			var lines = new List<string>();
			var line = "";
			foreach (var word in text.Split(' '))
			{
				var candidate = line.Length == 0 ? word : line + " " + word;
				if (font.Measure(candidate).X > width && line.Length > 0)
				{
					lines.Add(line);
					line = word;
				}
				else
					line = candidate;
			}

			if (line.Length > 0)
				lines.Add(line);

			return lines;
		}

		void CreateSprites(float windowScale)
		{
			scale = windowScale;
			sheet?.Dispose();
			var art = CityChromeGenerator.RenderLogo(fileSystem, scale,
				info.GetValueOrDefault("Style", "city|uistyle.yaml"), info.GetValueOrDefault("Icons", "city|bits/chrome/cityicons.vec"));

			var size = new Size(Exts.NextPowerOf2(art.Width), Exts.NextPowerOf2(art.Height));
			sheet = new Sheet(SheetType.BGRA, size);
			sheet.CreateBuffer();
			logo = new Sprite(sheet, new Rectangle(0, 0, art.Width, art.Height), TextureChannel.RGBA, 1f / scale);
			Util.FastCopyIntoChannel(logo, art.Data, SpriteFrameType.Rgba32);
			sheet.CommitBufferedData();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				sheet?.Dispose();
				backdrop?.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}
