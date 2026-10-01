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
using System.Text;

namespace OpenRA.Mods.City.Traits
{
	public partial class PropertyRegistry
	{
		/// <summary>Rolling hash of the lot state for determinism checks (replays compare it).</summary>
		public int StateHash
		{
			get
			{
				unchecked
				{
					var h = 17;
					for (var i = 0; i < records.Count; i++)
					{
						var p = records[i].P;
						h = h * 31 + p.Id;
						h = h * 31 + p.Origin.X * 4099 + p.Origin.Y;
						h = h * 31 + (int)p.Zone * 8 + p.Level;
						h = h * 31 + p.HouseholdSlots + p.TotalJobSlots * 7;
						h = h * 31 + (p.Operational ? 1 : 0) + p.AccessRoad.X * 13 + p.AccessRoad.Y;
						if (records[i].Growable != null)
							h = h * 31 + records[i].Growable.Condition;
					}

					return h;
				}
			}
		}

		/// <summary>Finished, not abandoned lots of a zone at or above a level: number of lots and cells (signature unlock rules).</summary>
		public void CountLots(ZoneType zone, int minLevel, out int lots, out int cells)
		{
			lots = 0;
			cells = 0;
			for (var i = 0; i < records.Count; i++)
			{
				var g = records[i].Growable;
				if (g == null || g.Zone != zone || g.Level < minLevel || g.Abandoned || (g.UnderConstruction && !g.IsUpgrading))
					continue;

				lots++;
				cells += g.Cells;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			int homes = 0, jobs = 0, households = 0, filled = 0, abandoned = 0, building = 0, lots = 0, rent = 0, declining = 0, landSum = 0, corners = 0, storage = 0;
			long condition = 0;
			var levels = new int[6];
			var shapes = new int[5, 5];
			var decliningByZone = new int[16];
			var upByZone = new int[16];
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				var p = r.P;
				homes += p.HouseholdSlots;
				households += p.Households;
				jobs += p.TotalJobSlots;
				filled += p.TotalJobsFilled;
				var g = r.Growable;
				if (g == null)
					continue;

				lots++;
				if (records[i].Corner)
					corners++;

				storage += g.StorageCapacity;
				levels[Math.Clamp(g.Level, 0, 5)]++;
				shapes[Math.Min(4, p.Width), Math.Min(4, p.Depth)]++;
				rent += p.RentPerMonth;
				landSum += p.LandValue;
				condition += g.Condition;
				if (g.Abandoned)
					abandoned++;
				else if (g.UnderConstruction && !g.IsUpgrading)
					building++;
				else if (g.Declining)
				{
					declining++;
					decliningByZone[(int)g.Zone & 15]++;
				}

				if (g.Level > 1)
					upByZone[(int)g.Zone & 15]++;
			}

			var sb = new StringBuilder();
			for (var w = 1; w <= 4; w++)
				for (var d = 1; d <= 4; d++)
					if (shapes[w, d] > 0)
						sb.Append(w).Append('x').Append(d).Append(':').Append(shapes[w, d]).Append(' ');

			var zoneParts = new StringBuilder();
			for (var z = 1; z < 13; z++)
				if (decliningByZone[z] > 0 || upByZone[z] > 0)
					zoneParts.Append((ZoneType)z).Append(":up").Append(upByZone[z]).Append("/down").Append(decliningByZone[z]).Append(' ');

			LodgingTotals(out var rooms, out var guests);
			var avgRent = lots > 0 ? rent / lots : 0;
			var avgCondition = lots > 0 ? (int)(condition / lots) : 0;
			return $"properties n={records.Count} lots={lots} homes={households}/{homes} jobs={filled}/{jobs} " +
				$"levels=L1:{levels[1]},L2:{levels[2]},L3:{levels[3]},L4:{levels[4]},L5:{levels[5]} abandoned={abandoned} building={building} declining={declining} " +
				$"corners={corners} rooms={guests}/{rooms} storage={storage} avgRent={avgRent} avgLV={(lots > 0 ? landSum / lots : 0)} avgCond={avgCondition} payers={(PaymentsActive ? 1 : 0)} byZone=[{zoneParts.ToString().TrimEnd()}] shapes=[{sb.ToString().TrimEnd()}] hash={StateHash:X8}";
		}
	}
}
