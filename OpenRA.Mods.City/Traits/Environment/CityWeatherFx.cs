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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Render-only weather drawn over the world: rain streaks and splashes, snow flakes, storm lightning, morning fog, drifting cloud",
		"shadows and smog haze over polluted cells. Every particle is a pure function of its index and a render clock (hash-driven).")]
	public class CityWeatherFxInfo : TraitInfo
	{
		[Desc("Particles at full precipitation intensity (per 1600x900 screen; scaled with the viewport).")]
		public readonly int MaxRainParticles = 420;

		public readonly int MaxSnowParticles = 380;

		[Desc("Rain fall speed in pixels per second.")]
		public readonly float RainSpeed = 520f;

		[Desc("Snow fall speed in pixels per second.")]
		public readonly float SnowSpeed = 38f;

		[Desc("Sideways drift in pixels per second per wind level.")]
		public readonly float WindDrift = 18f;

		[Desc("Average seconds between lightning strikes during a storm.")]
		public readonly float LightningSeconds = 6f;

		[Desc("Draw a haze veil over heavily polluted cells. Off by default: the veil hides the buildings beneath it,",
			"and pollution already reads from dark smoke, ground stains and the pollution info view.")]
		public readonly bool ShowSmog = false;

		[Desc("Air pollution (0..100) from which smog haze is drawn over a cell (when ShowSmog is on).")]
		public readonly int SmogThreshold = 60;

		[Desc("Image with the LIFE weather sequences (rain, splash, snow, fog, cloud-shadow, lightning, smog).")]
		public readonly string Image = "fx";

		public override object Create(ActorInitializer init) { return new CityWeatherFx(init.Self, this); }
	}

	public class CityWeatherFx : IRenderAboveWorld, IWorldLoaded
	{
		const int Capacity = 1600;
		const int CloudShadows = 10;

		readonly CityWeatherFxInfo info;
		readonly World world;
		CityAtmosphere atmosphere;
		CityClimate climate;
		IPollutionMap pollution;
		ISpriteSequence rain, splash, snow, fog, cloudShadow, lightning, smog;
		PaletteReference palette;
		long lastTime;
		float clock;
		int lastStrike = -1;

		public CityWeatherFx(Actor self, CityWeatherFxInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			climate = w.WorldActor.TraitOrDefault<CityClimate>();
			pollution = w.WorldActor.TraitsImplementing<IPollutionMap>().FirstOrDefault();
			var seqs = w.Map.Sequences;
			ISpriteSequence Seq(string name) => seqs.HasSequence(info.Image, name) ? seqs.GetSequence(info.Image, name) : null;
			rain = Seq("rain");
			splash = Seq("splash");
			snow = Seq("snow");
			fog = Seq("fog");
			cloudShadow = Seq("cloud-shadow");
			lightning = Seq("lightning");
			smog = Seq("smog");
			palette = wr.Palette("city");
		}

		static float H01(int a, int b)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 40503u ^ 0x9E3779B9u;
				h ^= h >> 15;
				h *= 2246822519u;
				h ^= h >> 13;
				return (h & 0xffffff) / 16777216f;
			}
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			var now = Game.RunTime;
			var dt = Math.Min(0.1f, (now - lastTime) / 1000f);
			lastTime = now;
			if (atmosphere == null)
				return;

			if (!world.Paused)
				clock += dt;

			var light = Math.Clamp(1f - atmosphere.Darkness * 0.6f, 0.4f, 1f);
			DrawCloudShadows(wr);
			DrawSmog(wr, light);
			DrawFog(wr, light);
			if (atmosphere.Precipitation > 0.01f)
				DrawPrecipitation(wr, light);
		}

		// Screen-space helper: the sprite's anchor lands on (x, y) like a SpriteRenderable at that screen point.
		void DrawAt(Sprite s, float x, float y, float alpha, float light)
		{
			var loc = new Vector3(x - (int)(s.Size.X / 2), y - (int)(s.Size.Y / 2), y);
			Game.Renderer.WorldSpriteRenderer.DrawSprite(s, palette, loc, 1f, new Vector3(light * alpha, light * alpha, light * alpha), alpha);
		}

		void DrawPrecipitation(WorldRenderer wr, float light)
		{
			var vp = wr.Viewport;
			var size = vp.ViewportSize;
			var topLeft = vp.TopLeft;
			var snowing = atmosphere.Snowing;
			var intensity = atmosphere.Precipitation;
			var storm = atmosphere.Storm;
			var area = size.Width * size.Height / (1600f * 900f);
			var max = snowing ? info.MaxSnowParticles : info.MaxRainParticles;
			var count = Math.Clamp((int)(max * intensity * Math.Max(0.3f, area)), 0, Capacity);
			var wind = (climate?.WindSpeed ?? 1) * info.WindDrift * (storm ? 1.6f : 1f);
			var w = size.Width + 80f;
			var h = size.Height + 80f;
			var seq = snowing ? snow : rain;
			if (seq == null)
				return;

			for (var i = 0; i < count; i++)
			{
				var speed = 0.75f + H01(i, 1) * 0.5f;
				var fall = speed * (snowing ? info.SnowSpeed : info.RainSpeed) * (storm ? 1.2f : 1f);
				var period = h / fall;
				var u = clock / period + H01(i, 2);
				var cycle = (int)MathF.Floor(u);
				var f = u - cycle;
				var t = f * period;

				// Streaks in the art slant down-left: the wind blows them that way.
				var x = H01(i, cycle * 2 + 3) * w - 40f - (snowing ? 0.45f : 1f) * wind * t;
				if (snowing)
					x += MathF.Sin(H01(i, 4) * 6.28f + clock * 1.1f) * 10f;

				x = ((x + 40f) % w + w) % w - 40f;
				var y = f * h - 40f;
				var frame = snowing ? i % 3 : Math.Min(2, (int)((speed - 0.75f) * 6f));
				DrawAt(seq.GetSprite(frame), topLeft.X + (int)x, topLeft.Y + (int)y, Math.Clamp(intensity + 0.3f, 0f, 1f), light);
			}

			// Splashes on the ground (rain only): short 4-frame bursts at hashed screen points.
			if (!snowing && splash != null)
			{
				var splashes = count / 3;
				for (var i = 0; i < splashes; i++)
				{
					var u = clock * 3f + H01(i, 7);
					var cycle = (int)MathF.Floor(u);
					var frame = (int)((u - cycle) * splash.Length);
					var x = topLeft.X + H01(i, cycle * 3 + 8) * size.Width;
					var y = topLeft.Y + H01(i, cycle * 3 + 9) * size.Height;
					DrawAt(splash.GetSprite(frame), (int)x, (int)y, 1f, light);
				}
			}

			if (storm)
				DrawLightning(wr);
		}

		void DrawLightning(WorldRenderer wr)
		{
			// One strike chance per LightningSeconds slot; the bolt shows for ~0.2 s at a hashed screen point.
			var slot = (int)(clock / info.LightningSeconds);
			if (H01(slot, 11) > 0.7f)
				return;

			var start = slot * info.LightningSeconds + H01(slot, 12) * info.LightningSeconds * 0.8f;
			var age = clock - start;
			if (age < 0f || age > 0.24f)
				return;

			if (lastStrike != slot && !world.Paused)
			{
				lastStrike = slot;
				atmosphere.Flash(0.7f + H01(slot, 13) * 0.3f);
			}

			if (lightning == null)
				return;

			var vp = wr.Viewport;
			var x = vp.TopLeft.X + (0.15f + H01(slot, 14) * 0.7f) * vp.ViewportSize.Width;
			var y = vp.TopLeft.Y + (0.45f + H01(slot, 15) * 0.4f) * vp.ViewportSize.Height;
			var s = lightning.GetSprite((int)(age / 0.08f));
			DrawAt(s, (int)x, (int)y - s.Size.Y / 2 + 2, 1f, 1f);
		}

		// Morning fog (or the test override): dithered fog tiles over every visible cell, denser near dawn.
		void DrawFog(WorldRenderer wr, float light)
		{
			if (fog == null)
				return;

			var hour = atmosphere.HourF;
			var morning = Math.Clamp(1f - Math.Abs(hour - 6.5f) / 2.5f, 0f, 1f);
			var cloud = climate != null ? climate.Cloud / 100f : 0f;
			var density = atmosphere.TestWeather == 4 ? 1f : morning * Math.Clamp((cloud - 0.45f) * 2.5f, 0f, 1f) * (atmosphere.Storm ? 0f : 1f);
			if (density <= 0.05f)
				return;

			// One tile kind for the whole view: the periodic noise only joins seamlessly with itself.
			var tile = fog.GetSprite(density > 0.8f ? 1 : 0);
			var alpha = 0.06f + density * 0.14f; // Light enough that buildings stay readable through it.
			foreach (var c in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				var s = wr.ScreenPxPosition(world.Map.CenterOfCell(c.ToCPos(world.Map)));
				DrawAt(tile, s.X, s.Y, alpha, light);
			}
		}

		// Soft cloud shadows drifting with the wind across the map (world anchored, so they scroll with the view).
		void DrawCloudShadows(WorldRenderer wr)
		{
			if (cloudShadow == null || climate == null)
				return;

			var cloud = climate.Cloud / 100f;
			if (cloud < 0.2f || atmosphere.Darkness > 0.6f || atmosphere.Precipitation > 0.3f)
				return;

			var map = world.Map;
			var wind = CityClimate.WindVectors[climate.WindDir];
			var speed = 40f + climate.WindSpeed * 25f;
			var spanX = map.MapSize.Width * 1024f;
			var spanY = map.MapSize.Height * 1024f;
			var count = (int)(CloudShadows * Math.Min(1f, cloud * 1.5f));
			for (var i = 0; i < count; i++)
			{
				var x = (H01(i, 21) * spanX + wind.X * speed * clock) % spanX;
				var y = (H01(i, 22) * spanY + wind.Y * speed * clock) % spanY;
				x = x < 0 ? x + spanX : x;
				y = y < 0 ? y + spanY : y;
				var s = wr.ScreenPxPosition(new WPos((int)x, (int)y, 0));
				DrawAt(cloudShadow.GetSprite(i & 1), s.X, s.Y, 0.35f * (1f - atmosphere.Darkness), 1f);
			}
		}

		// Smog haze over cells with heavy air pollution (3 intensities).
		void DrawSmog(WorldRenderer wr, float light)
		{
			if (!info.ShowSmog || smog == null || pollution == null)
				return;

			foreach (var c in wr.Viewport.AllVisibleCells.CandidateMapCoords)
			{
				var cell = c.ToCPos(world.Map);
				var air = pollution.GetAir(cell);
				if (air < info.SmogThreshold)
					continue;

				var level = Math.Min(2, (air - info.SmogThreshold) * 3 / Math.Max(1, 100 - info.SmogThreshold));
				var s = wr.ScreenPxPosition(world.Map.CenterOfCell(cell));
				DrawAt(smog.GetSprite(level), s.X, s.Y, 0.55f, light);
			}
		}
	}
}
