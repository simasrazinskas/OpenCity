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

using System.Collections.Generic;
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class SpriteRenderable : IPalettedRenderable, IModifyableRenderable, IFinalizedRenderable
	{
		public static readonly IEnumerable<IRenderable> None = [];

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha,
			Vector3 tint, TintModifiers tintModifiers, bool isDecoration, WAngle rotation)
		{
			Sprite = sprite;
			Anchor = pos;
			Offset = offset;
			ZOffset = zOffset;
			Palette = palette;
			Scale = scale;
			Rotation = rotation;
			Tint = tint;
			IsDecoration = isDecoration;
			TintModifiers = tintModifiers;
			Alpha = alpha;

			// PERF: Remove useless palette assignments for RGBA sprites
			// HACK: This is working around the fact that palettes are defined on traits rather than sequences
			// and can be removed once this has been fixed
			if (sprite.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false))
				Palette = null;
		}

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha,
			Vector3 tint, TintModifiers tintModifiers, bool isDecoration)
			: this(sprite, pos, offset, zOffset, palette, scale, alpha, tint, tintModifiers, isDecoration, WAngle.Zero) { }

		public WPos Pos => Anchor + Offset;

		/// <summary>The sprite, anchor position, scale and rotation (for traits that re-slice the sprite, e.g. iso strip sorting).</summary>
		public Sprite Sprite { get; }
		public WPos Anchor { get; }
		public float Scale { get; }
		public WAngle Rotation { get; } = WAngle.Zero;

		public WVec Offset { get; }
		public PaletteReference Palette { get; }
		public int ZOffset { get; }
		public bool IsDecoration { get; }

		public float Alpha { get; }
		public Vector3 Tint { get; }
		public TintModifiers TintModifiers { get; }

		public IPalettedRenderable WithPalette(PaletteReference newPalette)
		{
			return new SpriteRenderable(Sprite, Anchor, Offset, ZOffset, newPalette, Scale, Alpha, Tint, TintModifiers, IsDecoration, Rotation);
		}

		public IRenderable WithZOffset(int newOffset)
		{
			return new SpriteRenderable(Sprite, Anchor, Offset, newOffset, Palette, Scale, Alpha, Tint, TintModifiers, IsDecoration, Rotation);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new SpriteRenderable(Sprite, Anchor + vec, Offset, ZOffset, Palette, Scale, Alpha, Tint, TintModifiers, IsDecoration, Rotation);
		}

		public IRenderable AsDecoration()
		{
			return new SpriteRenderable(Sprite, Anchor, Offset, ZOffset, Palette, Scale, Alpha, Tint, TintModifiers, true, Rotation);
		}

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new SpriteRenderable(Sprite, Anchor, Offset, ZOffset, Palette, Scale, newAlpha, Tint, TintModifiers, IsDecoration, Rotation);
		}

		public IModifyableRenderable WithTint(in Vector3 newTint, TintModifiers newTintModifiers)
		{
			return new SpriteRenderable(Sprite, Anchor, Offset, ZOffset, Palette, Scale, Alpha, newTint, newTintModifiers, IsDecoration, Rotation);
		}

		Vector3 ScreenPosition(WorldRenderer wr)
		{
			var s = 0.5f * Scale * Sprite.Size;
			return wr.Screen3DPxPosition(Anchor) + wr.ScreenPxOffset(Offset).ToVector3() - new Vector3((int)s.X, (int)s.Y, s.Z);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var wsr = Game.Renderer.WorldSpriteRenderer;
			var t = Alpha * Tint;
			if (wr.TerrainLighting != null && (TintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.TintAt(Anchor);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = Alpha;
			if ((TintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			wsr.DrawSprite(Sprite, Palette, ScreenPosition(wr), Scale, t, a, Rotation.RendererRadians());
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var pos = ScreenPosition(wr) + Sprite.Offset;
			var tl = wr.Viewport.WorldToViewPx(pos).ToVector3();
			var br = wr.Viewport.WorldToViewPx(pos + Sprite.Size).ToVector3();
			if (Rotation == WAngle.Zero)
				Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);
			else
				Game.Renderer.RgbaColorRenderer.DrawPolygon(Util.RotateQuad(tl, br - tl, Rotation.RendererRadians()), 1, Color.Red);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var screenOffset = ScreenPosition(wr) + Sprite.Offset;
			return Util.BoundingRectangle(screenOffset, Sprite.Size, Rotation.RendererRadians());
		}
	}
}
