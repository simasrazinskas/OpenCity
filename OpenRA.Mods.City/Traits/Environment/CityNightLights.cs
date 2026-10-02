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
	[Desc("Render-only night lighting policy. Buildings show their authored '-lit' frames (drawn by WithIsoSprite, sorted with",
		"the building so whatever stands in front hides them); this trait decides which buildings are lit and how bright,",
		"and draws the street lamp light pools (roadprops lamp-pool) on the ground before the actors. Unpowered buildings",
		"stay dark.")]
	public class CityNightLightsInfo : TraitInfo, NotBefore<RoadLayerInfo>
	{
		[Desc("Darkness (0..1) at which the lights start to fade in.")]
		public readonly float MinDarkness = 0.22f;

		public readonly float LampStrength = 0.6f;

		[Desc("Without lamp props (CityPropLayer), a lamp pool lies on every road cell whose x + y is a multiple of this.")]
		public readonly int LampSpacing = 3;

		[Desc("Percent of buildings with lit windows for each hour of the day (24 values).")]
		public readonly int[] LitPercentByHour =
		[
			45, 35, 30, 30, 30, 30, 22, 10, 5, 0, 0, 0,
			0, 0, 0, 0, 55, 72, 86, 92, 92, 86, 72, 58
		];

		public override object Create(ActorInitializer init) { return new CityNightLights(init.Self, this); }
	}

	public class CityNightLights : IRenderOverlay, IRenderAboveWorld, IWorldLoaded
	{
		readonly CityNightLightsInfo info;
		readonly World world;
		CityAtmosphere atmosphere;
		IRoadNetwork roads;
		CityPropLayer props;

		public CityNightLights(Actor self, CityNightLightsInfo info)
		{
			this.info = info;
			world = self.World;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			props = w.WorldActor.TraitOrDefault<CityPropLayer>();
		}

		/// <summary>0..1 strength of every night light right now (lamps, lit windows).</summary>
		public float Intensity => atmosphere == null ? 0f
			: Math.Clamp((atmosphere.Darkness - info.MinDarkness) / (1f - info.MinDarkness), 0f, 1f);

		/// <summary>Alpha of the '-lit' frames of night props (street lamps, signs).</summary>
		public float LampAlpha => Intensity > 0.02f ? Intensity : 0f;

		/// <summary>
		/// Alpha (0 = dark) of a building's '-lit' frames: powered and operational buildings only, and only the share of
		/// buildings that have their lights on at this hour (stable per actor).
		/// </summary>
		public float LitAlpha(Actor actor, CityBuilding building)
		{
			if (building == null || !building.HasPower || !building.IsOperational)
				return 0f;

			var intensity = Intensity;
			if (intensity <= 0.02f)
				return 0f;

			var hour = (int)atmosphere.HourF % 24;
			var chance = Math.Min(100, info.LitPercentByHour[hour % info.LitPercentByHour.Length] + 30);
			return EnvHash.Hash((int)actor.ActorID, 31) % 100 < chance ? intensity : 0f;
		}

		bool Split => atmosphere != null && atmosphere.SplitAmbient;

		// Lamp pools lie on the ground: with the ambient split they are drawn before the actors and divided by the ground
		// tint (the tint pass multiplies them afterwards), so buildings and cars in front cover them.
		void IRenderOverlay.Render(WorldRenderer wr)
		{
			if (Split)
				DrawLamps(wr);
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (!Split)
				DrawLamps(wr);
		}

		// Street lights: a pool of light on every LampSpacing-th road cell (the RCT2 mockups light every street). Cells with
		// a lamp prop (road "lights" add-on) get their pool from CityPropLayer instead.
		void DrawLamps(WorldRenderer wr)
		{
			if (roads == null)
				return;

			var sprite = props?.LampPool;
			var intensity = Intensity;
			if (sprite == null || intensity <= 0.02f)
				return;

			var strength = intensity * info.LampStrength;
			if (Split)
			{
				var t = atmosphere.CurrentTint;
				strength = Math.Min(1f, strength / Math.Max(0.25f, (t.X + t.Y + t.Z) / 3f));
			}

			var renderer = Game.Renderer.WorldSpriteRenderer;
			var spacing = Math.Max(1, info.LampSpacing);
			var map = world.Map;
			foreach (var pp in wr.Viewport.AllVisibleCells)
			{
				var cell = new MPos(pp.U, pp.V).ToCPos(map);
				if ((cell.X + cell.Y) % spacing != 0 || !roads.IsRoad(cell))
					continue;

				if (props != null && props.HasLamp(cell))
					continue;

				// Screen3DPxPosition keeps the depth coordinate in the renderer's range (a raw screen y would be clipped).
				var pos = wr.Screen3DPxPosition(map.CenterOfCell(cell));
				var at = new Vector3(pos.X - (int)(0.5f * sprite.Size.X), pos.Y - (int)(0.5f * sprite.Size.Y), pos.Z);
				renderer.DrawSprite(sprite, null, at, 1f, Vector3.One, strength);
			}
		}
	}
}
