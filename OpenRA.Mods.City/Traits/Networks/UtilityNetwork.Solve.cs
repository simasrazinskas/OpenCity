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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	// The solver. Integer maths only; nodes and edges are created in ActorID / component order.
	public sealed partial class UtilityNetwork
	{
		const int Inf = 1 << 28;
		const int Source = 0;
		const int Sink = 1;

		readonly FlowGraph graph = new();
		readonly System.Collections.Generic.List<UtilityProducer> batteries = [];
		readonly System.Collections.Generic.List<int> batDischarge = [];
		readonly System.Collections.Generic.List<int> batCharge = [];
		int[] demand = [];
		int[] demandEdge = [];
		int[] importEdge = [];
		int[] supplyPct = [];

		// Same occupancy and use rules as the legacy city-wide pool (CityManager), so numbers stay comparable.
		static int Occupancy(CityBuilding b)
		{
			if (b.Category == ZoneCategory.Residential)
				return Math.Min(100, b.Residents * 100 / Math.Max(1, b.Info.MaxResidents));

			if (b.Info.MaxJobs > 0)
				return Math.Min(100, b.Workers * 100 / b.Info.MaxJobs);

			return 100;
		}

		static int Use(int baseUse, int occupancyPercent)
		{
			return baseUse <= 0 ? 0 : Math.Max(1, (baseUse * (25 + 75 * occupancyPercent / 100) + 99) / 100);
		}

		CityClimate climate;
		ServiceSimulation services;
		bool providersCached;

		// Base output plus service upgrades (incinerator, ...), scaled by sun and wind for solar and wind plants.
		int ProducerPower(UNode n)
		{
			if (n.Prod == null)
				return 0;

			var output = n.Prod.Info.Power + (services?.GetExtraPower(n.Actor) ?? 0);
			if (output <= 0 || climate == null)
				return Math.Max(0, output);

			var name = n.Actor.Info.Name;
			if (Info.SolarActors.Contains(name))
				return output * climate.SolarPercent / 100;

			if (Info.WindActors.Contains(name))
				return output * (40 + 20 * Math.Clamp(climate.WindSpeed, 0, 3)) / 100;

			return output;
		}

		void ITick.Tick(Actor self)
		{
			var interval = Math.Max(1, Info.SolveInterval);
			if (world.WorldTick % interval != 0)
				return;

			Solve();
			SettleMonthly();
		}

		/// <summary>Runs one full solve now (synced callers only; the world trait calls it every pulse).</summary>
		public void Solve()
		{
			var sig = RefreshBuildings();
			EnsureTopology(sig);

			if (!providersCached)
			{
				providersCached = true;
				climate = world.WorldActor.TraitOrDefault<CityClimate>();
				foreach (var p in world.Players)
				{
					if (!p.Playable)
						continue;

					services = p.PlayerActor.TraitOrDefault<ServiceSimulation>();
					break;
				}
			}

			var demandPct = climate?.PowerDemandPercent ?? 100;
			var progression = Roads?.Progression;
			foreach (var n in nodes)
			{
				n.Operational = n.Cb.IsOperational;
				var occ = Occupancy(n.Cb);
				n.PowerUse = Use(n.Cb.Info.PowerUse, occ);
				if (demandPct != 100 && n.PowerUse > 0)
					n.PowerUse = (n.PowerUse * demandPct + 50) / 100;

				n.WaterUse = Use(n.Cb.Info.WaterUse, occ);

				// Policies (PowerUsePct / WaterUsePct, signed percent, per district): power saving, water saving, ...
				if (progression != null)
				{
					var cell = new CPos(n.X0, n.Y0);
					var pp = progression.GetPolicy("PowerUsePct", cell);
					if (pp != 0 && n.PowerUse > 0)
						n.PowerUse = Math.Max(1, n.PowerUse * Math.Max(0, 100 + pp) / 100);

					var wp = progression.GetPolicy("WaterUsePct", cell);
					if (wp != 0 && n.WaterUse > 0)
						n.WaterUse = Math.Max(1, n.WaterUse * Math.Max(0, 100 + wp) / 100);
				}

				n.PowerOut = ProducerPower(n);
			}

			SolvePower();
			SolveWater();
			SolveSewage();
			WriteResults();
		}

		static int LevelPct(int flow, int dem) { return dem <= 0 ? 100 : (int)Math.Min(100L, flow * 100L / dem); }

		// ---- electricity: LV components, HV components, transformers, map-edge trade ----
		void SolvePower()
		{
			var comps = lvComps + hvComps;
			graph.Clear();
			graph.AddNodes(2 + comps);
			Ensure(comps);
			Array.Clear(demand, 0, comps);
			Array.Fill(demandEdge, -1, 0, comps);
			Array.Fill(importEdge, -1, 0, comps);

			int Comp(UNode n) => n.Lv >= 0 ? n.Lv : n.Hv >= 0 ? lvComps + n.Hv : -1;

			var consumed = 0;
			foreach (var n in nodes)
			{
				var c = Comp(n);
				n.PowerFlowComp = c;
				if (n.PowerUse > 0 && n.Operational)
				{
					consumed += n.PowerUse;
					if (c >= 0)
						demand[c] += n.PowerUse;
				}
			}

			PowerConsumed = consumed;
			for (var c = 0; c < comps; c++)
				if (demand[c] > 0)
					demandEdge[c] = graph.AddEdge(2 + c, Sink, demand[c]);

			// producers: one node each, may feed an LV and an HV component
			var produced = 0;
			var prodEdges = new System.Collections.Generic.List<int>();
			batteries.Clear();
			batDischarge.Clear();
			batCharge.Clear();
			foreach (var n in nodes)
			{
				if (n.Prod == null || !n.Operational)
					continue;

				var info = n.Prod.Info;
				if (n.PowerOut > 0 && (n.Lv >= 0 || n.Hv >= 0))
				{
					var p = graph.AddNode();
					prodEdges.Add(graph.AddEdge(Source, p, n.PowerOut));
					if (n.Lv >= 0)
						graph.AddEdge(p, 2 + n.Lv, Inf);

					if (n.Hv >= 0)
						graph.AddEdge(p, 2 + lvComps + n.Hv, Inf);

					produced += n.PowerOut;
				}

				if (info.Transformer > 0 && n.Lv >= 0 && n.Hv >= 0)
					graph.AddEdge(2 + n.Lv, 2 + lvComps + n.Hv, info.Transformer, info.Transformer);

				if (info.BatteryCapacity > 0 && (n.Lv >= 0 || n.Hv >= 0))
				{
					var comp = n.Lv >= 0 ? n.Lv : lvComps + n.Hv;
					var p = graph.AddNode();
					batteries.Add(n.Prod);
					batDischarge.Add(graph.AddEdge(Source, p, 0));
					graph.AddEdge(p, 2 + comp, Inf);
					batCharge.Add(graph.AddEdge(2 + comp, Sink, 0));
				}
			}

			PowerProduced = produced;
			graph.MaxFlow(Source, Sink);

			// batteries cover what the plants could not, before anything is imported
			for (var i = 0; i < batteries.Count; i++)
				graph.SetCapacity(batDischarge[i], Math.Min(batteries[i].Info.BatteryRate, batteries[i].Charge));

			if (batteries.Count > 0)
				graph.MaxFlow(Source, Sink);

			// imports fill what the local plants could not (only through map-edge cells)
			var imports = 0;
			for (var c = 0; c < comps; c++)
			{
				var edges = c < lvComps ? lvEdge[c] : hvEdge[c - lvComps];
				if (edges > 0)
					importEdge[c] = graph.AddEdge(Source, 2 + c, Info.ImportPowerCapacity * Math.Min(edges, Info.MaxEdgeConnections));
			}

			graph.MaxFlow(Source, Sink);
			for (var c = 0; c < comps; c++)
			{
				if (importEdge[c] < 0)
					continue;

				var f = graph.Flow(importEdge[c]);
				imports += f;
				graph.SetCapacity(importEdge[c], f);
			}

			// surplus charges the batteries first
			if (batteries.Count > 0)
			{
				for (var i = 0; i < batteries.Count; i++)
				{
					graph.SetCapacity(batDischarge[i], graph.Flow(batDischarge[i]));
					graph.SetCapacity(batCharge[i], Math.Min(batteries[i].Info.BatteryRate, batteries[i].Info.BatteryCapacity - batteries[i].Charge));
				}

				graph.MaxFlow(Source, Sink);
				var stored = 0;
				for (var i = 0; i < batteries.Count; i++)
				{
					graph.SetCapacity(batCharge[i], graph.Flow(batCharge[i]));
					batteries[i].Charge = Math.Clamp(batteries[i].Charge + graph.Flow(batCharge[i]) - graph.Flow(batDischarge[i]), 0, batteries[i].Info.BatteryCapacity);
					stored += batteries[i].Charge;
				}

				BatteryCharge = stored;
			}
			else
				BatteryCharge = 0;

			// surplus leaves the map through the same connections
			var exports = 0;
			var exportEdges = new System.Collections.Generic.List<int>();
			for (var c = 0; c < comps; c++)
			{
				var edges = c < lvComps ? lvEdge[c] : hvEdge[c - lvComps];
				if (edges > 0)
					exportEdges.Add(graph.AddEdge(2 + c, Sink, Info.ExportPowerCapacity * Math.Min(edges, Info.MaxEdgeConnections)));
			}

			graph.MaxFlow(Source, Sink);
			foreach (var e in exportEdges)
				exports += graph.Flow(e);

			var used = 0;
			foreach (var e in prodEdges)
				used += graph.Flow(e);

			for (var c = 0; c < comps; c++)
				supplyPct[c] = demandEdge[c] < 0 ? 100 : LevelPct(graph.Flow(demandEdge[c]), demand[c]);

			PowerImported = imports;
			PowerExported = exports;
			PowerLost = Math.Max(0, produced - used);

			foreach (var n in nodes)
				n.PowerPct = n.PowerFlowComp < 0 ? 0 : supplyPct[n.PowerFlowComp];
		}

		void Ensure(int count)
		{
			if (demand.Length < count)
			{
				var size = Math.Max(count, demand.Length * 2);
				demand = new int[size];
				demandEdge = new int[size];
				importEdge = new int[size];
				supplyPct = new int[size];
			}
		}

		// ---- water: pipe components, pumps and towers, map-edge trade ----
		void SolveWater()
		{
			var comps = waterComps;
			graph.Clear();
			graph.AddNodes(2 + comps);
			Ensure(comps);
			Array.Clear(demand, 0, comps);
			Array.Fill(demandEdge, -1, 0, comps);
			Array.Fill(importEdge, -1, 0, comps);

			var consumed = 0;
			foreach (var n in nodes)
			{
				n.WaterFlowComp = n.Water;
				if (n.WaterUse > 0 && n.Operational)
				{
					consumed += n.WaterUse;
					if (n.Water >= 0)
						demand[n.Water] += n.WaterUse;
				}
			}

			WaterConsumed = consumed;
			for (var c = 0; c < comps; c++)
				if (demand[c] > 0)
					demandEdge[c] = graph.AddEdge(2 + c, Sink, demand[c]);

			var produced = 0;
			foreach (var n in nodes)
			{
				if (n.Prod == null || !n.Operational || n.Water < 0)
					continue;

				// treatment plants give back part of the sewage they cleaned in the previous solve
				var output = n.Prod.Info.Water > 0 ? n.Prod.Info.Water * (100 - PollutionLoss(n)) / 100 : 0;
				output += n.ReturnWater;
				if (output <= 0)
					continue;

				graph.AddEdge(Source, 2 + n.Water, output);
				produced += output;
			}

			WaterProduced = produced;
			graph.MaxFlow(Source, Sink);

			var imports = 0;
			for (var c = 0; c < comps; c++)
				if (waterEdge[c] > 0)
					importEdge[c] = graph.AddEdge(Source, 2 + c, Info.ImportWaterCapacity * Math.Min(waterEdge[c], Info.MaxEdgeConnections));

			graph.MaxFlow(Source, Sink);
			for (var c = 0; c < comps; c++)
			{
				if (importEdge[c] < 0)
					continue;

				var f = graph.Flow(importEdge[c]);
				imports += f;
				graph.SetCapacity(importEdge[c], f);
			}

			var exports = 0;
			var exportEdges = new System.Collections.Generic.List<int>();
			for (var c = 0; c < comps; c++)
				if (waterEdge[c] > 0)
					exportEdges.Add(graph.AddEdge(2 + c, Sink, Info.ExportWaterCapacity * Math.Min(waterEdge[c], Info.MaxEdgeConnections)));

			graph.MaxFlow(Source, Sink);
			foreach (var e in exportEdges)
				exports += graph.Flow(e);

			for (var c = 0; c < comps; c++)
				supplyPct[c] = demandEdge[c] < 0 ? 100 : LevelPct(graph.Flow(demandEdge[c]), demand[c]);

			WaterImported = imports;
			WaterExported = exports;
			foreach (var n in nodes)
				n.WaterPct = n.Water < 0 ? 0 : supplyPct[n.Water];
		}

		// Percent of output a pump loses because an operational outlet is within its PollutedRadius.
		int PollutionLoss(UNode pump)
		{
			foreach (var o in nodes)
			{
				if (o.Prod == null || !o.Operational || o.Prod.Info.PollutedRadius <= 0 || o.Prod.Info.Sewage <= 0)
					continue;

				var r = o.Prod.Info.PollutedRadius;
				var dx = Math.Max(0, Math.Max(pump.X0 - o.X1, o.X0 - pump.X1));
				var dy = Math.Max(0, Math.Max(pump.Y0 - o.Y1, o.Y0 - pump.Y1));
				if (Math.Max(dx, dy) <= r)
					return o.Prod.Info.PollutedLoss;
			}

			return 0;
		}
	}
}
