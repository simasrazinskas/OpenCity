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
	public partial class PollutionLayer
	{
		struct Source
		{
			public int MinX, MinY, MaxX, MaxY;
			public PollutionEmission Emission;
		}

		struct RoadSample
		{
			public int Index;
			public int Load;
			public int Air;
			public int Noise;
		}

		readonly List<Source> sources = [];
		readonly List<RoadSample> roads = [];
		ITrafficService traffic;
		IRoadNetwork network;
		IProgression progression;
		RoadLayer roadLayer;
		bool providersResolved;

		void ResolveProviders()
		{
			if (providersResolved)
				return;

			providersResolved = true;
			var wa = world.WorldActor;
			traffic = wa.TraitsImplementing<ITrafficService>().FirstOrDefault();
			network = wa.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			progression = wa.TraitsImplementing<IProgression>().FirstOrDefault();
			roadLayer = wa.TraitOrDefault<RoadLayer>();
			if (progression == null)
			{
				foreach (var p in world.Players)
				{
					if (!p.Playable)
						continue;

					progression = p.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault();
					break;
				}
			}
		}

		void Pulse()
		{
			ResolveProviders();
			if (climate != null && clock != null)
				climate.Evaluate(clock.Now);

			var noisePulse = pulse % Math.Max(1, Info.NoiseEveryPulses) == 0;
			CollectSources();
			if (noisePulse)
				SampleRoads();

			UpdateGround();
			UpdateAir();
			if (noisePulse)
				UpdateNoise();

			UpdateWater();

			pulse++;
			Version++;
			StateHash = ComputeHash();
		}

		void CollectSources()
		{
			sources.Clear();
			foreach (var pair in world.ActorsWithTrait<IPollutionEmitter>())
			{
				var e = pair.Trait.Emission;
				if (e.Ground <= 0 && e.Air <= 0 && e.Noise <= 0 && e.Water <= 0)
					continue;

				var s = new Source { Emission = e };
				var building = pair.Actor.TraitOrDefault<CityBuilding>();
				var cells = building != null && building.Cells.Length > 0 ? building.Cells : null;
				if (cells == null)
				{
					var occupied = pair.Actor.OccupiesSpace?.OccupiedCells();
					if (occupied != null && occupied.Length > 0)
					{
						cells = new CPos[occupied.Length];
						for (var i = 0; i < cells.Length; i++)
							cells[i] = occupied[i].Cell;
					}
				}

				if (cells != null)
				{
					s.MinX = s.MinY = int.MaxValue;
					s.MaxX = s.MaxY = int.MinValue;
					foreach (var c in cells)
					{
						s.MinX = Math.Min(s.MinX, c.X);
						s.MinY = Math.Min(s.MinY, c.Y);
						s.MaxX = Math.Max(s.MaxX, c.X);
						s.MaxY = Math.Max(s.MaxY, c.Y);
					}
				}
				else
				{
					var loc = pair.Actor.Location;
					s.MinX = s.MaxX = loc.X;
					s.MinY = s.MaxY = loc.Y;
				}

				// Policies (PollutionPct, NoisePct) scale emissions in their district; null-tolerant, 0 = no policy.
				if (progression != null)
				{
					var at = new CPos(s.MinX, s.MinY);
					var pollutionScale = Math.Clamp(100 + progression.GetPolicy("PollutionPct", at), 0, 300);
					var noiseScale = Math.Clamp(100 + progression.GetPolicy("NoisePct", at), 0, 300);
					s.Emission.Ground = s.Emission.Ground * pollutionScale / 100;
					s.Emission.Air = s.Emission.Air * pollutionScale / 100;
					s.Emission.Water = s.Emission.Water * pollutionScale / 100;
					s.Emission.Noise = s.Emission.Noise * noiseScale / 100;
				}

				sources.Add(s);
			}

			EmitterCount = sources.Count;
		}

		void SampleRoads()
		{
			roads.Clear();
			if (traffic == null || network == null)
				return;

			foreach (var cell in network.RoadCells)
			{
				if (!InMap(cell))
					continue;

				var load = traffic.GetTrafficLoad(cell);
				int n;
				var air = load * Info.AirPerTrafficLoad / 100;
				if (roadLayer != null)
				{
					// The road's own emission at full traffic (type and add-ons such as trees and barriers), scaled by how busy it is:
					// an idle road still makes a fifth of it.
					var activity = Math.Clamp(20 + load * 2, 20, 100);
					n = roadLayer.GetNoise(cell) * activity / 100;
					air = roadLayer.GetAirPollution(cell) * activity / 100 * Info.AirPerTrafficLoad / 10;
				}
				else
				{
					n = traffic.GetNoise(cell);
					if (n <= 0)
						n = load * Info.NoisePerTrafficLoad / 100;
				}

				if (progression != null && n > 0)
					n = n * Math.Clamp(100 + progression.GetPolicy("NoisePct", cell), 0, 300) / 100;

				if (air <= 0 && n <= 0)
					continue;

				roads.Add(new RoadSample { Index = Idx(cell), Load = load, Air = air, Noise = n });
			}
		}

		// Stamps `strength` (0..1000) around the source's footprint with linear falloff. `combine` merges like independent chances, otherwise adds.
		void Stamp(int[] map, in Source s, int radius, int strength, bool combine, bool waterOnly = false)
		{
			if (radius <= 0 || strength <= 0)
				return;

			var r16 = radius * 16;
			for (var y = Math.Max(0, s.MinY - radius); y <= Math.Min(height - 1, s.MaxY + radius); y++)
			{
				var dy = Math.Max(Math.Max(s.MinY - y, y - s.MaxY), 0);
				for (var x = Math.Max(0, s.MinX - radius); x <= Math.Min(width - 1, s.MaxX + radius); x++)
				{
					var dx = Math.Max(Math.Max(s.MinX - x, x - s.MaxX), 0);
					var d16 = Exts.ISqrt((dx * dx + dy * dy) * 256);
					if (d16 >= r16)
						continue;

					var idx = y * width + x;
					if (waterOnly && !isWater[idx])
						continue;

					var v = strength * (r16 - d16) / r16;
					map[idx] = combine ? Math.Min(1000, Combine(map[idx], v)) : Math.Min(1000, map[idx] + v);
				}
			}
		}

		void UpdateGround()
		{
			Array.Clear(target);
			foreach (var s in sources)
				Stamp(target, s, s.Emission.Radius > 0 ? s.Emission.Radius : Info.GroundRadius, s.Emission.Ground * 10, true);

			var rise = Math.Clamp(Info.GroundRisePercent, 1, 100);
			for (var i = 0; i < ground.Length; i++)
			{
				var v = ground[i];
				var t = target[i];
				if (t > v)
					v += Math.Max(1, (t - v) * rise / 100);

				// Slow decay: about 5 game days for a full-strength spill.
				if (v > 0)
					v -= v / 768 + 1;

				ground[i] = Math.Max(0, v);
			}

			// A little blur every fourth pulse so pollution creeps outward from a persistent source.
			if (pulse % 4 == 0)
				Blur(ground);

			// Groundwater: ground pollution seeps in, the aquifer recovers (faster near water).
			for (var i = 0; i < groundwater.Length; i++)
			{
				var v = groundwater[i];
				var g = ground[i];
				if (g > Info.SeepThreshold)
					v += (g - Info.SeepThreshold) / 64;

				if (v > 0)
					v -= 1 + deposit[i] / 50 + v / 512;

				groundwater[i] = Math.Clamp(v, 0, 1000);
			}
		}

		void Blur(int[] map)
		{
			Array.Copy(map, scratch, map.Length);
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var i = y * width + x;
					var v = scratch[i];
					var n = x > 0 ? scratch[i - 1] : v;
					var e = x < width - 1 ? scratch[i + 1] : v;
					var up = y > 0 ? scratch[i - width] : v;
					var down = y < height - 1 ? scratch[i + width] : v;
					map[i] = (v * 12 + n + e + up + down) / 16;
				}
			}
		}

		void UpdateAir()
		{
			// Inject emitters and (cached) traffic.
			foreach (var s in sources)
			{
				if (s.Emission.Air <= 0)
					continue;

				var radius = Math.Max(1, Math.Min(Info.AirRadius, s.Emission.Radius > 0 ? s.Emission.Radius : Info.AirRadius));
				Stamp(air, s, radius, s.Emission.Air * Info.AirInjection / 100, false);
			}

			for (var i = 0; i < roads.Count; i++)
			{
				var r = roads[i];
				air[r.Index] = Math.Min(1000, air[r.Index] + r.Air);
			}

			// Advect downwind.
			var speed = climate != null ? climate.WindSpeed : 0;
			if (speed > 0)
			{
				var dir = CityClimate.WindVectors[(climate.WindDir % 8 + 8) % 8];
				Array.Clear(scratch);
				var pct = Math.Min(60, speed * Info.WindPercentPerLevel);
				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						var i = y * width + x;
						var v = air[i];
						if (v == 0)
							continue;

						var moved = v * pct / 100;
						scratch[i] += v - moved;
						var nx = x + dir.X;
						var ny = y + dir.Y;
						if (nx >= 0 && ny >= 0 && nx < width && ny < height)
							scratch[ny * width + nx] += moved;
					}
				}

				Array.Copy(scratch, air, air.Length);
			}

			// Diffuse (0.6 self + 0.1 per neighbour; open map edges), decay and rain wash.
			var wash = 0;
			if (climate != null && climate.Precipitation >= 15)
				wash = climate.TemperatureX10 <= 10 ? 20 : 10;

			Array.Copy(air, scratch, air.Length);
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var i = y * width + x;
					var v = scratch[i] * 6;
					if (x > 0)
						v += scratch[i - 1];
					if (x < width - 1)
						v += scratch[i + 1];
					if (y > 0)
						v += scratch[i - width];
					if (y < height - 1)
						v += scratch[i + width];

					v /= 10;
					if (v > 0)
					{
						v -= (v + 31) / 32;
						if (wash > 0)
							v -= v / wash;
					}

					air[i] = Math.Max(0, v);
				}
			}
		}

		void UpdateNoise()
		{
			Array.Clear(noise);
			var roadRadius = Math.Max(1, Info.RoadNoiseRadius);
			var r16 = roadRadius * 16;
			for (var i = 0; i < roads.Count; i++)
			{
				var r = roads[i];
				var n = Math.Min(100, r.Noise) * 10;
				if (n <= 0)
					continue;

				var cx = r.Index % width;
				var cy = r.Index / width;
				for (var y = Math.Max(0, cy - roadRadius); y <= Math.Min(height - 1, cy + roadRadius); y++)
				{
					for (var x = Math.Max(0, cx - roadRadius); x <= Math.Min(width - 1, cx + roadRadius); x++)
					{
						var d16 = Exts.ISqrt(((x - cx) * (x - cx) + (y - cy) * (y - cy)) * 256);
						if (d16 >= r16)
							continue;

						var idx = y * width + x;
						noise[idx] = Math.Min(1000, Combine(noise[idx], n * (r16 - d16) / r16));
					}
				}
			}

			foreach (var s in sources)
			{
				if (s.Emission.Noise > 0)
					Stamp(noise, s, s.Emission.Radius > 0 ? s.Emission.Radius : Info.NoiseRadius, s.Emission.Noise * 10, true);
			}
		}

		void UpdateWater()
		{
			Array.Clear(target);
			foreach (var s in sources)
			{
				if (s.Emission.Water > 0)
					Stamp(target, s, s.Emission.Radius > 0 ? s.Emission.Radius : 8, s.Emission.Water * 10, true, true);
			}

			for (var i = 0; i < water.Length; i++)
			{
				if (!isWater[i])
				{
					water[i] = 0;
					continue;
				}

				var v = water[i];
				if (target[i] > v)
					v += Math.Max(1, (target[i] - v) / 6);

				if (v > 0)
					v -= v / 64 + 1;

				water[i] = Math.Max(0, v);
			}

			// Flowing water spreads pollution along connected water cells.
			if (pulse % 4 == 0)
			{
				Array.Copy(water, scratch, water.Length);
				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						var i = y * width + x;
						if (!isWater[i])
							continue;

						var sum = scratch[i] * 6;
						var weight = 6;
						if (x > 0 && isWater[i - 1])
						{
							sum += scratch[i - 1];
							weight++;
						}

						if (x < width - 1 && isWater[i + 1])
						{
							sum += scratch[i + 1];
							weight++;
						}

						if (y > 0 && isWater[i - width])
						{
							sum += scratch[i - width];
							weight++;
						}

						if (y < height - 1 && isWater[i + width])
						{
							sum += scratch[i + width];
							weight++;
						}

						water[i] = sum / weight;
					}
				}
			}
		}

		int ComputeHash()
		{
			unchecked
			{
				var h = 17;
				for (var i = 0; i < ground.Length; i++)
				{
					h = h * 31 + ground[i];
					h = h * 31 + air[i];
					h = h * 31 + noise[i];
					h = h * 31 + groundwater[i];
					h = h * 31 + water[i];
				}

				return h;
			}
		}
	}
}
