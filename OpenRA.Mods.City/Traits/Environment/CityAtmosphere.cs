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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only day/night, season and weather lighting. Drives the engine TintPostProcessEffect from the CityClock",
		"(the tint is applied before the info view overlay, status icons and the HUD, so those stay bright).")]
	public class CityAtmosphereInfo : TraitInfo
	{
		[Desc("Tint colour (R, G, B, ambient) at full night.")]
		public readonly float[] Night = [0.40f, 0.48f, 0.80f, 0.80f];

		[Desc("Tint just before sunrise.")]
		public readonly float[] PreDawn = [0.62f, 0.60f, 0.80f, 0.86f];

		[Desc("Tint at sunrise.")]
		public readonly float[] Dawn = [1.00f, 0.80f, 0.68f, 0.94f];

		[Desc("Tint at midday.")]
		public readonly float[] Day = [1.00f, 1.00f, 1.00f, 1.00f];

		[Desc("Tint at sunset.")]
		public readonly float[] Dusk = [1.00f, 0.74f, 0.62f, 0.94f];

		[Desc("Tint shortly after sunset.")]
		public readonly float[] Twilight = [0.66f, 0.56f, 0.80f, 0.88f];

		[Desc("Multiplier of the whole tint while an info view is active (so the heat colours pop).")]
		public readonly float InfoViewDim = 0.62f;

		[Desc("How much of the cloud cover (0..1) darkens the world on a fully overcast day.")]
		public readonly float OvercastDarkening = 0.2f;

		[Desc("Extra darkening in a storm.")]
		public readonly float StormDarkening = 0.22f;

		[Desc("Lock the time of day to this hour (0..23, -1 = follow the clock). For screenshots and map previews.")]
		public readonly int FixedHour = -1;

		public override object Create(ActorInitializer init) { return new CityAtmosphere(init.Self, this); }
	}

	public class CityAtmosphere : ITickRender, IWorldLoaded
	{
		// Seasonal colour cast per calendar month (Jan..Dec): cold blue-white winter, fresh spring, warm golden autumn.
		static readonly float[][] SeasonTint =
		[
			[0.94f, 0.98f, 1.06f], [0.95f, 0.99f, 1.05f], [0.99f, 1.00f, 1.00f], [1.00f, 1.02f, 0.99f],
			[1.00f, 1.03f, 0.98f], [1.02f, 1.02f, 0.97f], [1.04f, 1.02f, 0.95f], [1.04f, 1.01f, 0.95f],
			[1.03f, 0.99f, 0.96f], [1.05f, 0.97f, 0.92f], [1.00f, 0.97f, 0.97f], [0.95f, 0.98f, 1.05f]
		];

		// Multiplier for tree sprites per calendar month (Jan..Dec): pale winter, lime spring, deep summer, orange autumn.
		static readonly float[][] FoliageSeason =
		[
			[1.10f, 1.18f, 1.34f], [1.10f, 1.16f, 1.28f], [1.04f, 1.12f, 0.95f], [1.04f, 1.14f, 0.88f],
			[0.98f, 1.08f, 0.86f], [0.92f, 1.00f, 0.88f], [0.92f, 0.98f, 0.88f], [0.98f, 0.98f, 0.82f],
			[1.40f, 1.00f, 0.60f], [1.85f, 0.86f, 0.38f], [1.55f, 0.92f, 0.62f], [1.15f, 1.12f, 1.20f]
		];

		public readonly CityAtmosphereInfo Info;
		readonly World world;
		float flash;
		CityClock clock;
		CityClimate climate;
		TintPostProcessEffect tint;
		InfoViewLayer infoViews;

		int lastTick = -1;
		long lastTickTime;
		float infoDim;

		public CityAtmosphere(Actor self, CityAtmosphereInfo info)
		{
			Info = info;
			world = self.World;
		}

		/// <summary>Time of day in game hours (0..24), including the sub-tick fraction.</summary>
		public float HourF { get; private set; } = 12f;

		/// <summary>Clock time in ticks including the sub-tick fraction.</summary>
		public float NowF { get; private set; }

		/// <summary>0 = bright day, 1 = deep night. Drives lights, particles and the moon.</summary>
		public float Darkness { get; private set; }

		/// <summary>Current tint multiplier (R, G, B including ambient).</summary>
		public System.Numerics.Vector3 CurrentTint { get; private set; } = System.Numerics.Vector3.One;

		/// <summary>
		/// Ambient multiply for depth-sorted world sprites (see <see cref="CityView.SpriteTint"/>). Equal to
		/// <see cref="CurrentTint"/> when the tint post-process runs before the actors (ground only), Vector3.One while it
		/// still runs after them.
		/// </summary>
		public System.Numerics.Vector3 SpriteTint { get; private set; } = System.Numerics.Vector3.One;

		/// <summary>Render-only weather override for headless screenshots (CityAutoTest weather=): 0 clear, 1 rain, 2 snow, 3 storm, 4 fog (-1 off).</summary>
		public int TestWeather = -1;

		/// <summary>True when sorted sprites must apply <see cref="SpriteTint"/> themselves (the ambient split, §3.5).</summary>
		public bool SplitAmbient { get; private set; }

		/// <summary>0..1 precipitation intensity for the weather effects.</summary>
		public float Precipitation { get; private set; }

		public bool Snowing { get; private set; }

		public bool Storm { get; private set; }

		/// <summary>0..1 snow cover on the ground.</summary>
		public float SnowCover { get; private set; }

		/// <summary>Fractional month position, Jan = 0.</summary>
		public float MonthF { get; private set; }

		/// <summary>Colour multiplier for foliage (trees) for the current time of year.</summary>
		public System.Numerics.Vector3 FoliageTint { get; private set; } = System.Numerics.Vector3.One;

		float? forcedHour, forcedMonth;

		/// <summary>Render-only override of the time of day (0..24) and/or month (0 = Jan .. 12) for screenshots and tests.</summary>
		public void ForceTime(float? hour, float? month)
		{
			if (hour.HasValue)
				forcedHour = hour;

			if (month.HasValue)
				forcedMonth = month;
		}

		/// <summary>Triggers a short lightning flash (render only).</summary>
		public void Flash(float strength = 1f) { flash = Math.Max(flash, strength); }

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			clock = w.WorldActor.TraitOrDefault<CityClock>();
			climate = w.WorldActor.TraitOrDefault<CityClimate>();
			tint = w.WorldActor.TraitOrDefault<TintPostProcessEffect>();
			infoViews = w.WorldActor.TraitOrDefault<InfoViewLayer>();

			// The engine's tint pass darkens everything drawn before it. Run before the actors (AfterTerrain) it only
			// darkens the ground, and the sorted sprites multiply SpriteTint themselves so lit frames can stay bright.
			SplitAmbient = tint != null && ((IRenderPostProcessPass)tint).Type == PostProcessPassType.AfterTerrain;
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (clock == null)
				return;

			var tick = world.WorldTick;
			if (tick != lastTick)
			{
				lastTick = tick;
				lastTickTime = Game.RunTime;
			}

			var frac = world.Paused || world.Timestep <= 0 ? 0f : Math.Clamp((Game.RunTime - lastTickTime) / (float)world.Timestep, 0f, 1f);
			NowF = clock.Now + frac;
			var ticksPerDay = clock.TicksPerDay;
			HourF = NowF % ticksPerDay / clock.TicksPerHour;
			if (Info.FixedHour >= 0)
				HourF = Info.FixedHour % 24;

			if (forcedHour.HasValue)
				HourF = forcedHour.Value % 24;

			MonthF = climate != null ? climate.MonthFloat(clock.Now) + frac / ticksPerDay : 0f;
			if (forcedMonth.HasValue)
				MonthF = forcedMonth.Value;

			var daylightHours = climate != null ? climate.DaylightX10(MonthF) / 10f : 12f;
			var sunrise = 12f - daylightHours / 2f;
			var sunset = 12f + daylightHours / 2f;

			var c = LightAt(HourF, sunrise, sunset, out var darkness);
			Darkness = darkness;

			// Seasonal colour cast.
			var season = SeasonAt(MonthF, SeasonTint);
			var r = c[0] * season[0];
			var g = c[1] * season[1];
			var b = c[2] * season[2];
			var ambient = c[3];

			var f = SeasonAt(MonthF, FoliageSeason);
			FoliageTint = new System.Numerics.Vector3(f[0], f[1], f[2]);

			// Weather: clouds dim and desaturate the scene.
			Precipitation = 0;
			Snowing = false;
			Storm = false;
			SnowCover = 0;
			if (climate != null)
			{
				var now = clock.Now;
				var cloud = climate.CloudAt(now) / 100f;
				var precip = climate.PrecipitationAt(now) / 100f;
				Precipitation = precip < 0.15f ? 0f : precip;
				climate.Evaluate(now);
				Snowing = climate.TemperatureX10 <= 10;
				Storm = !Snowing && climate.Weather == CityWeather.Storm;
				SnowCover = climate.SnowCoverAt(MonthF) / 100f;
				if (TestWeather == 0)
					precip = Precipitation = 0f;

				if (TestWeather is > 0 and < 4)
				{
					precip = Precipitation = 0.8f;
					Snowing = TestWeather == 2;
					Storm = TestWeather == 3;
					SnowCover = Snowing ? 1f : SnowCover;
				}

				var dim = cloud * Info.OvercastDarkening + (Storm ? precip * Info.StormDarkening : 0f);
				ambient *= 1f - dim;
				Darkness = Math.Min(1f, Darkness + (1f - Darkness) * dim * 1.3f);
				var grey = (r + g + b) / 3f;
				var desat = cloud * 0.22f;
				r += (grey - r) * desat;
				g += (grey - g) * desat;
				b += (grey - b) * desat;
			}

			// Info view: dim the world beneath the (undarkened) overlay.
			var target = infoViews != null && infoViews.Mode != CityInfoView.None ? 1f : 0f;
			infoDim += (target - infoDim) * 0.18f;
			if (Math.Abs(target - infoDim) < 0.01f)
				infoDim = target;

			if (SplitAmbient)
			{
				// Info views read like CS2: neutral daylight on the ground (the ramp colours stay exact), buildings in view
				// colours (WithIsoSprite), no night or weather cast.
				r += (1f - r) * infoDim;
				g += (1f - g) * infoDim;
				b += (1f - b) * infoDim;
				ambient += (1f - ambient) * infoDim;
			}
			else
				ambient *= 1f - infoDim * (1f - Info.InfoViewDim);

			// Lightning: pushes the scene towards white and decays quickly.
			if (flash > 0.01f)
			{
				r += (1.15f - r) * flash;
				g += (1.15f - g) * flash;
				b += (1.2f - b) * flash;
				ambient = Math.Max(ambient, 0.6f + 0.5f * flash);
				flash *= 0.84f;
			}
			else
				flash = 0f;

			CurrentTint = new System.Numerics.Vector3(r * ambient, g * ambient, b * ambient);
			SpriteTint = SplitAmbient ? CurrentTint : System.Numerics.Vector3.One;

			if (tint != null)
			{
				tint.Red = r;
				tint.Green = g;
				tint.Blue = b;
				tint.Ambient = ambient;
			}
		}

		static float[] SeasonAt(float monthF, float[][] table)
		{
			var shifted = monthF - 0.5f;
			var m0 = (int)MathF.Floor(shifted);
			var f = shifted - m0;
			var a = table[(m0 % 12 + 12) % 12];
			var b = table[((m0 + 1) % 12 + 12) % 12];
			return [a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f];
		}

		float[] LightAt(float hour, float sunrise, float sunset, out float darkness)
		{
			// Key times relative to sunrise/sunset: (hour, colour, darkness).
			Span<float> times = [sunrise - 1.6f, sunrise - 0.4f, sunrise + 0.3f, sunrise + 1.7f, sunset - 1.7f, sunset - 0.2f, sunset + 0.7f, sunset + 1.9f];

			float[][] colors = [Info.Night, Info.PreDawn, Info.Dawn, Info.Day, Info.Day, Info.Dusk, Info.Twilight, Info.Night];
			ReadOnlySpan<float> dark = [1f, 0.65f, 0.25f, 0f, 0f, 0.25f, 0.65f, 1f];

			if (hour <= times[0] || hour >= times[7])
			{
				darkness = 1f;
				return colors[0];
			}

			var i = 0;
			while (i < 6 && hour > times[i + 1])
				i++;

			var f = (hour - times[i]) / Math.Max(0.001f, times[i + 1] - times[i]);
			f = f * f * (3f - 2f * f);
			darkness = dark[i] + (dark[i + 1] - dark[i]) * f;
			var a = colors[i];
			var b = colors[i + 1];
			return [a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f, a[3] + (b[3] - a[3]) * f];
		}
	}
}
