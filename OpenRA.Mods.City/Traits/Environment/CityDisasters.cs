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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>
	/// Implemented by the city services work package: ignites a building (a fire that its fire engines then fight).
	/// ENV calls it for lightning strikes during storms. Synced callers only. Returns false if the building cannot burn.
	/// </summary>
	public interface IFireStarter
	{
		bool TryStartFire(int propertyId, string cause);
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Optional light disasters: lightning strikes that start fires during storms, and floods on low cells next to water after sustained rain.",
		"Disabled by default (enable with Enabled or the 'disasters' lobby option). Everything is derived from CityClimate and hashes, so it is replay safe.")]
	public class CityDisastersInfo : TraitInfo, ILobbyOptions
	{
		[Desc("Disasters on for every game (otherwise only when the lobby option is ticked).")]
		public readonly bool Enabled = false;

		[Desc("Ticks between disaster checks (300 = three game hours).")]
		public readonly int CheckTicks = 300;

		[Desc("Chance (percent) per check during a storm that lightning hits a building.")]
		public readonly int LightningChancePercent = 35;

		[Desc("Precipitation (0..100) that must persist over the last three weather samples for a flood.")]
		public readonly int FloodRainPercent = 62;

		[Desc("Cells from the water that the highest flood reaches.")]
		public readonly int MaxFloodRadius = 3;

		[Desc("Seed mixed into the hashes.")]
		public readonly int Seed = 7;

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption(map, "disasters", "Disasters", "Storms can start fires and long rain floods the shores.", true, 30, Enabled, false);
		}

		public override object Create(ActorInitializer init) { return new CityDisasters(init.Self, this); }
	}

	public class CityDisasters : ITick, IWorldLoaded, ISync, ICityAutoTestReporter
	{
		public readonly CityDisastersInfo Info;
		readonly World world;
		readonly byte[] shoreDistance;
		readonly int width;
		readonly int height;
		CityClimate climate;
		CityClock clock;
		CityStatistics stats;
		ICityProblems problems;
		IPropertyRegistry properties;
		IFireStarter fireStarter;
		int floodsTotal;

		public CityDisasters(Actor self, CityDisastersInfo info)
		{
			Info = info;
			world = self.World;
			width = world.Map.MapSize.Width;
			height = world.Map.MapSize.Height;
			shoreDistance = new byte[width * height];
		}

		public bool Enabled { get; private set; }

		/// <summary>0 = dry, 1..3 = rising water along the shores.</summary>
		[VerifySync]
		public int FloodLevel { get; private set; }

		/// <summary>Bumped when the flood level changes (render caches).</summary>
		public int FloodVersion { get; private set; }

		[VerifySync]
		public int Strikes { get; private set; }

		public int FloodedBuildings { get; private set; }

		/// <summary>Whether the cell is under water right now (a land cell within FloodLevel cells of water).</summary>
		public bool IsFlooded(CPos cell)
		{
			if (FloodLevel == 0 || cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
				return false;

			var d = shoreDistance[cell.Y * width + cell.X];
			return d > 0 && d <= FloodLevel;
		}

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			var wa = w.WorldActor;
			climate = wa.TraitOrDefault<CityClimate>();
			clock = wa.TraitOrDefault<CityClock>();
			stats = wa.TraitOrDefault<CityStatistics>();
			problems = wa.TraitsImplementing<ICityProblems>().FirstOrDefault();
			properties = wa.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			Enabled = Info.Enabled || w.LobbyInfo?.GlobalSettings.OptionOrDefault("disasters", false) == true;
			BuildShoreDistance();
		}

		// Chebyshev distance (1..MaxFloodRadius) of land cells to the nearest water cell, 0 elsewhere.
		void BuildShoreDistance()
		{
			var map = world.Map;
			var reach = Math.Clamp(Info.MaxFloodRadius, 1, 6);
			var water = new bool[width * height];
			foreach (var cell in map.AllCells)
			{
				if (cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height)
					water[cell.Y * width + cell.X] = map.GetTerrainInfo(cell).Type == "Water";
			}

			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					if (water[y * width + x])
						continue;

					var best = 0;
					for (var r = 1; r <= reach && best == 0; r++)
					{
						for (var dy = -r; dy <= r && best == 0; dy++)
						{
							for (var dx = -r; dx <= r; dx++)
							{
								if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
									continue;

								var nx = x + dx;
								var ny = y + dy;
								if (nx >= 0 && ny >= 0 && nx < width && ny < height && water[ny * width + nx])
								{
									best = r;
									break;
								}
							}
						}
					}

					shoreDistance[y * width + x] = (byte)best;
				}
			}
		}

		void ITick.Tick(Actor self)
		{
			if (!Enabled || climate == null || clock == null)
				return;

			var every = Math.Max(1, Info.CheckTicks);
			if (world.WorldTick % every != 11)
				return;

			var now = clock.Now;
			climate.Evaluate(now);
			CheckFlood(now);
			CheckLightning(now / every);
		}

		void CheckFlood(int now)
		{
			// Sustained rain: the weather of the last three samples (half a weather block apart) all above the limit.
			var block = Math.Max(1, climate.Info.WeatherBlockTicks / 2);
			var p1 = climate.PrecipitationAt(Math.Max(0, now - block));
			var p2 = climate.PrecipitationAt(Math.Max(0, now - 2 * block));
			var minPrecip = (int)Math.Min(climate.PrecipitationAt(now), Math.Min(p1, p2));
			var level = 0;
			if (climate.TemperatureX10 > 10 && minPrecip >= Info.FloodRainPercent)
				level = Math.Clamp(1 + (minPrecip - Info.FloodRainPercent) / 12, 1, Math.Max(1, Info.MaxFloodRadius));

			if (level != FloodLevel)
			{
				var rising = level > FloodLevel;
				FloodLevel = level;
				FloodVersion++;
				if (rising && level == 1)
				{
					floodsTotal++;
					stats?.Post("chirp-flood-warning-" + (1 + EnvHash.Hash(now, Info.Seed) % 2).ToString(CultureInfo.InvariantCulture), 0, null, 5, CPos.Zero);
				}
			}

			FloodedBuildings = 0;
			if (level == 0 || properties == null)
				return;

			var sample = CPos.Zero;
			var all = properties.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];
				if (p.Actor == null || !IsFlooded(p.Origin))
					continue;

				FloodedBuildings++;
				if (sample == CPos.Zero)
					sample = p.Origin;

				problems?.Raise(p.Actor, CityProblem.Flooded, Math.Max(1, Info.CheckTicks) + 20);
			}

			if (FloodedBuildings > 2)
				stats?.Post("chirp-flood-damage", 0, FloodedBuildings.ToString(CultureInfo.InvariantCulture), FloodedBuildings, sample);
		}

		void CheckLightning(int slot)
		{
			if (climate.Weather != CityWeather.Storm || properties == null || EnvHash.Hash(slot, Info.Seed, 1) % 100 >= Info.LightningChancePercent)
				return;

			var all = properties.All;
			if (all.Count == 0)
				return;

			// First operational building at or after a hashed index.
			var start = EnvHash.Hash(slot, Info.Seed, 2) % all.Count;
			for (var k = 0; k < all.Count; k++)
			{
				var p = all[(start + k) % all.Count];
				if (!p.Operational || p.Actor == null)
					continue;

				fireStarter ??= FindFireStarter();
				Strikes++;
				var started = fireStarter != null && fireStarter.TryStartFire(p.Id, "lightning");
				stats?.Post(started ? "chirp-lightning-fire" : "chirp-lightning-strike", 0, null, 3, p.Origin);
				return;
			}
		}

		IFireStarter FindFireStarter()
		{
			var found = world.WorldActor.TraitsImplementing<IFireStarter>().FirstOrDefault();
			if (found != null)
				return found;

			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				found = p.PlayerActor.TraitsImplementing<IFireStarter>().FirstOrDefault();
				if (found != null)
					return found;
			}

			return null;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			return $"disasters on={Enabled} flood={FloodLevel} floods={floodsTotal} flooded={FloodedBuildings} strikes={Strikes}";
		}
	}
}
