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
	[TraitLocation(SystemActors.World)]
	[Desc("Per-cell pollution maps (ground, air with wind, noise, groundwater, surface water). Sources are actors implementing IPollutionEmitter",
		"plus traffic from ITrafficService. Integer, deterministic, updated incrementally once per pulse.")]
	public class PollutionLayerInfo : TraitInfo
	{
		[Desc("Ticks between updates.")]
		public readonly int PulseTicks = 25;

		[Desc("Noise is recomputed from scratch every this many pulses.")]
		public readonly int NoiseEveryPulses = 4;

		[Desc("Ground pollution spread radius around an emitter (linear falloff).")]
		public readonly int GroundRadius = 5;

		[Desc("Air pollution radius at the source.")]
		public readonly int AirRadius = 3;

		[Desc("Noise radius around an emitter.")]
		public readonly int NoiseRadius = 4;

		[Desc("Noise radius around busy road cells.")]
		public readonly int RoadNoiseRadius = 3;

		[Desc("Fraction (percent) of a ground target the map moves towards each pulse.")]
		public readonly int GroundRisePercent = 12;

		[Desc("Air injected per pulse at an emitter, per 100 emission (0..1000 internal units).")]
		public readonly int AirInjection = 90;

		[Desc("Air injected per pulse per 100 traffic load on a road cell (0..1000 internal units).")]
		public readonly int AirPerTrafficLoad = 28;

		[Desc("Noise (0..100) at a road cell per 100 traffic load, percent.")]
		public readonly int NoisePerTrafficLoad = 55;

		[Desc("Percent of the air in a cell that moves one cell downwind per pulse, per wind speed level.")]
		public readonly int WindPercentPerLevel = 12;

		[Desc("Ground pollution above this (0..1000) seeps into the groundwater.")]
		public readonly int SeepThreshold = 300;

		[Desc("Terrain types that count as water.")]
		public readonly string[] WaterTerrainTypes = ["Water"];

		public override object Create(ActorInitializer init) { return new PollutionLayer(init.Self, this); }
	}

	// All maps are int arrays on a 0..1000 internal scale (the public API divides by 10 to 0..100).
	//  Ground      persistent, not wind driven; moves towards the emitters' target, decays very slowly (about 5 game days).
	//  Air         injected by emitters and traffic, advected along the wind, diffused, decays in about 5 game hours (rain washes it).
	//  Noise       recomputed from scratch every few pulses from roads and emitters (instant when the source stops).
	//  Groundwater ground pollution seeps in and the aquifer recovers slowly (faster near water).
	//  Water       surface water pollution of water cells (sewage outlets etc.), diffuses over connected water.
	public partial class PollutionLayer : ITick, IWorldLoaded, ISync, IPollutionMap, ICityAutoTestReporter
	{
		public readonly PollutionLayerInfo Info;
		readonly World world;
		readonly int width;
		readonly int height;
		readonly int[] ground;
		readonly int[] air;
		readonly int[] noise;
		readonly int[] groundwater;
		readonly int[] water;
		readonly int[] target;
		readonly int[] scratch;
		readonly byte[] deposit;
		readonly bool[] isWater;

		CityClimate climate;
		CityClock clock;
		int pulse;

		public PollutionLayer(Actor self, PollutionLayerInfo info)
		{
			Info = info;
			world = self.World;
			width = world.Map.MapSize.Width;
			height = world.Map.MapSize.Height;
			var n = width * height;
			ground = new int[n];
			air = new int[n];
			noise = new int[n];
			groundwater = new int[n];
			water = new int[n];
			target = new int[n];
			scratch = new int[n];
			deposit = new byte[n];
			isWater = new bool[n];
		}

		/// <summary>Bumped every pulse (for overlay caches).</summary>
		public int Version { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; }

		public int EmitterCount { get; private set; }

		bool InMap(CPos cell) { return cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height; }

		int Idx(CPos cell) { return cell.Y * width + cell.X; }

		/// <summary>0..100.</summary>
		public int GetGround(CPos cell) { return InMap(cell) ? ground[Idx(cell)] / 10 : 0; }

		public int GetAir(CPos cell) { return InMap(cell) ? air[Idx(cell)] / 10 : 0; }

		public int GetNoise(CPos cell) { return InMap(cell) ? noise[Idx(cell)] / 10 : 0; }

		public int GetGroundwater(CPos cell) { return InMap(cell) ? groundwater[Idx(cell)] / 10 : 0; }

		public int GetWaterPollution(CPos cell) { return InMap(cell) ? water[Idx(cell)] / 10 : 0; }

		/// <summary>Worst of ground, air and noise at a cell, 0..100. Facade for the legacy single pollution value.</summary>
		public int GetCombined(CPos cell)
		{
			if (!InMap(cell))
				return 0;

			var i = Idx(cell);
			return Math.Max(ground[i], Math.Max(air[i], noise[i])) / 10;
		}

		/// <summary>Exposure of a building cell: ground, air and noise, each 0..100.</summary>
		public void Exposure(CPos cell, out int groundValue, out int airValue, out int noiseValue)
		{
			if (!InMap(cell))
			{
				groundValue = airValue = noiseValue = 0;
				return;
			}

			var i = Idx(cell);
			groundValue = ground[i] / 10;
			airValue = air[i] / 10;
			noiseValue = noise[i] / 10;
		}

		/// <summary>Drinking-water quality 0..100 (100 = clean) around a pump or tower: 100 minus the average groundwater pollution in the radius.</summary>
		public int WaterQuality(CPos center, int radius)
		{
			var sum = 0;
			var count = 0;
			for (var y = Math.Max(0, center.Y - radius); y <= Math.Min(height - 1, center.Y + radius); y++)
			{
				for (var x = Math.Max(0, center.X - radius); x <= Math.Min(width - 1, center.X + radius); x++)
				{
					var i = y * width + x;
					sum += Math.Max(groundwater[i], water[i]);
					count++;
				}
			}

			return count == 0 ? 100 : 100 - sum / count / 10;
		}

		/// <summary>Average of a layer over every map cell, 0..100 (for statistics).</summary>
		public int Average(int layer)
		{
			var map = layer switch { 0 => ground, 1 => air, 2 => noise, 3 => groundwater, _ => water };
			long sum = 0;
			for (var i = 0; i < map.Length; i++)
				sum += map[i];

			return (int)(sum / Math.Max(1, map.Length) / 10);
		}

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			clock = w.WorldActor.TraitOrDefault<CityClock>();
			climate = w.WorldActor.TraitOrDefault<CityClimate>();
			ComputeWater();
		}

		void ComputeWater()
		{
			var map = world.Map;
			foreach (var cell in map.AllCells)
			{
				if (!InMap(cell))
					continue;

				var type = map.GetTerrainInfo(cell).Type;
				isWater[Idx(cell)] = Array.IndexOf(Info.WaterTerrainTypes, type) >= 0;
			}

			// Aquifer richness: 100 on water, falling off linearly over 8 cells.
			const int Reach = 8;
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					if (!isWater[y * width + x])
						continue;

					// Only shore cells seed the field, which keeps this cheap on big lakes.
					var shore = x == 0 || y == 0 || x == width - 1 || y == height - 1
						|| !isWater[y * width + x - 1] || !isWater[y * width + x + 1] || !isWater[(y - 1) * width + x] || !isWater[(y + 1) * width + x];
					deposit[y * width + x] = 100;
					if (!shore)
						continue;

					for (var yy = Math.Max(0, y - Reach); yy <= Math.Min(height - 1, y + Reach); yy++)
					{
						for (var xx = Math.Max(0, x - Reach); xx <= Math.Min(width - 1, x + Reach); xx++)
						{
							var d = Exts.ISqrt(((xx - x) * (xx - x) + (yy - y) * (yy - y)) * 256);
							var v = 100 * (Reach * 16 - d) / (Reach * 16);
							var j = yy * width + xx;
							if (v > deposit[j])
								deposit[j] = (byte)v;
						}
					}
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (world.WorldTick % Math.Max(1, Info.PulseTicks) != 0)
				return;

			Pulse();
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var maxG = 0;
			var maxA = 0;
			var maxN = 0;
			for (var i = 0; i < ground.Length; i++)
			{
				maxG = Math.Max(maxG, ground[i]);
				maxA = Math.Max(maxA, air[i]);
				maxN = Math.Max(maxN, noise[i]);
			}

			return $"pollution avg ground={Average(0)} air={Average(1)} noise={Average(2)} gw={Average(3)} water={Average(4)} " +
				$"max g/a/n={maxG / 10}/{maxA / 10}/{maxN / 10} emitters={EmitterCount} wind={climate?.WindDir ?? -1}/{climate?.WindSpeed ?? 0} hash={StateHash}";
		}

		// Combines two 0..1000 strengths like independent chances.
		static int Combine(int a, int b) { return a + b - a * b / 1000; }
	}
}
