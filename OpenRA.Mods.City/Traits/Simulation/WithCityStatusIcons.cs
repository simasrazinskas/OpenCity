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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Draws the problem icons above a city building: a severity tile (grey, blue, yellow, orange, red...) with a glyph on top.",
		"Shows the two worst active problems side by side (the second slot cycles when more apply). The data comes from CityProblems (see ICityProblems).",
		"The icons are screen-space annotations: they follow the building at any zoom but keep their size (scaled by the UI scale only).")]
	public class WithCityStatusIconsInfo : TraitInfo
	{
		[Desc("Image containing the tile and glyph sequences.")]
		public readonly string Image = "envicons";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence with one 20x20 tile per ProblemTier.")]
		public readonly string TileSequence = "tiles";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence with one 16x16 glyph per CityProblem (frame = problem - 1).")]
		public readonly string GlyphSequence = "glyphs";

		[PaletteReference]
		public readonly string Palette = "chrome";

		[Desc("Fallback height of the building sprite above its footprint, in world pixels (when it has no mouse bounds).")]
		public readonly int BuildingHeight = 28;

		[Desc("Gap between the roof and the bottom of the icons, in UI pixels.")]
		public readonly int IconGap = 2;

		[Desc("World ticks the second icon stays on one problem when more than two apply.")]
		public readonly int CycleTicks = 50;

		[Desc("Horizontal distance between the two icons, in UI pixels.")]
		public readonly int IconSpacing = 21;

		[Desc("Below this zoom only problems of tier Major or worse are drawn (declutters the zoomed-out view).",
			"The zoom is measured in world pixels per UI pixel (viewport zoom / UI scale), since the icons follow the UI scale.")]
		public readonly float MajorOnlyBelowZoom = 0.6f;

		[Desc("Below this zoom (world pixels per UI pixel) only the worst problem is drawn, so neighbouring icons do not overlap.")]
		public readonly float SingleIconBelowZoom = 1f;

		public override object Create(ActorInitializer init) { return new WithCityStatusIcons(init.Self, this); }
	}

	public class WithCityStatusIcons : IRenderAnnotations, INotifyCreated
	{
		readonly WithCityStatusIconsInfo info;
		readonly int footprintRows;
		CityProblems problems;
		ISpriteSequence tiles;
		ISpriteSequence glyphs;

		public WithCityStatusIcons(Actor self, WithCityStatusIconsInfo info)
		{
			this.info = info;
			footprintRows = self.Info.TraitInfoOrDefault<BuildingInfo>()?.Dimensions.Y ?? 1;
		}

		void INotifyCreated.Created(Actor self)
		{
			problems = self.TraitOrDefault<CityProblems>();
		}

		bool IRenderAnnotations.SpatiallyPartitionable => true;

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (problems == null || problems.Active == 0 || self.World.FogObscures(self))
				return SpriteRenderable.None;

			var world = self.World;
			var now = world.WorldTick;
			Span<int> list = stackalloc int[ProblemCatalog.Count + 1];
			Span<int> priority = stackalloc int[ProblemCatalog.Count + 1];
			var n = 0;
			var density = wr.Viewport.Zoom / Game.Settings.Graphics.UIScale;
			var majorOnly = density < info.MajorOnlyBelowZoom;
			var bits = problems.Active;
			while (bits != 0)
			{
				var i = System.Numerics.BitOperations.TrailingZeroCount(bits);
				bits &= bits - 1;
				var tier = problems.Tier((CityProblem)i, now);
				if (majorOnly && tier < ProblemTier.Major)
					continue;

				// Insertion sort by priority (descending), stable by problem id.
				var p = ProblemCatalog.Priority(tier);
				var at = n;
				while (at > 0 && priority[at - 1] < p)
				{
					list[at] = list[at - 1];
					priority[at] = priority[at - 1];
					at--;
				}

				list[at] = i;
				priority[at] = p;
				n++;
			}

			if (n == 0)
				return SpriteRenderable.None;

			tiles ??= world.Map.Sequences.GetSequence(info.Image, info.TileSequence);
			glyphs ??= world.Map.Sequences.GetSequence(info.Image, info.GlyphSequence);

			var second = n > 1 && density >= info.SingleIconBelowZoom ? 1 + now / Math.Max(1, info.CycleTicks) % (n - 1) : -1;

			// Anchor on the top of the building's screen bounds (footprint plus height, so tall towers carry their icons on
			// the roof), then lay the icons out in whole UI pixels so they stay crisp at any zoom.
			var anchor = wr.Viewport.WorldToViewPx(RoofPx(self, wr));
			var tileSize = tiles.GetSprite(0).Size;
			var y = anchor.Y - info.IconGap - (int)tileSize.Y / 2;
			var palette = wr.Palette(info.Palette);
			var result = new List<IRenderable>(4);
			var spacing = second >= 0 ? info.IconSpacing : 0;
			AddIcon(result, self, palette, (CityProblem)list[0], now, new int2(anchor.X - spacing / 2, y));
			if (second >= 0)
				AddIcon(result, self, palette, (CityProblem)list[second], now, new int2(anchor.X + spacing - spacing / 2, y));

			return result;
		}

		int2 RoofPx(Actor self, WorldRenderer wr)
		{
			var center = wr.ScreenPxPosition(self.CenterPosition);
			var bounds = self.MouseBounds(wr);
			if (!bounds.IsEmpty)
				return new int2(center.X, bounds.BoundingRect.Top);

			var top = int.MaxValue;
			foreach (var r in self.ScreenBounds(wr))
				top = Math.Min(top, r.Top);

			if (top != int.MaxValue)
				return new int2(center.X, top);

			var roof = self.CenterPosition - new WVec(0, (footprintRows * 16 + info.BuildingHeight) * 1024 / 32, 0);
			return wr.ScreenPxPosition(roof);
		}

		void AddIcon(List<IRenderable> result, Actor self, PaletteReference palette, CityProblem problem, int now, int2 center)
		{
			// UI sprites are drawn from their top-left corner: centre them on whole UI pixels.
			var tier = problems.Tier(problem, now);
			var tile = tiles.GetSprite((int)tier);
			var glyph = glyphs.GetSprite((int)problem - 1);
			result.Add(new UISpriteRenderable(tile, self.CenterPosition, TopLeft(center, tile), 0, palette));
			result.Add(new UISpriteRenderable(glyph, self.CenterPosition, TopLeft(center, glyph), 1, palette));
		}

		static Vector2 TopLeft(int2 center, Sprite sprite)
		{
			return new Vector2(center.X - (int)sprite.Size.X / 2, center.Y - (int)sprite.Size.Y / 2);
		}
	}
}
