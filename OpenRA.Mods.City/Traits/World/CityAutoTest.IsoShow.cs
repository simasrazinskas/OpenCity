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
	// Scenario "isoshow": the basic town plus a dense downtown of level 5 towers placed directly (the same CreateActor call
	// ZoneGrowth uses), for the iso depth-sorting screenshots: towers side by side, 3x1 / 4x3 lots, cars on the streets
	// behind tall buildings. Test only; not part of the golden scenarios. Lots: residential high block (u 14..19, v -8..-1)
	// with a row of 2x2 towers and 3x2 slabs, then the office, commercial and terrace blocks.
	public partial class CityAutoTest
	{
		static readonly (string Actor, int U, int V, int Level)[] IsoShowLots =
		[
			("res-high-2x2", 14, -8, 5), ("res-high-2x2", 16, -8, 5), ("res-high-2x2", 18, -8, 4),
			("res-high-3x2", 14, -6, 5), ("res-high-3x2", 17, -6, 3),
			("res-high-2x2", 14, -4, 5), ("res-high-2x2", 16, -4, 5), ("res-high-2x2", 18, -4, 5),
			("res-high-3x2", 14, -2, 2), ("res-high-3x2", 17, -2, 5),

			// Office block (u 21..26): glass towers next to each other.
			("off-high-3x3", 21, -8, 5), ("off-high-3x3", 24, -8, 5),
			("off-high-3x3", 21, -5, 4), ("off-high-3x3", 24, -5, 5),
			("off-high-2x2", 21, -2, 5), ("off-high-2x2", 23, -2, 3), ("off-high-2x2", 25, -2, 5),

			// Commercial high block (u 14..19, v 1..4): a 4x3 mall and a 2x2.
			("com-high-4x3", 14, 1, 5), ("com-high-2x2", 18, 1, 4), ("com-high-2x2", 18, 3, 5),

			// Long thin lots in the residential low block (u 7..12): 3x1 terraces and 1x3.
			("res-row-3x1", 7, -8, 3), ("res-row-3x1", 10, -8, 4), ("res-row-3x1", 7, -7, 2), ("res-row-3x1", 10, -7, 5),
			("res-row-1x3", 7, -4, 4), ("res-row-1x3", 8, -4, 3), ("res-row-2x2", 10, -4, 5),
		];

		void ScheduleIsoShow()
		{
			scheduled.Add((20, "isoshow-towers", IsoShowTowers));
		}

		void IsoShowTowers(World w, Player p)
		{
			var placed = 0;
			foreach (var (actor, u, v, level) in IsoShowLots)
			{
				if (!w.Map.Rules.Actors.ContainsKey(actor))
					continue;

				var origin = At(u, v);
				var bi = w.Map.Rules.Actors[actor].TraitInfoOrDefault<Common.Traits.BuildingInfo>();
				var free = true;
				if (bi != null)
					foreach (var c in bi.Tiles(origin))
						if (w.ActorMap.AnyActorsAt(c))
							free = false;

				if (!free)
					continue;

				w.CreateActor(actor, [new LocationInit(origin), new OwnerInit(p), new GrowableLevelInit(level), new GrowableThemeInit(placed % 2 + 1)]);
				placed++;
			}

			Report(w, $"isoshow placed {placed} lots");
		}
	}
}
