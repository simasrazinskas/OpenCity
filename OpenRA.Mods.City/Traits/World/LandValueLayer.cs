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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Per-cell land value (0..100). A local score from services, water, shops, pollution and abandonment spreads along roads",
		"(with a fade per step) and is smoothed. Recomputed every RecomputeInterval ticks in four passes.")]
	public class LandValueLayerInfo : TraitInfo
	{
		[Desc("World ticks between recomputations; the work is spread over four consecutive ticks.")]
		public readonly int RecomputeInterval = 100;

		public readonly int BaseValue = 25;

		[Desc("Cells within this distance of water get a land value bonus.")]
		public readonly int WaterRadius = 6;

		public readonly int WaterBonus = 15;

		[Desc("Terrain type names that count as water.")]
		public readonly string[] WaterTerrainTypes = ["Water"];

		[Desc("Weights in percent of the 0..100 inputs.")]
		public readonly int ParkWeight = 30;
		public readonly int PoliceWeight = 8;
		public readonly int FireWeight = 6;
		public readonly int HealthWeight = 12;
		public readonly int EducationWeight = 12;
		public readonly int ShopWeight = 15;
		public readonly int PollutionWeight = 45;
		public readonly int NoiseWeight = 20;

		[Desc("Shops (commercial properties) count within this many cells.")]
		public readonly int ShopRadius = 6;

		[Desc("Each abandoned building within AbandonedRadius lowers the value by this much (up to AbandonedMax).")]
		public readonly int AbandonedPenalty = 6;
		public readonly int AbandonedRadius = 4;
		public readonly int AbandonedMax = 30;

		[Desc("Road propagation: value lost per road step and the maximum number of steps.")]
		public readonly int RoadFade = 4;
		public readonly int RoadSteps = 12;

		[Desc("Lot cells within LotReach cells of a road take the road value minus LotFade per cell of distance.")]
		public readonly int LotReach = 3;
		public readonly int LotFade = 5;

		[Desc("Industrial land value read by lots is capped here (industry prefers cheap land).")]
		public readonly int IndustrialCap = 60;

		[Desc("Added to the land value of high density commercial lots.")]
		public readonly int CommercialHighBonus = 10;

		public override object Create(ActorInitializer init) { return new LandValueLayer(init.Self, this); }
	}

	public class LandValueLayer : ILandValueSource, ITick, IWorldLoaded, ICityAutoTestReporter
	{
		public readonly LandValueLayerInfo Info;
		readonly World world;
		readonly int width;
		readonly int height;
		readonly byte[] water;
		readonly int[] local;
		readonly int[] prop;
		readonly byte[] abandoned;
		readonly byte[] shops;
		byte[] value;
		readonly int[] roadA;
		readonly int[] roadB;
		CPos[] roadCells = [];
		int roadVersion = -1;

		IRoadNetwork roads;
		PropertyRegistry registry;
		CityCoverageLayer coverage;
		ICityServices services;
		IPollutionMap pollution;
		IProgression progression;

		public LandValueLayer(Actor self, LandValueLayerInfo info)
		{
			Info = info;
			world = self.World;
			width = world.Map.MapSize.Width;
			height = world.Map.MapSize.Height;
			var n = width * height;
			water = new byte[n];
			local = new int[n];
			prop = new int[n];
			abandoned = new byte[n];
			shops = new byte[n];
			value = new byte[n];
			roadA = new int[n];
			roadB = new int[n];
			Array.Fill(value, (byte)info.BaseValue);
		}

		/// <summary>Incremented after each recomputation (overlay caches).</summary>
		public int Version { get; private set; }

		bool InMap(CPos cell) { return cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height; }

		public int GetLandValue(CPos cell)
		{
			return InMap(cell) ? value[cell.Y * width + cell.X] : Info.BaseValue;
		}

		public int GetLandValue(CPos cell, ZoneType zone)
		{
			var v = GetLandValue(cell);
			if (zone == ZoneType.Industrial || zone == ZoneType.Warehouse)
				v = Math.Min(v, Info.IndustrialCap);
			else if (zone == ZoneType.CommercialHigh)
				v += Info.CommercialHighBonus;

			return Math.Clamp(v, 0, 100);
		}

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			registry = w.WorldActor.TraitOrDefault<PropertyRegistry>();
			coverage = w.WorldActor.TraitOrDefault<CityCoverageLayer>();
			services = ZoningLookup.Find<ICityServices>(w);
			pollution = ZoningLookup.Find<IPollutionMap>(w);

			// Policies (LandValuePct) come from the city player's progression.
			foreach (var pl in w.Players)
				if (pl.Playable && pl.PlayerActor.TraitOrDefault<CityManager>() != null)
				{
					progression = pl.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault();
					break;
				}

			ComputeWater();
		}

		void ComputeWater()
		{
			var map = world.Map;
			var radius = Math.Max(1, Info.WaterRadius);
			foreach (var cell in map.AllCells)
			{
				var type = map.GetTerrainInfo(cell).Type;
				if (Array.IndexOf(Info.WaterTerrainTypes, type) < 0 || !InMap(cell))
					continue;

				for (var y = Math.Max(0, cell.Y - radius); y <= Math.Min(height - 1, cell.Y + radius); y++)
				{
					for (var x = Math.Max(0, cell.X - radius); x <= Math.Min(width - 1, cell.X + radius); x++)
					{
						var d16 = Exts.ISqrt(((x - cell.X) * (x - cell.X) + (y - cell.Y) * (y - cell.Y)) * 256);
						if (d16 >= radius * 16)
							continue;

						var v = (byte)(Info.WaterBonus * (radius * 16 - d16) / (radius * 16));
						var idx = y * width + x;
						if (v > water[idx])
							water[idx] = v;
					}
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			var interval = Math.Max(4, Info.RecomputeInterval);
			var phase = world.WorldTick % interval;
			if (phase > 3)
				return;

			if (phase == 0)
				GatherStamps();

			var rowsPerPhase = (height + 3) / 4;
			ComputeLocal(phase * rowsPerPhase, Math.Min(height, (phase + 1) * rowsPerPhase));

			if (phase == 3)
				Propagate();
		}

		// Abandoned buildings and shops stamp into small per-cell maps (radius falloff).
		void GatherStamps()
		{
			Array.Clear(abandoned);
			Array.Clear(shops);
			if (registry == null)
				return;

			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];
				if (p.Kind == PropertyKind.Commercial && p.Operational)
					StampLinear(p, Info.ShopRadius, 100, shops);
				else if (p.Kind != PropertyKind.Service && registry.GetGrowable(p.Id)?.Abandoned == true)
					StampCount(p, Info.AbandonedRadius, abandoned);
			}
		}

		void StampLinear(Property p, int radius, int strength, byte[] map)
		{
			var r16 = radius * 16;
			for (var y = Math.Max(0, p.Origin.Y - radius); y <= Math.Min(height - 1, p.Origin.Y + p.Depth - 1 + radius); y++)
			{
				var dy = Math.Max(Math.Max(p.Origin.Y - y, y - (p.Origin.Y + p.Depth - 1)), 0);
				for (var x = Math.Max(0, p.Origin.X - radius); x <= Math.Min(width - 1, p.Origin.X + p.Width - 1 + radius); x++)
				{
					var dx = Math.Max(Math.Max(p.Origin.X - x, x - (p.Origin.X + p.Width - 1)), 0);
					var d16 = Exts.ISqrt((dx * dx + dy * dy) * 256);
					if (d16 >= r16)
						continue;

					var v = strength * (r16 - d16) / r16;
					var idx = y * width + x;
					int old = map[idx];
					map[idx] = (byte)Math.Min(100, old + v - old * v / 100);
				}
			}
		}

		void StampCount(Property p, int radius, byte[] map)
		{
			for (var y = Math.Max(0, p.Origin.Y - radius); y <= Math.Min(height - 1, p.Origin.Y + p.Depth - 1 + radius); y++)
				for (var x = Math.Max(0, p.Origin.X - radius); x <= Math.Min(width - 1, p.Origin.X + p.Width - 1 + radius); x++)
				{
					var idx = y * width + x;
					if (map[idx] < 250)
						map[idx]++;
				}
		}

		int Cov(CityService service, ServiceKind kind, CPos cell)
		{
			if (services != null)
				return services.GetSatisfaction(kind, cell);

			return coverage != null ? coverage.GetCoverage(service, cell) : 0;
		}

		void ComputeLocal(int y0, int y1)
		{
			for (var y = y0; y < y1; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var cell = new CPos(x, y);
					var idx = y * width + x;
					var poll = 0;
					var noise = 0;
					if (pollution != null)
					{
						poll = Math.Max(pollution.GetGround(cell), pollution.GetAir(cell));
						noise = pollution.GetNoise(cell);
					}
					else if (coverage != null)
						poll = coverage.GetPollution(cell);

					var lv = Info.BaseValue + water[idx]
						+ Cov(CityService.Parks, ServiceKind.Parks, cell) * Info.ParkWeight / 100
						+ Cov(CityService.Police, ServiceKind.Police, cell) * Info.PoliceWeight / 100
						+ Cov(CityService.Fire, ServiceKind.Fire, cell) * Info.FireWeight / 100
						+ Cov(CityService.Health, ServiceKind.Health, cell) * Info.HealthWeight / 100
						+ Cov(CityService.Education, ServiceKind.Education, cell) * Info.EducationWeight / 100
						+ shops[idx] * Info.ShopWeight / 100
						- poll * Info.PollutionWeight / 100
						- noise * Info.NoiseWeight / 100
						- Math.Min(Info.AbandonedMax, abandoned[idx] * Info.AbandonedPenalty);

					local[idx] = Math.Clamp(lv, 0, 100);
				}
			}
		}

		void RefreshRoadList()
		{
			var list = new List<CPos>();
			foreach (var c in roads.RoadCells)
				if (InMap(c))
					list.Add(c);

			roadCells = list.ToArray();
			roadVersion = roads.NetworkVersion;
		}

		// Value flows along roads (Jacobi relaxation, order independent), fades per step, then lots near a road take it.
		void Propagate()
		{
			Array.Copy(local, prop, local.Length);
			if (roads != null)
			{
				if (roadVersion != roads.NetworkVersion)
					RefreshRoadList();

				SpreadAlongRoads();
			}

			var next = new byte[value.Length];
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					// 3x3 box (in-map cells only) smooths cliffs.
					var sum = 0;
					var n = 0;
					for (var dy = -1; dy <= 1; dy++)
					{
						var yy = y + dy;
						if (yy < 0 || yy >= height)
							continue;

						for (var dx = -1; dx <= 1; dx++)
						{
							var xx = x + dx;
							if (xx < 0 || xx >= width)
								continue;

							sum += prop[yy * width + xx];
							n++;
						}
					}

					var v = sum / n;

					// Policy "LandValuePct": percent change of the land value (per district), 0 = off.
					var pct = progression != null ? progression.GetPolicy("LandValuePct", new CPos(x, y)) : 0;
					if (pct != 0)
						v = v * (100 + pct) / 100;

					next[y * width + x] = (byte)Math.Clamp(v, 0, 100);
				}
			}

			value = next;
			Version++;
		}

		void SpreadAlongRoads()
		{
			if (roadCells.Length == 0)
				return;

			// Seed: each road cell takes the best local score of itself and its non-road neighbours.
			foreach (var c in roadCells)
			{
				var best = local[c.Y * width + c.X];
				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (InMap(n) && !roads.IsRoad(n))
						best = Math.Max(best, local[n.Y * width + n.X]);
				}

				roadA[c.Y * width + c.X] = best;
			}

			for (var step = 0; step < Info.RoadSteps; step++)
			{
				foreach (var c in roadCells)
				{
					var idx = c.Y * width + c.X;
					var best = roadA[idx];
					foreach (var d in CityUtils.Neighbours4)
					{
						var n = c + d;
						if (InMap(n) && roads.IsRoad(n))
							best = Math.Max(best, roadA[n.Y * width + n.X] - Info.RoadFade);
					}

					roadB[idx] = best;
				}

				foreach (var c in roadCells)
					roadA[c.Y * width + c.X] = roadB[c.Y * width + c.X];
			}

			// Lots next to a road take the road value minus the distance fade.
			var reach = Info.LotReach;
			foreach (var c in roadCells)
			{
				var rv = roadA[c.Y * width + c.X];
				for (var dy = -reach; dy <= reach; dy++)
				{
					for (var dx = -reach; dx <= reach; dx++)
					{
						var n = new CPos(c.X + dx, c.Y + dy);
						if (!InMap(n))
							continue;

						var idx = n.Y * width + n.X;
						var cand = rv - Info.LotFade * Math.Max(Math.Abs(dx), Math.Abs(dy));
						if (cand > prop[idx])
							prop[idx] = cand;
					}
				}
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			// Average and range over the cells of zoned/built lots are enough for a determinism and sanity line.
			long sum = 0;
			int min = 255, max = 0;
			foreach (var b in value)
			{
				sum += b;
				min = Math.Min(min, b);
				max = Math.Max(max, b);
			}

			var h = 17;
			unchecked
			{
				for (var i = 0; i < value.Length; i += 7)
					h = h * 31 + value[i];
			}

			return $"landvalue v={Version} avg={(value.Length > 0 ? sum / value.Length : 0)} min={min} max={max} hash={h:X8}";
		}
	}
}
