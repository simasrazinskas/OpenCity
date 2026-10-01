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

namespace OpenRA.Mods.City.Traits
{
	// Read-only queries for the UI (zone overlay): nothing here changes sim state.
	public partial class ZoneGrowth
	{
		/// <summary>
		/// False if no lot of the cell's zone can cover this empty zoned cell with the current roads and zones
		/// (no connected road within lot depth, or the frontage there is narrower than the narrowest shape).
		/// Optimistic about the depth of neighbouring columns, so a "dead" cell really never grows.
		/// </summary>
		public bool CanEverGrow(CPos cell)
		{
			if (zones == null || roads == null || catalog == null)
				return true;

			var zone = zones.GetZone(cell);
			if (zone == ZoneType.None || !catalog.CanGrow(zone) || !IsZoneUnlocked(zone))
				return zone == ZoneType.None;

			for (var side = 0; side < 4; side++)
			{
				var step = CityUtils.Neighbours4[side];
				var axis = side % 2 == 0 ? new CVec(1, 0) : new CVec(0, 1);
				for (var k = 1; k <= LotCatalog.MaxDepth; k++)
				{
					var p = cell + new CVec(step.X * k, step.Y * k);
					if (!zones.IsFrontageRoad(p))
					{
						// Only free, same-zone cells may lie between the cell and the road.
						if (zones.GetZone(p) != zone)
							break;

						continue;
					}

					if (!roads.IsConnectedToOutside(p))
						break;

					// Frontage cell next to the road, `depth` rows from the cell.
					var frontage = cell + new CVec(step.X * (k - 1), step.Y * (k - 1));
					var run = 1;
					for (var dir = -1; dir <= 1; dir += 2)
					{
						for (var i = 1; i < 6; i++)
						{
							var c = frontage + new CVec(axis.X * i * dir, axis.Y * i * dir);
							var r = c + step;
							if (zones.GetZone(c) != zone || !zones.IsFrontageRoad(r))
								break;

							run++;
						}
					}

					foreach (var shape in catalog.Shapes(zone))
						if (shape.W <= run && shape.D >= k)
							return true;

					break;
				}
			}

			return false;
		}
	}
}
