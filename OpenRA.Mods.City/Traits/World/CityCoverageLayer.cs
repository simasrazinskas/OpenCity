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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Per-cell service coverage, land value and pollution maps, recomputed periodically.")]
	public class CityCoverageLayerInfo : TraitInfo
	{
		[Desc("World ticks between recomputations.")]
		public readonly int RecomputeInterval = 100;

		[Desc("Pollution spreads this many cells from its source (linear falloff).")]
		public readonly int PollutionRadius = 6;

		[Desc("Cells within this distance of water get a land value bonus.")]
		public readonly int WaterRadius = 6;

		[Desc("Maximum land value bonus next to water.")]
		public readonly int WaterBonus = 15;

		[Desc("Land value of a cell without any services.")]
		public readonly int BaseLandValue = 25;

		[Desc("Terrain type names that count as water.")]
		public readonly string[] WaterTerrainTypes = ["Water"];

		public override object Create(ActorInitializer init) { return new CityCoverageLayer(init.Self, this); }
	}

	// Maps are all 0..100, integers, deterministic.
	//  service coverage: written by ServiceSimulation (road catchment x capacity) for every kind it manages. For a kind it does not
	//    manage (no ServiceSimulation) the legacy radius stamp is used: Strength * (R - d) / R, combined like independent chances.
	//  pollution: delegated to IPollutionMap when present, otherwise the legacy radius stamp of CityBuilding.Pollution.
	//  land value: delegated to ILandValueSource when present, otherwise
	//    base + water bonus + parks*0.30 + (police+fire+health)*0.10 + education*0.12 - pollution*0.40.
	public class CityCoverageLayer : ITick, IWorldLoaded
	{
		public readonly CityCoverageLayerInfo Info;
		readonly World world;
		readonly byte[][] coverage = new byte[13][];
		readonly bool[] external = new bool[13];
		ILandValueSource landValueSource;
		IPollutionMap pollutionMap;
		readonly byte[] landValue;
		readonly byte[] pollution;
		readonly byte[] waterBonus;
		readonly int width;
		readonly int height;
		bool computed;

		public CityCoverageLayer(Actor self, CityCoverageLayerInfo info)
		{
			Info = info;
			world = self.World;
			width = world.Map.MapSize.Width;
			height = world.Map.MapSize.Height;
			for (var i = 0; i < coverage.Length; i++)
				coverage[i] = new byte[width * height];

			landValue = new byte[width * height];
			pollution = new byte[width * height];
			waterBonus = new byte[width * height];
			Array.Fill(landValue, (byte)info.BaseLandValue);
		}

		/// <summary>Incremented after each recomputation (for overlay caches).</summary>
		public int Version { get; private set; }

		bool InMap(CPos cell) { return cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height; }

		static ServiceKind KindOf(CityService service)
		{
			switch (service)
			{
				case CityService.Police: return ServiceKind.Police;
				case CityService.Fire: return ServiceKind.Fire;
				case CityService.Health: return ServiceKind.Health;
				case CityService.Education: return ServiceKind.Education;
				default: return ServiceKind.Parks;
			}
		}

		/// <summary>0..100 (legacy API).</summary>
		public int GetCoverage(CityService service, CPos cell) { return GetServiceCoverage(KindOf(service), cell); }

		/// <summary>0..100 coverage x capacity of any service kind.</summary>
		public int GetServiceCoverage(ServiceKind kind, CPos cell) { return InMap(cell) ? coverage[(int)kind][cell.Y * width + cell.X] : 0; }

		/// <summary>0..100. Delegates to the land value layer when there is one.</summary>
		public int GetLandValue(CPos cell)
		{
			if (landValueSource != null)
				return landValueSource.GetLandValue(cell);

			return InMap(cell) ? landValue[cell.Y * width + cell.X] : Info.BaseLandValue;
		}

		/// <summary>0..100. Delegates to the pollution layer when there is one (the worst of ground and air pollution).</summary>
		public int GetPollution(CPos cell)
		{
			if (pollutionMap != null)
				return Math.Max(pollutionMap.GetGround(cell), pollutionMap.GetAir(cell));

			return InMap(cell) ? pollution[cell.Y * width + cell.X] : 0;
		}

		/// <summary>
		/// A service simulation takes over the coverage map of a kind: `map` (width * height, row-major) is read from now on and
		/// must be updated in place by the caller, followed by <see cref="Touch"/>.
		/// </summary>
		public void SetExternalCoverage(ServiceKind kind, byte[] map)
		{
			if (map == null || map.Length != width * height)
				return;

			coverage[(int)kind] = map;
			external[(int)kind] = true;
		}

		/// <summary>Marks the maps as changed (info view caches).</summary>
		public void Touch() { Version++; }

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			landValueSource = w.WorldActor.TraitsImplementing<ILandValueSource>().FirstOrDefault();
			pollutionMap = w.WorldActor.TraitsImplementing<IPollutionMap>().FirstOrDefault();
			ComputeWaterBonus();
		}

		void ComputeWaterBonus()
		{
			var map = world.Map;
			var radius = Math.Max(1, Info.WaterRadius);
			foreach (var cell in map.AllCells)
			{
				var type = map.GetTerrainInfo(cell).Type;
				if (Array.IndexOf(Info.WaterTerrainTypes, type) < 0 || !InMap(cell))
					continue;

				for (var y = cell.Y - radius; y <= cell.Y + radius; y++)
				{
					for (var x = cell.X - radius; x <= cell.X + radius; x++)
					{
						if (x < 0 || y < 0 || x >= width || y >= height)
							continue;

						var d16 = Exts.ISqrt(((x - cell.X) * (x - cell.X) + (y - cell.Y) * (y - cell.Y)) * 256);
						if (d16 >= radius * 16)
							continue;

						var v = (byte)(Info.WaterBonus * (radius * 16 - d16) / (radius * 16));
						var idx = y * width + x;
						if (v > waterBonus[idx])
							waterBonus[idx] = v;
					}
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (computed && world.WorldTick % Math.Max(1, Info.RecomputeInterval) != 0)
				return;

			computed = true;
			Recompute();
		}

		void Recompute()
		{
			for (var i = 0; i < coverage.Length; i++)
				if (!external[i])
					Array.Clear(coverage[i]);

			Array.Clear(pollution);

			foreach (var pair in world.ActorsWithTrait<CityBuilding>())
			{
				var b = pair.Trait;
				if (b.Cells.Length == 0 || !b.IsOperational)
					continue;

				var service = b.Service;
				if (service != null && b.HasPower && !external[(int)service.Info.Kind])
					Stamp(b, service.Info.Radius, service.Info.Strength, coverage[(int)service.Info.Kind]);

				if (b.Info.Pollution > 0 && pollutionMap == null)
					Stamp(b, Info.PollutionRadius, b.Info.Pollution, pollution);
			}

			for (var i = 0; i < landValue.Length; i++)
			{
				var poll = pollutionMap != null ? Math.Max(pollutionMap.GetGround(CellAt(i)), pollutionMap.GetAir(CellAt(i))) : pollution[i];
				var lv = Info.BaseLandValue + waterBonus[i]
					+ coverage[(int)ServiceKind.Parks][i] * 30 / 100
					+ (coverage[(int)ServiceKind.Police][i] + coverage[(int)ServiceKind.Fire][i] + coverage[(int)ServiceKind.Health][i]) * 10 / 100
					+ coverage[(int)ServiceKind.Education][i] * 12 / 100
					- poll * 40 / 100;
				landValue[i] = (byte)Math.Clamp(lv, 0, 100);
			}

			Version++;
		}

		CPos CellAt(int index) { return new CPos(index % width, index / width); }

		void Stamp(CityBuilding b, int radius, int strength, byte[] map)
		{
			if (radius <= 0 || strength <= 0)
				return;

			var minX = int.MaxValue;
			var minY = int.MaxValue;
			var maxX = int.MinValue;
			var maxY = int.MinValue;
			foreach (var c in b.Cells)
			{
				minX = Math.Min(minX, c.X);
				minY = Math.Min(minY, c.Y);
				maxX = Math.Max(maxX, c.X);
				maxY = Math.Max(maxY, c.Y);
			}

			var r16 = radius * 16;
			for (var y = Math.Max(0, minY - radius); y <= Math.Min(height - 1, maxY + radius); y++)
			{
				var dy = Math.Max(Math.Max(minY - y, y - maxY), 0);
				for (var x = Math.Max(0, minX - radius); x <= Math.Min(width - 1, maxX + radius); x++)
				{
					var dx = Math.Max(Math.Max(minX - x, x - maxX), 0);
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
	}
}
