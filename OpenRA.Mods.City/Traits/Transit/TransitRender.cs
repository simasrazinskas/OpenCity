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
using System.Numerics;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	// Render-only state the UI may set (never synced, never read by the simulation).
	public sealed partial class TransitLayer
	{
		/// <summary>Line whose route is highlighted on the map (0 = none). Local UI state.</summary>
		public int HighlightLineId;

		/// <summary>Show the routes of all lines. Local UI state.</summary>
		public bool ShowAllLines;

		public static readonly Color[] LineColors =
		[
			Color.FromArgb(0xE0, 0x53, 0x4A), Color.FromArgb(0x3C, 0x8F, 0xE0), Color.FromArgb(0x4C, 0xB8, 0x5A), Color.FromArgb(0xF2, 0xB8, 0x4B),
			Color.FromArgb(0x9B, 0x59, 0xD0), Color.FromArgb(0x2F, 0xB8, 0xB0), Color.FromArgb(0xF2, 0x7F, 0x3C), Color.FromArgb(0xE0, 0x6B, 0xA8),
			Color.FromArgb(0x8E, 0xB8, 0x3C), Color.FromArgb(0x6B, 0x7B, 0xE8), Color.FromArgb(0xA8, 0x6B, 0x3C), Color.FromArgb(0x70, 0x78, 0x80),
		];

		/// <summary>A palette index (0..11) or a 24-bit RGB value (the UI line tool sends RGB).</summary>
		public static Color LineColor(int value)
		{
			if (value >= 0 && value < LineColors.Length)
				return LineColors[value];

			return Color.FromArgb((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
		}

		/// <summary>
		/// Where a vehicle is along its leg for rendering: the leg cells and a position from 0 (centre of the first cell) to
		/// cells.Length - 1. False when the vehicle should not be drawn by us (a traffic service draws it, or it is in the depot).
		/// </summary>
		public bool TryGetVehiclePath(TransitVehicle v, float now, out CPos[] cells, out float along)
		{
			cells = v.LegCells;
			along = 0f;
			switch (v.State)
			{
				case TransitVehicleState.Leg:
				case TransitVehicleState.ToDepot:
				case TransitVehicleState.ToPickup:
				case TransitVehicleState.Carrying:
					if (!v.Virtual || cells.Length == 0)
						return false;

					along = Math.Clamp(now - v.LegStartTick, 0f, v.LegTicks) * (cells.Length - 1) / Math.Max(1, v.LegTicks);
					return true;

				case TransitVehicleState.Dwell:
				case TransitVehicleState.Idle:
				case TransitVehicleState.WaitingPassenger:
					if (cells.Length >= 2 && cells[^1] == v.Cell)
						along = cells.Length - 1;
					else
						cells = [v.Cell];

					return true;

				default:
					return false;
			}
		}

		/// <summary>World position and facing of a vehicle on the road centre line (overlay markers).</summary>
		public bool TryGetVehiclePose(TransitVehicle v, int now, out WPos pos, out WAngle facing)
		{
			pos = WPos.Zero;
			facing = WAngle.Zero;
			if (!TryGetVehiclePath(v, now, out var cells, out var along) || cells.Length == 0)
				return false;

			MoverArt.CellPathPose(world.Map, cells, along, 0, out pos, out facing);
			return true;
		}
	}

	/// <summary>Draws stops (with waiting people), vehicles driven by the virtual timer (or standing at a stop) and the highlighted line routes.</summary>
	public sealed class TransitRender : IEffect, IEffectAnnotation
	{
		// Buses, taxis and trams keep to the right-hand street lane; tram tracks are centred there too (NET: +-10 q).
		const int RoadLane = 160;

		readonly TransitLayer layer;
		readonly World world;
		ISpriteSequence stopSequence, waitSequence;
		ISpriteSequence busSequence, taxiSequence, tramSequence, trainSequence, trainCarSequence;
		MoverArt art;
		CityAtmosphere atmosphere;
		readonly List<(WPos Pos, WAngle Facing)> trainPoses = [];
		readonly List<IRenderable> frame = [];
		int lastTick = -1;
		long lastTickTime;
		bool resolved;

		public TransitRender(TransitLayer layer, World world)
		{
			this.layer = layer;
			this.world = world;
		}

		void IEffect.Tick(World world) { }

		void Resolve()
		{
			resolved = true;
			var sequences = world.Map.Sequences;
			ISpriteSequence Seq(string image, string sequence = "idle") =>
				sequences.HasSequence(image, sequence) ? sequences.GetSequence(image, sequence) : null;

			stopSequence = Seq(layer.Info.StopImage);
			waitSequence = Seq("waiting");
			busSequence = Seq(layer.Info.BusImage);
			taxiSequence = Seq(layer.Info.TaxiImage);
			tramSequence = Seq(layer.Info.TramImage) ?? busSequence;
			trainSequence = Seq(layer.Info.TrainImage);
			trainCarSequence = Seq(layer.Info.TrainCarImage) ?? trainSequence;
			art = new MoverArt(world);
			atmosphere = world.WorldActor.TraitOrDefault<CityAtmosphere>();
		}

		// World tick plus the fraction of the next tick, so vehicles glide between ticks (render only).
		float RenderNow()
		{
			var tick = world.WorldTick;
			if (tick != lastTick)
			{
				lastTick = tick;
				lastTickTime = Game.RunTime;
			}

			var frac = world.Paused || world.Timestep <= 0 ? 0f : Math.Clamp((Game.RunTime - lastTickTime) / (float)world.Timestep, 0f, 1f);
			return tick + frac;
		}

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer wr)
		{
			if (!resolved)
				Resolve();

			frame.Clear();
			var palette = wr.Palette("city");
			var ambient = MoverArt.Ambient(atmosphere);
			var night = MoverArt.Night(atmosphere);
			var map = world.Map;
			var now = RenderNow();
			DrawStops(map, palette, ambient);

			var vehicles = layer.Vehicles;
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (v.Mode == TransitMode.Metro)
					continue;

				if (v.Mode == TransitMode.Train)
				{
					if (v.State != TransitVehicleState.Gone && v.State != TransitVehicleState.Idle)
						DrawTrain(v, now, palette, ambient, night);

					continue;
				}

				if (!layer.TryGetVehiclePath(v, now, out var cells, out var along) || cells.Length == 0)
					continue;

				MoverArt.CellPathPose(map, cells, along, RoadLane, out var pos, out var facing);
				MoverModel a, b = null, c = null;
				if (v.Mode == TransitMode.Tram)
				{
					a = art.Get("tram-cab");
					b = art.Get("tram-middle");
					c = art.Get("tram-rear");
				}
				else if (v.Mode == TransitMode.Taxi)
					a = art.Get("taxi");
				else if (v.Id % 3 == 0 && art.Get("artic-bus-front") != null)
				{
					a = art.Get("artic-bus-front");
					b = art.Get("artic-bus-rear");
				}
				else
					a = art.Get("bus");

				if (a == null)
				{
					var seq = v.Mode == TransitMode.Taxi ? taxiSequence : (v.Mode == TransitMode.Tram ? tramSequence : busSequence);
					if (seq != null)
						frame.Add(new SpriteRenderable(seq.GetSprite(0, facing), pos, WVec.Zero, 0, palette, 1f, 1f, ambient, TintModifiers.None, false));

					continue;
				}

				MoverArt.Draw(frame, a, pos, facing, v.LineId % Math.Max(1, a.Variants), -1, night, ambient, palette);
				var back = 0f;
				var prev = a.Length;
				foreach (var seg in new[] { b, c })
				{
					if (seg == null)
						break;

					back += prev * 0.5f + seg.Length * 0.5f + 0.02f;
					MoverArt.CellPathPose(map, cells, along - back, RoadLane, out var sp, out var sf);
					MoverArt.Draw(frame, seg, sp, sf, 0, -1, night, ambient, palette);
					prev = seg.Length;
				}
			}

			if (TrafficSim.Showcase)
				DrawShowcase(wr, palette, ambient, night);

			return frame;
		}

		// Render-only showcase for headless screenshots (CityAutoTest movers=demo): a train and a tram running along a winding road
		// path near the view centre, to check consists on curves without building a rail network.
		void DrawShowcase(WorldRenderer wr, PaletteReference palette, Vector3 ambient, float night)
		{
			var roads = world.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			if (roads == null)
				return;

			var map = world.Map;
			var center = map.CellContaining(wr.Viewport.CenterPosition);
			for (var n = 0; n < 2; n++)
			{
				var path = ShowcasePath(roads, center + new CVec(n * 5 - 2, n * 3 - 1), 18 + n);
				if (path.Length < 4)
					continue;

				var along = Game.RunTime / 1000f * 0.9f % (path.Length - 1);
				MoverModel[] models = n == 0
					? [art.Get("train-loco"), art.Get("train-coach"), art.Get("train-coach"), art.Get("train-coach")]
					: [art.Get("tram-cab"), art.Get("tram-middle"), art.Get("tram-rear")];
				var back = 0f;
				for (var k = 0; k < models.Length; k++)
				{
					if (models[k] == null)
						continue;

					if (k > 0)
						back += n == 0 ? TransitLayer.TrainUnitSpacing : models[k - 1].Length * 0.5f + models[k].Length * 0.5f + 0.02f;

					MoverArt.CellPathPose(map, path, along - back, n == 0 ? 0 : RoadLane, out var pos, out var facing);
					MoverArt.Draw(frame, models[k], pos, facing, 0, -1, k == 0 ? night : 0f, ambient, palette);
				}
			}
		}

		static CPos[] ShowcasePath(IRoadNetwork roads, CPos near, int length)
		{
			var start = CPos.Zero;
			for (var r = 0; r < 8 && start == CPos.Zero; r++)
				for (var dy = -r; dy <= r && start == CPos.Zero; dy++)
					for (var dx = -r; dx <= r && start == CPos.Zero; dx++)
						if (roads.IsRoad(near + new CVec(dx, dy)))
							start = near + new CVec(dx, dy);

			if (start == CPos.Zero)
				return [];

			var path = new List<CPos> { start };
			var dir = -1;
			for (var i = 1; i < length; i++)
			{
				var c = path[^1];
				var next = -1;
				for (var k = 0; k < 4; k++)
				{
					// Prefer turning every few cells so the path has curves.
					var d = (i / 4 + k) & 3;
					if (dir >= 0 && d == ((dir + 2) & 3))
						continue;

					var to = c + CityUtils.Neighbours4[d];
					if (roads.IsRoad(to) && !path.Contains(to))
					{
						next = d;
						break;
					}
				}

				if (next < 0)
					break;

				dir = next;
				path.Add(c + CityUtils.Neighbours4[next]);
			}

			return path.ToArray();
		}

		void DrawStops(Map map, PaletteReference palette, Vector3 ambient)
		{
			var stops = layer.Stops;
			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				if (s.StationActorId != 0)
					continue;

				var center = map.CenterOfCell(s.Cell);
				if (stopSequence != null)
				{
					var stopFrame = Math.Min(stopSequence.Length - 1, (s.Mode == TransitMode.Taxi ? 4 : 0) + s.Side);
					frame.Add(new SpriteRenderable(stopSequence.GetSprite(stopFrame), center, WVec.Zero, 0, palette, 1f, 1f, ambient, TintModifiers.None, false));
				}

				// Waiting passengers stand on the sidewalk of the stop's side, facing the road.
				if (waitSequence == null || s.WaitingCount <= 0)
					continue;

				var side = CityUtils.Neighbours4[Math.Clamp(s.Side, 0, 3)];
				var facing = new WVec(-side.X, -side.Y, 0).Yaw;
				var people = Math.Min(5, (s.WaitingCount + 2) / 3);
				for (var k = 0; k < people; k++)
				{
					var along = (k - (people - 1) * 0.5f) * 160;
					var pos = center + new WVec(side.X * 416 - side.Y * (int)along, side.Y * 416 + side.X * (int)along, 0);
					var f = (world.WorldTick / 20 + k * 7 + s.Id) % Math.Max(1, waitSequence.Length);
					frame.Add(new SpriteRenderable(waitSequence.GetSprite(f, facing), MoverArt.SnapToPixel(pos), WVec.Zero, 0, palette, 1f, 1f, ambient,
						TintModifiers.None, false));
				}
			}
		}

		void DrawTrain(TransitVehicle v, float now, PaletteReference palette, Vector3 ambient, float night)
		{
			var units = v.Intercity ? 6 : 4;
			trainPoses.Clear();
			layer.GetTrainPoses(v, now, units, trainPoses);
			var loco = art.Get("train-loco");
			var coach = art.Get("train-coach") ?? loco;
			for (var k = 0; k < trainPoses.Count; k++)
			{
				var (pos, facing) = trainPoses[k];
				if (loco != null)
				{
					// Intercity trains are push-pull: a second loco at the tail, facing backwards.
					var tail = v.Intercity && k == trainPoses.Count - 1;
					var model = k == 0 || tail ? loco : coach;
					MoverArt.Draw(frame, model, pos, tail ? facing + new WAngle(512) : facing, 0, -1, k == 0 || tail ? night : 0f, ambient, palette);
					continue;
				}

				var unit = k == 0 ? trainSequence : trainCarSequence;
				if (unit != null)
					frame.Add(new SpriteRenderable(unit.GetSprite(0, facing), pos, WVec.Zero, 0, palette, 1f, 1f, ambient, TintModifiers.None, false));
			}
		}

		IEnumerable<IRenderable> IEffectAnnotation.RenderAnnotation(WorldRenderer wr)
		{
			// Underground trains are only visible on the transit overlay.
			var now = world.WorldTick;
			var vehicles = layer.Vehicles;
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (v.Mode != TransitMode.Metro || (!layer.ShowAllLines && v.LineId != layer.HighlightLineId))
					continue;

				if (layer.TryGetVehiclePose(v, now, out var pos, out _))
					yield return new MarkerTileRenderable(world.Map.CellContaining(pos), Color.FromArgb(230, 255, 255, 255));
			}

			var lines = layer.Lines;
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				if (!layer.ShowAllLines && line.Id != layer.HighlightLineId)
					continue;

				var c = TransitLayer.LineColor(line.Color);
				var tint = Color.FromArgb(line.Id == layer.HighlightLineId ? 130 : 80, c.R, c.G, c.B);
				for (var k = 0; k < line.Legs.Count; k++)
				{
					var cells = line.Legs[k].Cells;
					for (var m = 0; m < cells.Length; m++)
						yield return new MarkerTileRenderable(cells[m], tint);
				}
			}
		}
	}
}
