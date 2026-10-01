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
using System.Linq;

namespace OpenRA.Mods.City.Traits
{
	// Sewage, writing results to the buildings, monthly trade and upkeep settlement.
	public sealed partial class UtilityNetwork
	{
		int[] sewageLeft = [];
		long importPowerUnits, exportPowerUnits, importWaterUnits, exportWaterUnits, exportSewageUnits;
		int lastDay = -1;
		CityClock clock;
		bool clockCached;

		// Consumers produce sewage equal to the water they actually received. Sinks (outlets, treatment plants) and map-edge
		// export take it; when a component's sinks are full the consumers at the end of ActorID order get HasSewage = false.
		void SolveSewage()
		{
			var comps = sewerComps;
			graph.Clear();
			graph.AddNodes(2 + comps);
			Ensure(comps);
			if (sewageLeft.Length < comps)
				sewageLeft = new int[Math.Max(comps, sewageLeft.Length * 2)];

			Array.Clear(demand, 0, comps);
			Array.Fill(demandEdge, -1, 0, comps);
			Array.Fill(importEdge, -1, 0, comps);
			var sinkCap = new int[comps];
			var plantCap = new int[comps];
			var sewerTreated = new int[comps];

			var produced = 0;
			foreach (var n in nodes)
			{
				n.SewageUse = n.Operational && n.WaterUse > 0 ? n.WaterUse * n.WaterPct / 100 : 0;
				if (n.SewageUse > 0)
				{
					produced += n.SewageUse;
					if (n.Sewer >= 0)
						demand[n.Sewer] += n.SewageUse;
				}
			}

			var capacity = 0;
			foreach (var n in nodes)
			{
				if (n.Prod != null && n.Operational && n.Prod.Info.Sewage > 0 && n.Sewer >= 0)
				{
					sinkCap[n.Sewer] += n.Prod.Info.Sewage;
					plantCap[n.Sewer] += n.Prod.Info.Sewage;
					capacity += n.Prod.Info.Sewage;
				}
			}

			// septic allowance for every district that produces sewage
			for (var c = 0; c < comps; c++)
			{
				if (demand[c] > 0 && Info.SepticCapacity > 0)
				{
					sinkCap[c] += Info.SepticCapacity;
					capacity += Info.SepticCapacity;
				}
			}

			SewageProduced = produced;
			SewageCapacity = capacity;

			var treatEdge = new int[comps];
			for (var c = 0; c < comps; c++)
			{
				treatEdge[c] = -1;
				if (demand[c] <= 0)
					continue;

				graph.AddEdge(Source, 2 + c, demand[c]);
				if (sinkCap[c] > 0)
					treatEdge[c] = graph.AddEdge(2 + c, Sink, sinkCap[c]);
			}

			graph.MaxFlow(Source, Sink);
			var exports = 0;
			var exportEdge = new int[comps];
			for (var c = 0; c < comps; c++)
			{
				exportEdge[c] = -1;
				if (demand[c] > 0 && sewerEdge[c] > 0)
					exportEdge[c] = graph.AddEdge(2 + c, Sink, Info.ExportSewageCapacity * Math.Min(sewerEdge[c], Info.MaxEdgeConnections));
			}

			graph.MaxFlow(Source, Sink);
			for (var c = 0; c < comps; c++)
			{
				var handled = 0;
				if (treatEdge[c] >= 0)
					handled += graph.Flow(treatEdge[c]);

				if (exportEdge[c] >= 0)
				{
					var f = graph.Flow(exportEdge[c]);
					handled += f;
					exports += f;
				}

				sewageLeft[c] = handled;
				sewerTreated[c] = treatEdge[c] >= 0 ? graph.Flow(treatEdge[c]) * plantCap[c] / Math.Max(1, sinkCap[c]) : 0;
			}

			foreach (var n in nodes)
			{
				n.ReturnWater = 0;
				if (n.Prod != null && n.Operational && n.Sewer >= 0 && n.Prod.Info.Sewage > 0 && n.Prod.Info.SewageReturnPercent > 0 && plantCap[n.Sewer] > 0)
					n.ReturnWater = sewerTreated[n.Sewer] * n.Prod.Info.Sewage / plantCap[n.Sewer] * n.Prod.Info.SewageReturnPercent / 100;
			}

			SewageExported = exports;

			// Consumers in ActorID order: the first ones fit, the tail is backed up.
			foreach (var n in nodes)
			{
				if (n.SewageUse <= 0)
				{
					n.Sewage = true;
					continue;
				}

				if (n.Sewer < 0)
				{
					n.Sewage = false;
					continue;
				}

				if (n.SewageUse <= sewageLeft[n.Sewer])
				{
					sewageLeft[n.Sewer] -= n.SewageUse;
					n.Sewage = true;
				}
				else
					n.Sewage = false;
			}
		}

