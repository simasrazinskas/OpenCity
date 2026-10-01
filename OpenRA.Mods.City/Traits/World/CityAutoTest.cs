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
using System.Globalization;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Developer harness: when the OPENCITY_AUTOTEST environment variable is set, builds a test city through",
		"real player orders, logs city stats, takes screenshots and exits. Does nothing otherwise.",
		"OPENCITY_AUTOTEST format: semicolon-separated key=value pairs, e.g.",
		"'ticks=3000;shots=100,1500,3000;timestep=5;log=250;scenario=basic'.")]
	public class CityAutoTestInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new CityAutoTest(); }
	}

	public class CityAutoTest : IWorldLoaded, ITick
	{
		bool enabled;
		int endTick = 3000;
		int logInterval = 250;
		int timestep = 0;
		string scenario = "basic";
		float shotZoom;
		string probe;
		CityInfoView shotView;
		(int U, int V) shotCenter = (14, 0);
		readonly HashSet<int> shotTicks = [];
		WorldRenderer worldRenderer;
		CPos anchor;
		bool issued;
		bool menuOnly;
		readonly List<(int Tick, string Name, Action<World, Player> Run)> scheduled = [];
		bool reportedGameOver;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var env = Environment.GetEnvironmentVariable("OPENCITY_AUTOTEST");
			if (string.IsNullOrEmpty(env) || w.Type == WorldType.Editor)
				return;

			// Shellmap (main menu) worlds only take screenshots: autotest.sh passes "menu=1" when run without a map.
			menuOnly = w.Type == WorldType.Shellmap;
			if (menuOnly != env.Contains("menu=1", StringComparison.Ordinal))
				return;

			enabled = true;
			worldRenderer = wr;
			foreach (var kv in env.Split(';', StringSplitOptions.RemoveEmptyEntries))
			{
				var parts = kv.Split('=', 2);
				if (parts.Length != 2)
					continue;

				switch (parts[0].Trim())
				{
					case "ticks": endTick = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "log": logInterval = Math.Max(1, int.Parse(parts[1], CultureInfo.InvariantCulture)); break;
					case "timestep": timestep = int.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "scenario": scenario = parts[1].Trim(); break;
					case "zoom": shotZoom = float.Parse(parts[1], CultureInfo.InvariantCulture); break;
					case "probe": probe = parts[1].Trim(); break;
					case "center":
						var uv = parts[1].Split(',');
						shotCenter = (int.Parse(uv[0], CultureInfo.InvariantCulture), int.Parse(uv[1], CultureInfo.InvariantCulture));
						break;
					case "view": shotView = Enum.Parse<CityInfoView>(parts[1].Trim(), true); break;
					case "shots":
						foreach (var s in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
							shotTicks.Add(int.Parse(s, CultureInfo.InvariantCulture));
						break;
				}
			}

			var connection = w.ActorsWithTrait<OutsideConnection>().FirstOrDefault();
			if (connection.Actor != null)
				anchor = connection.Actor.Location + connection.Trait.Info.Direction * (connection.Trait.Info.Length - 1);
			else
				anchor = new MPos(w.Map.Bounds.Left + w.Map.Bounds.Width / 2, w.Map.Bounds.Top + w.Map.Bounds.Height / 2).ToCPos(w.Map);

			Report(w, $"autotest enabled: scenario={scenario} ticks={endTick} anchor={anchor}");
		}

		void ITick.Tick(Actor self)
		{
			if (!enabled)
				return;

			var w = self.World;
			var tick = w.WorldTick;

			if (timestep > 0 && w.Timestep != timestep)
				w.Timestep = timestep;

			// Replay playback: only log stats (compare them with the original run to verify determinism).
			if (w.IsReplay)
			{
				if (timestep > 0)
					w.ReplayTimestep = timestep;

				if (w.IsGameOver && !reportedGameOver)
				{
					reportedGameOver = true;
					Report(w, "REPLAY ENDED EARLY OR WENT OUT OF SYNC");
				}

				if (tick % logInterval == 0)
					LogStats(w);

				if (tick >= endTick)
				{
					enabled = false;
					Report(w, "autotest (replay) finished");
					Game.RunAfterDelay(500, Game.Exit);
				}

				return;
			}

			if (menuOnly)
			{
				if (shotTicks.Contains(tick))
					Game.RunAfterTick(Game.TakeScreenshot);

				if (tick >= endTick)
				{
					enabled = false;
					Report(w, "autotest (menu) finished");
					Game.RunAfterDelay(500, Game.Exit);
				}

				return;
			}

			if (!issued && tick >= 5 && w.LocalPlayer != null)
			{
				issued = true;
				IssueScenario(w, w.LocalPlayer);
			}

			// Scheduled steps of the "full" scenario (issued once their tick is reached).
			for (var i = 0; i < scheduled.Count; i++)
			{
				if (scheduled[i].Tick > tick || w.LocalPlayer == null)
					continue;

				var (_, name, run) = scheduled[i];
				scheduled.RemoveAt(i--);
				run(w, w.LocalPlayer);
				Report(w, $"scenario step '{name}' issued");
			}

			if (tick % logInterval == 0)
				LogStats(w);

			if (shotTicks.Contains(tick))
			{
				var viewport = worldRenderer.Viewport;
				if (shotZoom > 0)
					viewport.AdjustZoom((float)Math.Log(shotZoom / viewport.Zoom));

				viewport.Center(w.Map.CenterOfCell(At(shotCenter.U, shotCenter.V)));
				Game.RunAfterTick(() =>
				{
					// Order generators may only be swapped outside synced code.
					if (probe != null)
						Sync.RunUnsynced(w, () => ActivateProbe(w));

					var infoView = w.WorldActor.TraitOrDefault<InfoViewLayer>();
					if (infoView != null)
						infoView.Mode = shotView;

					Game.TakeScreenshot();
				});
				Report(w, $"screenshot requested at tick {tick}");
			}

			if (tick >= endTick)
			{
				enabled = false;
				LogStats(w);
				Report(w, "autotest finished");
				Game.RunAfterDelay(500, Game.Exit);
			}
		}

		/// <summary>Activates a placement tool with the pointer over a fixed cell, so screenshots show whether its preview lines up at any zoom.</summary>
		void ActivateProbe(World w)
		{
			w.OrderGenerator = probe == "bulldoze" ? new BulldozeOrderGenerator(w) : new PlaceCityBuildingOrderGenerator(w, probe);
			var target = w.Map.CenterOfCell(At(18, 3));
			Viewport.LastMousePos = worldRenderer.Viewport.WorldToViewPx(worldRenderer.ScreenPxPosition(target));
			Report(w, $"probe '{probe}' at {At(18, 3)} zoom={worldRenderer.Viewport.Zoom:0.00} mouse={Viewport.LastMousePos}");
		}

		// Scenario layout assumes the outside connection road runs east (Direction 1,0); (u, v) = (east, south) offsets from its end.
		CPos At(int u, int v) => anchor + new CVec(u, v);

		void IssueScenario(World w, Player p)
		{
			var orders = new List<Order>
			{
				// Main avenue and a street grid.
				CityOrders.BuildRoadOrder(p, At(0, 0), At(34, 0)),
				CityOrders.BuildRoadOrder(p, At(6, -9), At(6, 9)),
				CityOrders.BuildRoadOrder(p, At(13, -9), At(13, 9)),
				CityOrders.BuildRoadOrder(p, At(20, -9), At(20, 9)),
				CityOrders.BuildRoadOrder(p, At(27, -9), At(27, 9)),
				CityOrders.BuildRoadOrder(p, At(6, -9), At(27, -9)),
				CityOrders.BuildRoadOrder(p, At(6, 9), At(27, 9)),
				CityOrders.BuildRoadOrder(p, At(6, 5), At(27, 5)),

				// Zones.
				CityOrders.ZoneOrder(p, At(7, -8), At(12, -1), ZoneType.ResidentialLow),
				CityOrders.ZoneOrder(p, At(14, -8), At(19, -1), ZoneType.ResidentialHigh),
				CityOrders.ZoneOrder(p, At(21, -8), At(26, -1), ZoneType.Office),
				CityOrders.ZoneOrder(p, At(7, 1), At(12, 4), ZoneType.CommercialLow),
				CityOrders.ZoneOrder(p, At(14, 1), At(19, 4), ZoneType.CommercialHigh),
				CityOrders.ZoneOrder(p, At(21, 1), At(26, 8), ZoneType.Industrial),
				CityOrders.ZoneOrder(p, At(7, 6), At(19, 8), ZoneType.ResidentialLow),

				// Utilities and services along the avenue (north side: v = -2..-1, south side: v = 1..2).
				CityOrders.PlaceBuildingOrder(p, "police", At(1, -2)),
				CityOrders.PlaceBuildingOrder(p, "firestation", At(3, -2)),
				CityOrders.PlaceBuildingOrder(p, "clinic", At(29, -2)),
				CityOrders.PlaceBuildingOrder(p, "school", At(31, -2)),
				CityOrders.PlaceBuildingOrder(p, "park-large", At(33, -2)),
				CityOrders.PlaceBuildingOrder(p, "powerplant-coal", At(29, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(31, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(32, 1)),
				CityOrders.PlaceBuildingOrder(p, "windturbine", At(33, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(34, 1)),
				CityOrders.PlaceBuildingOrder(p, "watertower", At(33, -1)),
				CityOrders.PlaceBuildingOrder(p, "park-small", At(1, 1)),
				CityOrders.PlaceBuildingOrder(p, "plaza", At(2, 1)),
				CityOrders.SetSpeedOrder(p, 3),
			};

			if (scenario == "full")
				ScheduleFullScenario();

			if (scenario == "stress")
			{
				// Test-only grant (runs in synced tick code, so it is still deterministic).
				p.PlayerActor.TraitOrDefault<CityManager>()?.AddFunds(2000000, "test");
				AddStressOrders(w, p, orders);
			}

			if (scenario == "bulldoze")
			{
				orders.Add(CityOrders.BulldozeOrder(p, At(13, -9), At(13, -9)));
				orders.Add(CityOrders.ZoneOrder(p, At(7, 6), At(9, 8), ZoneType.None));
			}

			foreach (var o in orders)
				w.IssueOrder(o);

			Report(w, $"issued {orders.Count} scenario orders");
		}

		// Exercises every system on top of "basic": extra utilities, an extended avenue, a farm with its field area, a landfill,
		// a bus depot and a bus line, then schools/hospital once progression unlocks them (locked orders are simply refused).
		void ScheduleFullScenario()
		{
			scheduled.Add((5, "utilities", FullUtilities));
			scheduled.Add((300, "industry-and-depots", FullIndustryAndDepots));
			scheduled.Add((600, "farm-area", FullFarmArea));
			scheduled.Add((900, "bus-stops", FullBusStops));
			scheduled.Add((950, "bus-line", FullBusLine));
			foreach (var t in new[] { 4800, 9600 })
			{
				scheduled.Add((t, "farm-retry", FullFarmRetry));
				scheduled.Add((t + 300, "farm-area-retry", FullFarmArea));
			}

			scheduled.Add((4800, "education-health", FullEducationHealth));
			scheduled.Add((9600, "hospital", FullHospital));
		}

		void FullUtilities(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "watertower", At(34, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "windturbine", At(5, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "windturbine", At(5, -1)));
		}

		void FullIndustryAndDepots(World w, Player p)
		{
			w.IssueOrder(CityOrders.BuildRoadOrder(p, At(34, 0), At(48, 0)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "farm-hub", At(38, 1)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "landfill", At(42, -3)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "busdepot", At(44, 1)));
		}

		void FullFarmRetry(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "farm-hub", At(38, 1)));
		}

		void FullFarmArea(World w, Player p)
		{
			var hub = w.Actors.FirstOrDefault(a => a.Owner == p && a.Info.Name == "farm-hub" && !a.IsDead);
			if (hub != null)
				w.IssueOrder(IndustryOrders.AreaOrder(p, hub, At(36, 4), At(44, 10), true));
		}

		void FullBusStops(World w, Player p)
		{
			foreach (var u in new[] { 3, 10, 17, 24, 31 })
				w.IssueOrder(TransitOrders.PlaceStopOrder(p, At(u, 0), TransitMode.Bus));
		}

		void FullBusLine(World w, Player p)
		{
			// Use whichever stops were actually placed (ids are assigned by the transit layer).
			var transit = w.WorldActor.TraitOrDefault<TransitLayer>();
			var ids = transit?.Stops.Select(s => s.Id).Order().ToList() ?? [];
			if (ids.Count >= 2)
				w.IssueOrder(TransitOrders.CreateLineOrder(p, TransitMode.Bus, false, ids, 0xE04040, "", 2));
		}

		void FullEducationHealth(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "school", At(31, -2)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "park-large", At(33, -2)));
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "highschool", At(46, -3)));
		}

		void FullHospital(World w, Player p)
		{
			w.IssueOrder(CityOrders.PlaceBuildingOrder(p, "hospital", At(38, -3)));
		}

		// Covers most of the map with a street grid and zones every block, to measure simulation cost at scale.
		static void AddStressOrders(World w, Player p, List<Order> orders)
		{
			var b = w.Map.Bounds;
			var tl = new MPos(b.Left + 2, b.Top + 2).ToCPos(w.Map);
			var br = new MPos(b.Right - 3, b.Bottom - 3).ToCPos(w.Map);
			const int Block = 7;
			for (var y = tl.Y; y <= br.Y; y += Block)
				orders.Add(CityOrders.BuildRoadOrder(p, new CPos(tl.X, y), new CPos(br.X, y)));

			for (var x = tl.X; x <= br.X; x += Block)
				orders.Add(CityOrders.BuildRoadOrder(p, new CPos(x, tl.Y), new CPos(x, br.Y)));

			ZoneType[] zones =
			[
				ZoneType.ResidentialLow, ZoneType.ResidentialHigh, ZoneType.CommercialLow, ZoneType.Industrial,
				ZoneType.ResidentialLow, ZoneType.CommercialHigh, ZoneType.Office, ZoneType.ResidentialHigh
			];
			var i = 0;
			for (var y = tl.Y; y + Block <= br.Y; y += Block)
				for (var x = tl.X; x + Block <= br.X; x += Block)
					orders.Add(CityOrders.ZoneOrder(p, new CPos(x + 1, y + 1), new CPos(x + Block - 1, y + Block - 1), zones[i++ % zones.Length]));

			// Utilities: a power plant and water towers along the first street.
			for (var x = tl.X + 1; x + 1 < br.X; x += 12)
			{
				orders.Add(CityOrders.PlaceBuildingOrder(p, "powerplant-coal", new CPos(x, tl.Y + 1)));
				orders.Add(CityOrders.PlaceBuildingOrder(p, "watertower", new CPos(x + 3, tl.Y + 1)));
				orders.Add(CityOrders.PlaceBuildingOrder(p, "watertower", new CPos(x + 4, tl.Y + 1)));
			}
		}

		long lastLogRunTime = -1;
		int lastLogTick;

		void LogStats(World w)
		{
			// Real-time simulation speed since the previous log line (not part of the deterministic output).
			var runTime = Game.RunTime;
			var tps = lastLogRunTime >= 0 && runTime > lastLogRunTime ? (w.WorldTick - lastLogTick) * 1000L / (runTime - lastLogRunTime) : 0;
			lastLogRunTime = runTime;
			lastLogTick = w.WorldTick;
			Console.WriteLine($"[autotest-perf t={w.WorldTick}] ticks/s={tps}");

			// Replays have no local player: report the first playable player's city.
			var p = w.LocalPlayer ?? w.Players.FirstOrDefault(pl => pl.Playable);
			var cm = p?.PlayerActor.TraitOrDefault<CityManager>();
			if (cm == null)
			{
				Report(w, "no CityManager on local player");
				return;
			}

			var growables = w.ActorsHavingTrait<GrowableBuilding>().ToList();
			var byZone = growables
				.GroupBy(a => a.Trait<GrowableBuilding>().Zone)
				.Select(g => $"{g.Key}:{g.Count()}");
			var roads = w.WorldActor.TraitOrDefault<RoadLayer>()?.RoadCellCount ?? -1;
			var traffic = w.WorldActor.TraitsImplementing<ITrafficService>().FirstOrDefault()?.ActiveVehicles ?? -1;

			Report(w, $"date={cm.Date} funds={cm.Funds} balance/mo={cm.MonthlyBalance} pop={cm.Population} workers={cm.Workers} jobs={cm.Jobs} " +
				$"unemployed={cm.Unemployed} demand R/C/I/O={cm.GetDemand(ZoneCategory.Residential)}/{cm.GetDemand(ZoneCategory.Commercial)}/" +
				$"{cm.GetDemand(ZoneCategory.Industrial)}/{cm.GetDemand(ZoneCategory.Office)} power={cm.PowerConsumed}/{cm.PowerProduced} " +
				$"water={cm.WaterConsumed}/{cm.WaterProduced} happy={cm.AverageHappiness} milestone={cm.MilestoneName} roads={roads} " +
				$"growables={growables.Count} [{string.Join(",", byZone)}] vehicles={traffic}");

			// Work-package reporters (world and player traits), in trait order: deterministic content only.
			foreach (var r in w.WorldActor.TraitsImplementing<ICityAutoTestReporter>())
				Report(w, "report " + r.AutoTestReport());

			foreach (var r in p.PlayerActor.TraitsImplementing<ICityAutoTestReporter>())
				Report(w, "report " + r.AutoTestReport());
		}

		static void Report(World w, string message)
		{
			var line = $"[autotest t={w.WorldTick}] {message}";
			Console.WriteLine(line);
			Log.Write("debug", line);
		}
	}
}
