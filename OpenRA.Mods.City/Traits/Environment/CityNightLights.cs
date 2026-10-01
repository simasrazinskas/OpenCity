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
	[Desc("Render-only night lights: additive window glows on powered, operational buildings and street lamp pools on roads.",
		"Drawn after the tint pass, so the lights stay bright while the world is dark. Unpowered buildings stay dark.")]
	public class CityNightLightsInfo : TraitInfo
	{
		[Desc("Image with the light sequence.")]
		public readonly string Image = "envlights";

		[SequenceReference(nameof(Image))]
		public readonly string Sequence = "lights";

		[Desc("Darkness (0..1) at which the lights start to fade in.")]
		public readonly float MinDarkness = 0.22f;

		public readonly float WindowStrength = 0.95f;
		public readonly float LampStrength = 0.6f;

		[Desc("A street lamp stands on every road cell whose x + y is a multiple of this.")]
		public readonly int LampSpacing = 3;

		[Desc("Percent of buildings with lit windows for each hour of the day (24 values).")]
		public readonly int[] LitPercentByHour =
		[
			45, 35, 30, 30, 30, 30, 22, 10, 5, 0, 0, 0,
			0, 0, 0, 0, 55, 72, 86, 92, 92, 86, 72, 58
		];

		public override object Create(ActorInitializer init) { return new CityNightLights(init.Self, this); }
	}

	public partial class CityNightLights : IRenderAboveWorld, IWorldLoaded
	{
		const int WarmSmall = 0;
		const int Lamp = 4;
		const int Cool = 6;
		const int Furnace = 7;

		readonly CityNightLightsInfo info;
		readonly World world;
		ISpriteSequence sequence;
		CityAtmosphere atmosphere;
		IRoadNetwork roads;

		public CityNightLights(Actor self, CityNightLightsInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			sequence = w.Map.Sequences.GetSequence(info.Image, info.Sequence);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (atmosphere == null || sequence == null)
				return;

			var intensity = Math.Clamp((atmosphere.Darkness - info.MinDarkness) / (1f - info.MinDarkness), 0f, 1f);
			if (intensity <= 0.02f)
				return;

			var renderer = Game.Renderer.WorldSpriteRenderer;
			var hour = (int)atmosphere.HourF % 24;
			var litPercent = info.LitPercentByHour[hour % info.LitPercentByHour.Length];

			DrawBuildings(wr, renderer, intensity, litPercent);
			DrawLamps(wr, renderer, intensity);
		}

		void DrawBuildings(WorldRenderer wr, SpriteRenderer renderer, float intensity, int litPercent)
		{
			BeginFrame();
			var vp = wr.Viewport;
			var windowSprite = sequence.GetSprite(WarmSmall);
			foreach (var actor in world.ScreenMap.RenderableActorsInBox(vp.TopLeft, vp.BottomRight))
			{
				var b = actor.TraitOrDefault<CityBuilding>();
				if (b == null || b.Cells.Length == 0 || !b.HasPower || !b.IsOperational)
					continue;

				var id = (int)actor.ActorID;
				var entry = LightsOf(actor, wr);
				var rect = entry.Bounds;
				if (rect.Width <= 0)
					continue;

				var strength = intensity * info.WindowStrength;
				if (entry.Windows != null)
				{
					// Light individual windows (the lit windows drawn in the art); more of them are on in the evening.
					var windows = entry.Windows;
					var lit = 0;
					for (var i = 0; i < windows.Length; i++)
					{
						var chance = Math.Min(100, litPercent + 25);
						if (EnvHash.Hash(id, i, 17) % 100 >= chance)
							continue;

						lit++;
						Draw(renderer, windowSprite, entry.Origin.X + windows[i].X + 0.5f, entry.Origin.Y + windows[i].Y + 0.5f, 0.6f, strength * 0.9f);
					}

					// A soft building-wide glow when plenty of windows are lit (reads as a lit interior from afar).
					if (lit * 3 > windows.Length)
					{
						var frame = WarmSmall + 1;
						if (b.Category == ZoneCategory.Commercial || b.Category == ZoneCategory.Office)
							frame = Cool;
						else if (b.Category == ZoneCategory.Industrial)
							frame = Furnace;

						Draw(renderer, sequence.GetSprite(frame), rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.62f, 1.2f, strength * 0.22f);
					}

					continue;
				}

				// Fallback: one or two glows around the middle of the sprite.
				if (EnvHash.Hash(id, 31) % 100 >= litPercent)
					continue;

				int fallback;
				switch (b.Category)
				{
					case ZoneCategory.Commercial:
					case ZoneCategory.Office:
						fallback = Cool;
						break;
					case ZoneCategory.Industrial:
						fallback = Furnace;
						break;
					default:
						fallback = rect.Width <= 40 ? WarmSmall : rect.Width <= 72 ? WarmSmall + 1 : rect.Width <= 104 ? WarmSmall + 2 : WarmSmall + 3;
						break;
				}

				var x = rect.X + rect.Width / 2f;
				var y = rect.Y + rect.Height * 0.62f;
				Draw(renderer, sequence.GetSprite(fallback), x, y, 1f, strength * (fallback == Furnace ? 0.7f : 1f));
			}

			EndFrame();
		}

		// SpriteRenderer positions a sprite by its top-left corner, so centre it explicitly.
		static void Draw(SpriteRenderer renderer, Sprite sprite, float cx, float cy, float scale, float alpha)
		{
			var x = cx - (int)(0.5f * scale * sprite.Size.X);
			var y = cy - (int)(0.5f * scale * sprite.Size.Y);
			renderer.DrawSprite(sprite, null, new Vector3(x, y, cy), scale, Vector3.One, alpha);
		}

		void DrawLamps(WorldRenderer wr, SpriteRenderer renderer, float intensity)
		{
			if (roads == null)
				return;

			var spacing = Math.Max(1, info.LampSpacing);
			var strength = intensity * info.LampStrength;
			var map = world.Map;
			var sprite = sequence.GetSprite(Lamp);
			foreach (var pp in wr.Viewport.AllVisibleCells)
			{
				var cell = new MPos(pp.U, pp.V).ToCPos(map);
				if ((cell.X + cell.Y) % spacing != 0 || !roads.IsRoad(cell))
					continue;

				var pos = wr.ScreenPosition(map.CenterOfCell(cell));
				Draw(renderer, sprite, pos.X, pos.Y, 1f, strength);
			}
		}
	}
}