		void WriteResults()
		{
			var dark = 0;
			var dry = 0;
			var backed = 0;
			unchecked
			{
				var h = 17;
				foreach (var n in nodes)
				{
					var power = n.PowerUse <= 0 || n.PowerPct >= Info.SuppliedPercent;
					var water = n.WaterUse <= 0 || n.WaterPct >= Info.SuppliedPercent;
					n.Cb.HasPower = power;
					n.Cb.HasWater = water;
					if (n.Operational)
					{
						if (!power)
							dark++;

						if (!water)
							dry++;

						if (!n.Sewage)
							backed++;
					}

					h = h * 31 + (int)n.Id;
					h = h * 31 + (n.PowerPct | (n.WaterPct << 8) | (n.Sewage ? 1 << 20 : 0));
				}

				h = h * 31 + BatteryCharge;
				h = h * 31 + PowerProduced;
				h = h * 31 + PowerConsumed;
				h = h * 31 + WaterProduced;
				h = h * 31 + WaterConsumed;
				h = h * 31 + PowerImported + PowerExported * 7 + WaterImported * 13 + WaterExported * 17 + SewageExported * 19;
				if (h != StateHash)
				{
					StateHash = h;
					Version++;
				}
			}

			UnpoweredBuildings = dark;
			DryBuildings = dry;
			SewageBackedUp = backed;

			importPowerUnits += PowerImported;
			exportPowerUnits += PowerExported;
			importWaterUnits += WaterImported;
			exportWaterUnits += WaterExported;
			exportSewageUnits += SewageExported;
		}

		// Once per calendar month: pays imports and upkeep of lines and pipes, books export income.
		void SettleMonthly()
		{
			if (!clockCached)
			{
				clockCached = true;
				clock = world.WorldActor.TraitOrDefault<CityClock>();
			}

			if (clock == null)
				return;

			var day = clock.DayIndex;
			if (lastDay < 0)
			{
				lastDay = day;
				return;
			}

			if (day == lastDay)
				return;

			lastDay = day;
			var pulses = Math.Max(1, clock.TicksPerDay / Math.Max(1, Info.SolveInterval));
			var cost = (int)((importPowerUnits * Info.ImportPowerPrice + importWaterUnits * Info.ImportWaterPrice) / pulses);
			var income = (int)((exportPowerUnits * Info.ExportPowerPrice + exportWaterUnits * Info.ExportWaterPrice
				+ exportSewageUnits * Info.ExportSewagePrice) / pulses);
			importPowerUnits = exportPowerUnits = importWaterUnits = exportWaterUnits = exportSewageUnits = 0;
			LastMonthTradeCost = cost;
			LastMonthTradeIncome = income;

			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				var cm = p.PlayerActor.TraitOrDefault<CityManager>();
				if (cm == null)
					continue;

				if (income > 0)
					cm.AddFunds(income, "trade");

				if (cost > 0)
					cm.TrySpend(cost, "trade");

				var upkeep = MonthlyUpkeep;
				if (upkeep > 0)
					cm.TrySpend(upkeep, "utility-upkeep");

				break;
			}
		}

		/// <summary>Import cost paid and export income booked at the last month change (dollars).</summary>
		public int LastMonthTradeCost { get; private set; }

		public int LastMonthTradeIncome { get; private set; }

		/// <summary>Number of components per network, for tools and tests: LV, HV, water, sewer.</summary>
		public (int Lv, int Hv, int Water, int Sewer) ComponentCounts => (lvComps, hvComps, waterComps, sewerComps);

		/// <summary>Component index (>= 0) of the LV / HV / water / sewer network at a cell, or -1.</summary>
		public int ComponentAt(CPos cell, int network)
		{
			if (!map.Contains(cell))
				return -1;

			return network switch
			{
				0 => lvLabel[cell],
				1 => hvLabel[cell],
				2 => waterLabel[cell],
				_ => sewerLabel[cell],
			};
		}

		/// <summary>Sorted list of actor ids that currently have no power, for tests and info panels.</summary>
		public IReadOnlyList<uint> UnpoweredIds()
		{
			return nodes.Where(n => n.Operational && n.PowerUse > 0 && n.PowerPct < Info.SuppliedPercent).Select(n => n.Id).ToList();
		}
	}
}
