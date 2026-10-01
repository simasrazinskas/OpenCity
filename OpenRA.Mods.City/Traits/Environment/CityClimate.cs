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
	public enum CityWeather : byte { Clear = 0, Cloudy, Rain, Snow, Storm }

	/// <summary>Deterministic integer hashing for environment effects (pure functions of tick/cell/id, never random state).</summary>
	public static class EnvHash
	{
		public static int Hash(int a, int b = 0, int c = 0)
		{
			unchecked
			{
				var h = (uint)a * 0x9E3779B1u;
				h ^= (uint)b * 0x85EBCA77u + 0x165667B1u + (h << 6) + (h >> 2);
				h ^= (uint)c * 0xC2B2AE3Du + 0x27D4EB2Fu + (h << 6) + (h >> 2);
				h ^= h >> 15;
				h *= 0x2C1B3C6Du;
				h ^= h >> 12;
				h *= 0x297A2D39u;
				h ^= h >> 15;
				return (int)(h & 0x7FFFFFFF);
			}
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Deterministic climate: temperature, weather, wind and snow cover as pure functions of the CityClock (no synced state, replay-safe).")]
	public class CityClimateInfo : TraitInfo
	{
		[Desc("Mean daytime temperature per calendar month in degrees C x10 (Jan..Dec).")]
		public readonly int[] MonthlyTemperatureX10 = [-20, 0, 60, 110, 160, 200, 230, 220, 170, 100, 40, -10];

		[Desc("Hours of daylight per calendar month (Jan..Dec), x10.")]
		public readonly int[] MonthlyDaylightX10 = [80, 90, 110, 130, 150, 160, 160, 150, 130, 110, 90, 80];

		[Desc("Chance (percent) that a six-hour weather block brings rain or snow, per calendar month (Jan..Dec).")]
		public readonly int[] MonthlyPrecipitationChance = [30, 25, 30, 28, 25, 20, 16, 20, 25, 30, 35, 35];

		[Desc("Diurnal temperature swing (degrees x10): cold at 05:00, warm at 15:00.")]
		public readonly int DiurnalAmplitudeX10 = 50;

		[Desc("Ticks per weather block (one block is 6 game hours by default).")]
		public readonly int WeatherBlockTicks = 600;

		[Desc("Prevailing wind direction (0 N, 1 NE, 2 E, ... 7 NW) the air moves towards.")]
		public readonly int PrevailingWindDirection = 2;

		[Desc("Fixed wind (as in Cities: Skylines 2). When false the wind varies by one step around the prevailing direction.")]
		public readonly bool FixedWind = false;

		[Desc("Wind speed 0..3 used when FixedWind is set.")]
		public readonly int FixedWindSpeed = 1;

		[Desc("Seed mixed into weather hashes (change for a different weather history).")]
		public readonly int Seed = 1;

		public override object Create(ActorInitializer init) { return new CityClimate(init.Self, this); }
	}

	/// <summary>
	/// Climate service. Everything is derived from CityClock.Now with integer hashes, so it never needs syncing
	/// and every client (and every replay) sees the same weather. Consumers: PollutionLayer (wind, rain), utilities
	/// (PowerDemandPercent), citizens (leisure), the atmosphere renderer.
	/// </summary>
	public class CityClimate : ITick, INotifyCreated
	{
		public static readonly CVec[] WindVectors =
		[
			new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1)
		];

		public readonly CityClimateInfo Info;

		int cachedTick = int.MinValue;

		public CityClimate(Actor self, CityClimateInfo info)
		{
			Info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			Clock = self.TraitOrDefault<CityClock>();
		}

		/// <summary>Degrees C x10 at the current tick.</summary>
		public int TemperatureX10 { get; private set; }

		public CityWeather Weather { get; private set; }

		/// <summary>0..100 cloud cover.</summary>
		public int Cloud { get; private set; }

		/// <summary>0..100 precipitation intensity (0 when Weather is Clear/Cloudy).</summary>
		public int Precipitation { get; private set; }

		/// <summary>0..100 snow cover on the ground.</summary>
		public int SnowDepth { get; private set; }

		/// <summary>Direction the wind moves towards: 0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW.</summary>
		public int WindDir { get; private set; }

		/// <summary>0..3.</summary>
		public int WindSpeed { get; private set; }

		/// <summary>Electricity demand multiplier, 100..200 (CS2: lowest at 18-22 C, double at -18 C / 58 C).</summary>
		public int PowerDemandPercent { get; private set; } = 100;

		/// <summary>Solar output multiplier, 0..100 (cloud cover and snow reduce it, night is 0).</summary>
		public int SolarPercent { get; private set; } = 100;

		/// <summary>
		/// Seasonal effect hooks. Nothing reads them until a work package opts in, so they change no balance by themselves.
		/// Water demand multiplier, 100..130: summer heat raises water use (lawns, showers).
		/// </summary>
		public int WaterDemandPercent { get; private set; } = 100;

		/// <summary>Road speed multiplier, 80..100: rain and above all snow slow vehicles. Traffic can scale its link speeds with it.</summary>
		public int TrafficSpeedPercent { get; private set; } = 100;

		/// <summary>Accident risk multiplier, 100..300: wet, snowy, stormy roads. Traffic/services can scale their accident chance with it.</summary>
		public int AccidentRiskPercent { get; private set; } = 100;

		/// <summary>Crop growth multiplier, 0..100: zero in the cold season and under snow, full in spring and early summer. Farms can scale yield with it.</summary>
		public int CropGrowthPercent { get; private set; } = 100;

		/// <summary>True while crops grow (CropGrowthPercent &gt; 0).</summary>
		public bool GrowingSeason => CropGrowthPercent > 0;

		public CityClock Clock { get; private set; }

		void ITick.Tick(Actor self)
		{
			if (Clock == null)
				return;

			Evaluate(Clock.Now);
		}

		/// <summary>Recomputes the cached values for Clock time `now` (idempotent per tick).</summary>
		public void Evaluate(int now)
		{
			if (Clock == null || now == cachedTick)
				return;

			cachedTick = now;
			var month = MonthFloat(now);
			var hour = (float)(now % Clock.TicksPerDay) / Clock.TicksPerHour;

			TemperatureX10 = (int)MathF.Round(TemperatureAt(month, hour, now));
			Cloud = Math.Clamp((int)MathF.Round(CloudAt(now)), 0, 100);
			Precipitation = Math.Clamp((int)MathF.Round(PrecipitationAt(now)), 0, 100);
			SnowDepth = Math.Clamp((int)MathF.Round(SnowCoverAt(month)), 0, 100);

			if (Precipitation < 15)
				Weather = Cloud > 55 ? CityWeather.Cloudy : CityWeather.Clear;
			else if (TemperatureX10 <= 10)
				Weather = CityWeather.Snow;
			else if (Precipitation >= 85 && EnvHash.Hash(Info.Seed, BlockIndex(now), 7) % 3 == 0)
				Weather = CityWeather.Storm;
			else
				Weather = CityWeather.Rain;

			WindAt(now, out var dir, out var speed);
			WindDir = dir;
			WindSpeed = speed;

			var t = TemperatureX10;
			var p = 100;
			if (t < 180)
				p = 100 + (180 - t) * 100 / 360;
			else if (t > 220)
				p = 100 + (t - 220) * 100 / 360;

			PowerDemandPercent = Math.Clamp(p, 100, 200);

			var solar = Clock.IsDaytime ? 100 - Cloud / 4 : 0;
			if (SnowDepth > 50)
				solar /= 2;

			SolarPercent = solar;

			WaterDemandPercent = t > 200 ? Math.Min(130, 100 + (t - 200) * 30 / 150) : 100;

			var raining = Precipitation >= 15 && TemperatureX10 > 10;
			var snowing = Precipitation >= 15 && TemperatureX10 <= 10;
			var speedPercent = 100;
			var risk = 100;
			if (raining)
			{
				speedPercent -= Precipitation * 8 / 100;
				risk += Precipitation / 2;
			}

			if (Weather == CityWeather.Storm)
			{
				speedPercent -= 4;
				risk += 50;
			}

			if (SnowDepth > 30)
			{
				speedPercent = Math.Min(speedPercent, 100 - SnowDepth * 15 / 100);
				risk += SnowDepth / 2;
			}

			if (snowing)
			{
				speedPercent -= 5;
				risk += 30;
			}

			TrafficSpeedPercent = Math.Clamp(speedPercent, 80, 100);
			AccidentRiskPercent = Math.Clamp(risk, 100, 300);

			int growth;
			if (t < 50 || SnowDepth > 20)
				growth = 0;
			else if (t < 150)
				growth = t - 50;
			else if (t <= 260)
				growth = 100;
			else
				growth = Math.Max(40, 100 - (t - 260) * 20 / 100);

			CropGrowthPercent = growth;
		}

		int BlockIndex(int now) { return now / Math.Max(1, Info.WeatherBlockTicks); }

		/// <summary>Fractional month position, 0..12, Jan = 0.0 (continuous across the year).</summary>
		public float MonthFloat(int now)
		{
			var ticksPerDay = Clock.TicksPerDay;
			var day = now / ticksPerDay;
			var frac = (float)(now % ticksPerDay) / ticksPerDay;
			var months = Math.Max(1, Clock.Info.MonthsPerYear);
			return (Clock.Info.StartMonth - 1 + day) % months + frac;
		}

		static float Monthly(int[] table, float monthFloat)
		{
			var n = table.Length;

			// Table values describe mid-month: interpolate between month centres.
			var shifted = monthFloat - 0.5f;
			var m0 = (int)MathF.Floor(shifted);
			var f = shifted - m0;
			var a = table[(m0 % n + n) % n];
			var b = table[((m0 + 1) % n + n) % n];
			return a + (b - a) * f;
		}

		/// <summary>Mean temperature (x10) at this point of the year, without the diurnal swing.</summary>
		public float MeanTemperatureX10(float monthFloat) { return Monthly(Info.MonthlyTemperatureX10, monthFloat); }

		/// <summary>Hours of daylight (x10) at this point of the year.</summary>
		public float DaylightX10(float monthFloat) { return Monthly(Info.MonthlyDaylightX10, monthFloat); }

		float TemperatureAt(float monthFloat, float hour, int now)
		{
			var mean = MeanTemperatureX10(monthFloat);

			// Smooth swing (basic float maths only, so every platform agrees): coldest at 05:00, warmest at 15:00.
			var t = (hour - 5f + 24f) % 24f;
			var x = t < 10f ? t / 10f : 1f - (t - 10f) / 14f;
			var diurnal = (2f * x * x * (3f - 2f * x) - 1f) * Info.DiurnalAmplitudeX10;

			// A slow weather offset (cold fronts, warm spells), +-5 C, changing smoothly each day.
			var day = now / Clock.TicksPerDay;
			var f = (float)(now % Clock.TicksPerDay) / Clock.TicksPerDay;
			var o0 = EnvHash.Hash(Info.Seed, day, 1) % 101 - 50;
			var o1 = EnvHash.Hash(Info.Seed, day + 1, 1) % 101 - 50;
			var offset = o0 + (o1 - o0) * f * f * (3 - 2 * f);
			return mean + diurnal + offset;
		}

		float BlockValue(int block)
		{
			var chance = Info.MonthlyPrecipitationChance;
			var ticksPerBlock = Math.Max(1, Info.WeatherBlockTicks);
			var month = (int)MonthFloat(Math.Max(0, block) * ticksPerBlock);
			var c = chance[Math.Clamp(month, 0, chance.Length - 1)];
			var h = EnvHash.Hash(Info.Seed, block, 3) % 100;
			return h < c ? 55 + EnvHash.Hash(Info.Seed, block, 34) % 46 : 0;
		}

		float CloudBlock(int block)
		{
			var precip = BlockValue(block);
			var baseCloud = EnvHash.Hash(Info.Seed, block, 11) % 55;
			return Math.Min(100, baseCloud + (precip > 0 ? 30 + precip * 15 / 100 : 0));
		}

		float Blocks(int now, Func<int, float> value)
		{
			var len = Math.Max(1, Info.WeatherBlockTicks);
			var pos = (float)now / len - 0.5f;
			var b0 = (int)MathF.Floor(pos);
			var f = pos - b0;
			f = f * f * (3 - 2 * f);
			var a = value(b0);
			var b = value(b0 + 1);
			return a + (b - a) * f;
		}

		/// <summary>0..100 precipitation, smooth across weather blocks (a block's value is reached at its centre).</summary>
		public float PrecipitationAt(int now) { return Blocks(now, BlockValue); }

		/// <summary>0..100 cloud cover, smooth across weather blocks.</summary>
		public float CloudAt(int now) { return Blocks(now, CloudBlock); }

		/// <summary>0..100 snow cover, a smooth function of the season (cold months are white).</summary>
		public float SnowCoverAt(float monthFloat)
		{
			var mean = MeanTemperatureX10(monthFloat);
			return Math.Clamp((50f - mean) * 100f / 70f, 0f, 100f);
		}

		void WindAt(int now, out int dir, out int speed)
		{
			if (Info.FixedWind)
			{
				dir = (Info.PrevailingWindDirection % 8 + 8) % 8;
				speed = Math.Clamp(Info.FixedWindSpeed, 0, 3);
				return;
			}

			var period = now / Math.Max(1, Clock.TicksPerDay / 2);
			var h = EnvHash.Hash(Info.Seed, period, 5);
			dir = (Info.PrevailingWindDirection + h % 3 - 1 + 8) % 8;
			speed = 1 + EnvHash.Hash(Info.Seed, period, 6) % 3;
			if (Precipitation >= 85)
				speed = Math.Min(3, speed + 1);
		}
	}
}
