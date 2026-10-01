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

using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// A text annotation (UI pass) anchored to a world position but laid out in UI pixels: the anchor follows the world at
	/// any zoom while the text keeps its size and spacing (which only follow the UI scale), and it is placed on whole UI
	/// pixels so the glyphs stay crisp. The text is centred horizontally; its top edge sits <c>uiOffset.Y</c> UI pixels
	/// below the anchor (negative values move it up).
	/// </summary>
	public class CityAnnotationText : IRenderable, IFinalizedRenderable
	{
		readonly SpriteFont font;
		readonly int2 uiOffset;
		readonly Color color;
		readonly string text;

		public CityAnnotationText(SpriteFont font, WPos anchor, int2 uiOffset, Color color, string text)
		{
			this.font = font;
			Pos = anchor;
			this.uiOffset = uiOffset;
			this.color = color;
			this.text = text;
		}

		public WPos Pos { get; }
		public int ZOffset => 0;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(in WVec vec) { return new CityAnnotationText(font, Pos + vec, uiOffset, color, text); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		int2 TopLeft(WorldRenderer wr)
		{
			var anchor = wr.Viewport.WorldToViewPx(wr.ScreenPxPosition(Pos));
			return anchor + uiOffset - new int2(font.Measure(text).X / 2, 0);
		}

		public void Render(WorldRenderer wr)
		{
			font.DrawTextWithContrast(text, TopLeft(wr).ToVector2(), color,
				ChromeMetrics.Get<Color>("TextContrastColorDark"), ChromeMetrics.Get<Color>("TextContrastColorLight"), 1);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var tl = TopLeft(wr);
			Game.Renderer.RgbaColorRenderer.DrawRect(tl.ToVector3(), (tl + font.Measure(text)).ToVector3(), 1, Color.Red);
		}

		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }

		/// <summary>Line height (UI pixels) for stacking several annotation lines.</summary>
		public static int LineHeight(SpriteFont font) { return font.Measure("Ag").Y + 2; }
	}
}
