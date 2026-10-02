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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Result of planning a road add-on drag (trees, barriers, lights, parking, bus and bike lanes). Shared by order and preview.</summary>
	public sealed class RoadAddonPlan
	{
		public readonly List<CPos> Path;

		/// <summary>Road cells that will gain (or lose) the add-on.</summary>
		public readonly List<CPos> Cells = [];

		/// <summary>Cells of the drag that are skipped (no road, not allowed, already there / nothing to remove).</summary>
		public readonly List<CPos> Skipped = [];

		public RoadAddons Flag;
		public bool Remove;

		/// <summary>Net price (negative = refund).</summary>
		public int Cost;

		public string ErrorKey;

		public RoadAddonPlan(List<CPos> path) { Path = path; }
	}

	public static class RoadAddonPlanner
	{
		public static RoadAddonPlan Plan(World world, RoadLayer roads, CityManager cm, CPos from, CPos to, RoadAddons flag, bool remove)
		{
			var plan = new RoadAddonPlan(CityUtils.RoadPath(from, to)) { Flag = flag, Remove = remove };
			var data = roads.GetAddonData(flag);
			if (data == null)
			{
				plan.ErrorKey = ConstructionUtils.ErrorNotPlaceable;
				return plan;
			}

			int? funds = cm == null || cm.UnlimitedMoney ? null : cm.Funds;
			var spent = 0;
			var notAllowed = false;
			foreach (var c in plan.Path)
			{
				if (!roads.IsRoad(c))
				{
					plan.Skipped.Add(c);
					continue;
				}

				var has = (roads.GetAddons(c) & flag) != 0;
				if (remove)
				{
					if (has)
					{
						plan.Cells.Add(c);
						spent -= data.Info.Cost * roads.Info.RefundPercent / 100;
					}
					else
						plan.Skipped.Add(c);

					continue;
				}

				if ((roads.AllowedAddons(c) & flag) == 0)
				{
					notAllowed = true;
					plan.Skipped.Add(c);
					continue;
				}

				if (has)
				{
					plan.Skipped.Add(c);
					continue;
				}

				if (!roads.IsCellOwned(c))
				{
					plan.ErrorKey ??= ConstructionUtils.ErrorNotOwned;
					plan.Skipped.Add(c);
					continue;
				}

				if (funds.HasValue && (long)spent + data.Info.Cost > funds.Value)
				{
					plan.ErrorKey ??= ConstructionUtils.ErrorMoney;
					plan.Skipped.Add(c);
					continue;
				}

				spent += data.Info.Cost;
				plan.Cells.Add(c);
			}

			if (plan.Cells.Count == 0 && notAllowed)
				plan.ErrorKey ??= ConstructionUtils.ErrorAddonNotAllowed;

			plan.Cost = spent;
			return plan;
		}
	}
}
