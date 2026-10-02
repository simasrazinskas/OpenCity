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
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Render-time building facing toward the access road (ENGINE-PLAN §3.4). Pure function of sim state.</summary>
	public static class IsoFacing
	{
		/// <summary>Facing index per side: 0 = front toward +Y, 1 = +X, 2 = -Y, 3 = -X.</summary>
		public static readonly CVec[] Directions = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

		/// <summary>
		/// Facing of a W x D footprint at <paramref name="origin"/> (top-left cell): toward <paramref name="accessRoad"/> when
		/// it is set, else toward the side with the most road cells (long sides first on ties), else the long side. ART
		/// renders every lot in all four facings (a W x D actor shows its D x W model turned at 1 and 3), so any side works.
		/// </summary>
		public static int Choose(IRoadNetwork roads, CPos origin, int width, int depth, CPos accessRoad)
		{
			var side = accessRoad != CPos.Zero ? SideOf(accessRoad, origin, width, depth) : -1;
			if (side < 0)
				side = BestSide(roads, origin, width, depth);

			return side;
		}

		/// <summary>Facing for a footprint that is not placed yet (tool ghosts): the side with the most road cells.</summary>
		public static int ForGhost(IRoadNetwork roads, CPos origin, int width, int depth)
		{
			return BestSide(roads, origin, width, depth);
		}

		static int SideOf(CPos road, CPos origin, int width, int depth)
		{
			if (road.Y >= origin.Y + depth && road.X >= origin.X && road.X < origin.X + width)
				return 0;

			if (road.X >= origin.X + width && road.Y >= origin.Y && road.Y < origin.Y + depth)
				return 1;

			if (road.Y < origin.Y && road.X >= origin.X && road.X < origin.X + width)
				return 2;

			if (road.X < origin.X && road.Y >= origin.Y && road.Y < origin.Y + depth)
				return 3;

			return -1;
		}

		static int BestSide(IRoadNetwork roads, CPos origin, int width, int depth)
		{
			var fallback = depth > width ? 1 : 0;
			Span<int> order = depth > width ? [1, 3, 0, 2] : [0, 2, 1, 3];
			if (roads == null)
				return fallback;

			var best = -1;
			var bestCount = 0;
			foreach (var side in order)
			{
				var count = 0;
				var along = side == 0 || side == 2 ? width : depth;
				for (var i = 0; i < along; i++)
				{
					var cell = side switch
					{
						0 => new CPos(origin.X + i, origin.Y + depth),
						1 => new CPos(origin.X + width, origin.Y + i),
						2 => new CPos(origin.X + i, origin.Y - 1),
						_ => new CPos(origin.X - 1, origin.Y + i)
					};

					if (roads.IsRoad(cell))
						count++;
				}

				if (count > bestCount)
				{
					best = side;
					bestCount = count;
				}
			}

			return best >= 0 ? best : fallback;
		}

		/// <summary>Facing toward the side of the footprint with the most water cells (terrain type "Water"), else 0.</summary>
		public static int TowardWater(Map map, CPos origin, int width, int depth)
		{
			var best = 0;
			var bestCount = 0;
			for (var side = 0; side < 4; side++)
			{
				var count = 0;
				var along = side == 0 || side == 2 ? width : depth;
				for (var i = 0; i < along; i++)
				{
					// Look two cells out: shores are often one land cell away from the open water.
					for (var d = 0; d < 2; d++)
					{
						var cell = side switch
						{
							0 => new CPos(origin.X + i, origin.Y + depth + d),
							1 => new CPos(origin.X + width + d, origin.Y + i),
							2 => new CPos(origin.X + i, origin.Y - 1 - d),
							_ => new CPos(origin.X - 1 - d, origin.Y + i)
						};

						if (map.Contains(cell) && map.GetTerrainInfo(cell).Type == "Water")
							count++;
					}
				}

				if (count > bestCount)
				{
					best = side;
					bestCount = count;
				}
			}

			return best;
		}

		/// <summary>Flat colour of a building's see-through stub, by zone family (services: slate).</summary>
		public static Color StubColor(CityBuilding building)
		{
			if (building == null)
				return Color.FromArgb(255, 150, 150, 160);

			switch (building.Category)
			{
				case ZoneCategory.Residential: return Color.FromArgb(255, 112, 168, 96);
				case ZoneCategory.Commercial: return Color.FromArgb(255, 96, 140, 200);
				case ZoneCategory.Industrial: return Color.FromArgb(255, 200, 176, 88);
				case ZoneCategory.Office: return Color.FromArgb(255, 96, 184, 184);
				default: return Color.FromArgb(255, 150, 150, 168);
			}
		}
	}
}
