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
		"Shows the two worst active problems side by side (the second slot cycles when more apply). The data comes from CityProblems (see ICityProblems).")]
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

		[Desc("Approximate height of the building sprite above its footprint, in pixels.")]
		public readonly int BuildingHeight = 28;

		[Desc("World ticks the second icon stays on one problem when more than two apply.")]
		public readonly int CycleTicks = 50;

		[Desc("Horizontal distance between the two icons, in pixels.")]
		public readonly int IconSpacing = 21;

		[Desc("Below this zoom only problems of tier Major or worse are drawn (declutters the zoomed-out view).")]
		public readonly float MajorOnlyBelowZoom = 0.6f;

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
			var majorOnly = wr.Viewport.Zoom < info.MajorOnlyBelowZoom;
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

			var second = n > 1 ? 1 + now / Math.Max(1, info.CycleTicks) % (n - 1) : -1;
			var yPixels = footprintRows * 16 + info.BuildingHeight + 12;
			var palette = wr.Palette(info.Palette);
			var result = new List<IRenderable>(4);
			var spacing = second >= 0 ? info.IconSpacing : 0;
			AddIcon(result, self, palette, (CityProblem)list[0], now, second >= 0 ? -spacing / 2 : 0, yPixels);
			if (second >= 0)
				AddIcon(result, self, palette, (CityProblem)list[second], now, spacing - spacing / 2, yPixels);

			return result;
		}

		void AddIcon(List<IRenderable> result, Actor self, PaletteReference palette, CityProblem problem, int now, int xPixels, int yPixels)
		{
			var tier = problems.Tier(problem, now);
			var offset = new WVec(xPixels * 32, -yPixels * 32, 0);
			result.Add(new SpriteRenderable(tiles.GetSprite((int)tier), self.CenterPosition, offset, 0, palette, 1f, 1f, Vector3.One, TintModifiers.None, true));
			result.Add(new SpriteRenderable(glyphs.GetSprite((int)problem - 1), self.CenterPosition, offset, 1, palette, 1f, 1f, Vector3.One, TintModifiers.None, true));
		}
	}
}
