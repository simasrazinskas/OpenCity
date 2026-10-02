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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// One vertical screen strip of a building sprite (RCT2-style slicing). Drawn exactly where the full sprite would
	/// be drawn (same anchor, offset and half size), but sorted at <see cref="Pos"/>: the strip's own ground point.
	/// </summary>
	public sealed class IsoStripRenderable : IPalettedRenderable, IModifyableRenderable, IFinalizedRenderable
	{
		readonly Sprite strip;
		readonly WPos anchor;
		readonly WVec offset;
		readonly int2 halfSize;
		readonly float scale;

		public IsoStripRenderable(Sprite strip, WPos anchor, WVec offset, int2 halfSize, WPos sortPos, int zOffset,
			PaletteReference palette, float scale, float alpha, Vector3 tint, TintModifiers tintModifiers, bool isDecoration)
		{
			this.strip = strip;
			this.anchor = anchor;
			this.offset = offset;
			this.halfSize = halfSize;
			Pos = sortPos;
			this.scale = scale;
			ZOffset = zOffset;
			Palette = strip.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false) ? null : palette;
			Alpha = alpha;
			Tint = tint;
			TintModifiers = tintModifiers;
			IsDecoration = isDecoration;
		}

		public WPos Pos { get; }
		public int ZOffset { get; }
		public bool IsDecoration { get; }
		public PaletteReference Palette { get; }
		public float Alpha { get; }
		public Vector3 Tint { get; }
		public TintModifiers TintModifiers { get; }

		IsoStripRenderable With(WPos anchor, WPos sortPos, int zOffset, PaletteReference palette, float alpha, in Vector3 tint,
			TintModifiers modifiers, bool decoration)
		{
			return new IsoStripRenderable(strip, anchor, offset, halfSize, sortPos, zOffset, palette, scale, alpha, tint, modifiers, decoration);
		}

		public IPalettedRenderable WithPalette(PaletteReference newPalette)
		{
			return With(anchor, Pos, ZOffset, newPalette, Alpha, Tint, TintModifiers, IsDecoration);
		}

		public IRenderable WithZOffset(int newOffset) { return With(anchor, Pos, newOffset, Palette, Alpha, Tint, TintModifiers, IsDecoration); }
		public IRenderable OffsetBy(in WVec vec) { return With(anchor + vec, Pos + vec, ZOffset, Palette, Alpha, Tint, TintModifiers, IsDecoration); }
		public IRenderable AsDecoration() { return With(anchor, Pos, ZOffset, Palette, Alpha, Tint, TintModifiers, true); }
		public IModifyableRenderable WithAlpha(float newAlpha) { return With(anchor, Pos, ZOffset, Palette, newAlpha, Tint, TintModifiers, IsDecoration); }

		public IModifyableRenderable WithTint(in Vector3 newTint, TintModifiers newTintModifiers)
		{
			return With(anchor, Pos, ZOffset, Palette, Alpha, newTint, newTintModifiers, IsDecoration);
		}

		Vector3 Location(WorldRenderer wr)
		{
			return wr.Screen3DPxPosition(anchor) + wr.ScreenPxOffset(offset).ToVector3() - new Vector3(halfSize.X, halfSize.Y, 0);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		public void Render(WorldRenderer wr)
		{
			var a = Alpha;
			if ((TintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			Game.Renderer.WorldSpriteRenderer.DrawSprite(strip, Palette, Location(wr), scale, Alpha * Tint, a);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var b = ScreenBounds(wr);
			var tl = wr.Viewport.WorldToViewPx(new int2(b.Left, b.Top)).ToVector3();
			var br = wr.Viewport.WorldToViewPx(new int2(b.Right, b.Bottom)).ToVector3();
			Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Orange);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var o = Location(wr) + scale * strip.Offset;
			return new Rectangle((int)o.X, (int)o.Y, (int)(scale * strip.Size.X), (int)(scale * strip.Size.Y));
		}
	}

	/// <summary>
	/// Flat-shaded iso box (or flat lot when Height is 0) drawn inside the depth sort: the see-through stubs and
	/// simple code-drawn props. Corners are world positions; the box is the footprint rectangle raised by Height.
	/// </summary>
	public sealed class IsoBoxRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos min, max;
		readonly int height;
		readonly Color top, left, right;

		public IsoBoxRenderable(WPos min, WPos max, int height, Color top, Color left, Color right, WPos sortPos, int zOffset)
		{
			this.min = min;
			this.max = max;
			this.height = height;
			this.top = top;
			this.left = left;
			this.right = right;
			Pos = sortPos;
			ZOffset = zOffset;
		}

		public WPos Pos { get; }
		public int ZOffset { get; }
		public bool IsDecoration => false;

		public IRenderable WithZOffset(int newOffset) { return new IsoBoxRenderable(min, max, height, top, left, right, Pos, newOffset); }

		public IRenderable OffsetBy(in WVec vec)
		{
			return new IsoBoxRenderable(min + vec, max + vec, height, top, left, right, Pos + vec, ZOffset);
		}

		public IRenderable AsDecoration() { return this; }
		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		public void Render(WorldRenderer wr)
		{
			var r = Game.Renderer.WorldRgbaColorRenderer;
			var up = new WVec(0, 0, height);

			// Ground corners: back (min), right (+X), front (max), left (+Y).
			var back = new WPos(min.X, min.Y, min.Z);
			var east = new WPos(max.X, min.Y, min.Z);
			var front = new WPos(max.X, max.Y, min.Z);
			var west = new WPos(min.X, max.Y, min.Z);
			if (height > 0)
			{
				// Visible side faces: +Y (lit, screen lower left) and +X (shaded, screen lower right).
				r.FillRect(P(wr, west), P(wr, front), P(wr, front + up), P(wr, west + up), left);
				r.FillRect(P(wr, front), P(wr, east), P(wr, east + up), P(wr, front + up), right);
			}

			r.FillRect(P(wr, back + up), P(wr, east + up), P(wr, front + up), P(wr, west + up), top);
		}

		static Vector3 P(WorldRenderer wr, WPos p) { return wr.Screen3DPxPosition(p); }

		public void RenderDebugGeometry(WorldRenderer wr) { }

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var a = wr.ScreenPxPosition(new WPos(min.X, max.Y, min.Z));
			var b = wr.ScreenPxPosition(new WPos(max.X, min.Y, min.Z));
			var t = wr.ScreenPxPosition(new WPos(min.X, min.Y, min.Z + height));
			var f = wr.ScreenPxPosition(new WPos(max.X, max.Y, min.Z));
			return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(t.Y, f.Y), Math.Max(a.X, b.X), Math.Max(t.Y, f.Y));
		}
	}
}
