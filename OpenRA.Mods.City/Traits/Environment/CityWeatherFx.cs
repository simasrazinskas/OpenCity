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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only rain and snow particles (and storm lightning) driven by the weather of CityClimate. Uses its own local random numbers.")]
	public class CityWeatherFxInfo : TraitInfo
	{
		[Desc("Particles at full precipitation intensity (per 1600x900 screen; scaled with the viewport).")]
		public readonly int MaxRainParticles = 520;

		public readonly int MaxSnowParticles = 380;

		[Desc("Rain fall speed in pixels per second.")]
		public readonly float RainSpeed = 1100f;

		[Desc("Snow fall speed in pixels per second.")]
		public readonly float SnowSpeed = 55f;

		[Desc("Sideways drift in pixels per second per wind level.")]
		public readonly float WindDrift = 70f;

		[Desc("Average seconds between lightning flashes during a storm.")]
		public readonly float LightningSeconds = 6f;

		public override object Create(ActorInitializer init) { return new CityWeatherFx(init.Self, this); }
	}

	public class CityWeatherFx : IRenderAboveWorld, IWorldLoaded
	{
		struct Particle
		{
			public float X, Y, Speed, Phase, Size;
		}

		const int Capacity = 1600;

		readonly CityWeatherFxInfo info;
		readonly World world;
		readonly MersenneTwister random = new(12345);
		readonly Particle[] particles = new Particle[Capacity];
		CityAtmosphere atmosphere;
		CityClimate climate;
		long lastTime;
		float lightningTimer;
		bool seeded;

		public CityWeatherFx(Actor self, CityWeatherFxInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			climate = w.WorldActor.TraitOrDefault<CityClimate>();
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			var now = Game.RunTime;
			var dt = Math.Min(0.1f, (now - lastTime) / 1000f);
			lastTime = now;
			if (atmosphere == null || atmosphere.Precipitation <= 0.01f)
				return;

			var vp = wr.Viewport;
			var size = vp.ViewportSize;
			var topLeft = vp.TopLeft;
			var snowing = atmosphere.Snowing;
			var intensity = atmosphere.Precipitation;
			var max = snowing ? info.MaxSnowParticles : info.MaxRainParticles;
			var area = size.Width * size.Height / (1600f * 900f);
			var count = Math.Clamp((int)(max * intensity * Math.Max(0.3f, area)), 0, Capacity);

			if (!seeded)
			{
				seeded = true;
				for (var i = 0; i < Capacity; i++)
					Respawn(ref particles[i], size.Width, size.Height, true);
			}

			var wind = (climate?.WindSpeed ?? 1) * info.WindDrift * (atmosphere.Storm ? 1.6f : 1f);
			var windX = climate != null ? CityClimate.WindVectors[climate.WindDir].X : 1;
			var drift = wind * (windX != 0 ? windX : 0.25f) * (snowing ? 0.45f : 0.3f);
			var tint = atmosphere.CurrentTint;
			var light = Math.Clamp((tint.X + tint.Y + tint.Z) / 3f * 1.35f, 0.4f, 1f);
			var paused = world.Paused;

			var renderer = Game.Renderer.WorldRgbaColorRenderer;
			for (var i = 0; i < count; i++)
			{
				ref var p = ref particles[i];
				if (!paused)
				{
					var fall = p.Speed * (snowing ? info.SnowSpeed : info.RainSpeed) * (atmosphere.Storm ? 1.2f : 1f);
					p.Y += fall * dt;
					p.X += (drift + (snowing ? MathF.Sin(p.Phase + now * 0.0011f) * 22f : 0f)) * dt;
					if (p.Y > size.Height + 20f || p.X > size.Width + 40f || p.X < -40f)
						Respawn(ref p, size.Width, size.Height, false);
				}

				var x = topLeft.X + p.X;
				var y = topLeft.Y + p.Y;
				if (snowing)
				{
					var a = (byte)(235 * Math.Clamp(intensity + 0.3f, 0f, 1f));
					var c = Color.FromArgb(a, (byte)(250 * light), (byte)(252 * light), (byte)(255 * light));
					var s = p.Size;
					renderer.FillRect(new Vector3(x, y, y), new Vector3(x + s, y + s, y), c);
				}
				else
				{
					var len = 9f + p.Speed * 7f;
					var dx = drift * len / info.RainSpeed;
					var c = Color.FromArgb((byte)(185 * Math.Clamp(intensity + 0.25f, 0f, 1f)), (byte)(175 * light), (byte)(195 * light), (byte)(235 * light));
					renderer.DrawLine(new Vector3(x, y, y), new Vector3(x - dx, y - len, y), 1f, c);
				}
			}

			if (atmosphere.Storm && !paused)
			{
				lightningTimer -= dt;
				if (lightningTimer <= 0f)
				{
					lightningTimer = info.LightningSeconds * (0.4f + random.NextFloat() * 1.2f);
					atmosphere.Flash(0.7f + random.NextFloat() * 0.3f);
				}
			}
		}

		void Respawn(ref Particle p, int width, int height, bool anywhere)
		{
			p.X = random.NextFloat() * (width + 80) - 40;
			p.Y = anywhere ? random.NextFloat() * height : -random.NextFloat() * 30f;
			p.Speed = 0.75f + random.NextFloat() * 0.5f;
			p.Phase = random.NextFloat() * 6.28f;
			p.Size = 1.5f + random.NextFloat() * 1.8f;
		}
	}
}
