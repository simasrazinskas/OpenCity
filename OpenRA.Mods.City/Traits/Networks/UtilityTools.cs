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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Result of planning an HV line or pipe drag. Shared by the order handler and the preview.</summary>
	public sealed class UtilityPlan
	{
		public readonly List<CPos> Path;

		/// <summary>Cells that will get (or lose) the line / pipe.</summary>
		public readonly List<CPos> Cells = [];

		/// <summary>Cells that already have it (add mode) or have nothing to remove (remove mode).</summary>
		public readonly List<CPos> Skipped = [];

		/// <summary>Index into Path of the first cell that stops the drag, Path.Count if none.</summary>
		public int StopIndex;

		/// <summary>Net price (negative = refund).</summary>
		public int Cost;

		public string ErrorKey;
		public bool Remove;

		/// <summary>0 = HV line, otherwise the pipe kind (1 water, 2 sewage, 3 both).</summary>
		public int Kind;

		public UtilityPlan(List<CPos> path)
		{
			Path = path;
			StopIndex = path.Count;
		}
	}

	public static class UtilityPlanner
	{
		/// <summary>Plans an L-path of HV line (kind 0) or pipes (kind 1..3).</summary>
		public static UtilityPlan Plan(World world, UtilityNetwork net, CityManager cm, CPos from, CPos to, int kind, bool remove, Func<CPos, bool> isPending = null)
		{
			var plan = new UtilityPlan(CityUtils.RoadPath(from, to)) { Kind = kind, Remove = remove };
			var funds = cm?.Funds ?? int.MaxValue;
			var spent = 0;
			var refundPct = net.Info.RefundPercent;

			for (var i = 0; i < plan.Path.Count; i++)
			{
				var c = plan.Path[i];
				if (!world.Map.Contains(c))
				{
					plan.StopIndex = i;
					plan.ErrorKey = ConstructionUtils.ErrorOutOfBounds;
					break;
				}

				if (remove)
				{
					var price = CellPrice(net, c, kind);
					if (Has(net, c, kind))
					{
						plan.Cells.Add(c);
						spent -= price * refundPct / 100;
					}
					else
						plan.Skipped.Add(c);

					continue;
				}

				if (Has(net, c, kind, true))
				{
					plan.Skipped.Add(c);
					continue;
				}

				if (!IsOwned(world, c))
				{
					plan.StopIndex = i;
					plan.ErrorKey = ConstructionUtils.ErrorNotOwned;
					break;
				}

				var terrainOk = kind == 0 ? net.IsLineTerrain(c) : !net.IsWaterCell(c) && IsLand(world, c);
				if (!terrainOk)
				{
					plan.StopIndex = i;
					plan.ErrorKey = ConstructionUtils.ErrorTerrain;
					break;
				}

				if (ConstructionUtils.HasBlockingActor(world, c) || (isPending != null && isPending(c)))
				{
					// a pipe runs under buildings' neighbours but not through them
					plan.StopIndex = i;
					plan.ErrorKey = ConstructionUtils.ErrorBlocked;
					break;
				}

				var cost = CellPrice(net, c, kind) + (kind == 0 ? ConstructionUtils.AutoClearCost(world, c) : 0);
				if ((long)spent + cost > funds)
				{
					plan.StopIndex = i;
					plan.ErrorKey = ConstructionUtils.ErrorMoney;
					break;
				}

				spent += cost;
				plan.Cells.Add(c);
			}

			plan.Cost = spent;
			return plan;
		}

		static bool IsOwned(World world, CPos c)
		{
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			return roads == null || roads.IsCellOwned(c);
		}

		static bool IsLand(World world, CPos c)
		{
			var roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			return roads == null || roads.IsBuildableTerrain(c);
		}

		static int CellPrice(UtilityNetwork net, CPos c, int kind)
		{
			if (kind == 0)
				return net.Info.PowerLineCost * (net.IsWaterCell(c) ? 2 : 1);

			return net.Info.PipeCost * (kind == 3 ? 2 : 1);
		}

		static bool Has(UtilityNetwork net, CPos c, int kind, bool forAdd = false)
		{
			if (kind == 0)
				return net.HasPowerLine(c);

			var bits = net.PipeBits(c);
			return forAdd ? (bits & kind) == kind : (bits & kind) != 0;
		}
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("Resolves the HV power line and pipe drag orders (NetworkOrders.BuildPowerLine / BuildPipe).")]
	public class UtilityToolsInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new UtilityTools(init.Self); }
	}

	public class UtilityTools : IResolveOrder
	{
		readonly Actor self;
		readonly HashSet<CPos> noPending = [];

		public UtilityTools(Actor self)
		{
			this.self = self;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == NetworkOrders.BuildPowerLine)
				Build(order, 0, (order.ExtraData & 1) != 0);
			else if (order.OrderString == NetworkOrders.BuildPipe)
				Build(order, (int)(order.ExtraData & 3), (order.ExtraData & 256) != 0);
		}

		void Build(Order order, int kind, bool remove)
		{
			var world = self.World;
			var net = world.WorldActor.TraitOrDefault<UtilityNetwork>();
			if (net == null || order.Target.Type == TargetType.Invalid || (order.OrderString == NetworkOrders.BuildPipe && kind == 0))
				return;

			var from = order.ExtraLocation;
			var to = world.Map.CellContaining(order.Target.CenterPosition);
			var cm = self.TraitOrDefault<CityManager>();
			var plan = UtilityPlanner.Plan(world, net, cm, from, to, kind, remove, noPending.Contains);

			if (plan.Cells.Count == 0)
			{
				if (plan.ErrorKey != null)
					ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey);

				return;
			}

			if (plan.Cost > 0 && cm != null && !cm.TrySpend(plan.Cost, "construction"))
			{
				ConstructionUtils.NotifyError(world, self.Owner, ConstructionUtils.ErrorMoney);
				return;
			}

			if (plan.Cost < 0)
				cm?.AddFunds(-plan.Cost, "refund");

			foreach (var c in plan.Cells)
			{
				if (remove)
				{
					if (kind == 0)
						net.RemovePowerLine(c);
					else
						net.RemovePipe(c, kind);
				}
				else if (kind == 0)
				{
					ConstructionUtils.ClearAutoClearActors(world, c);
					net.AddPowerLine(c);
				}
				else
					net.AddPipe(c, kind);
			}

			if (plan.ErrorKey != null)
				ConstructionUtils.NotifyError(world, self.Owner, plan.ErrorKey);
			else
				ConstructionUtils.PlaySound(world, self.Owner, ConstructionUtils.SoundRoad);
		}
	}
}
