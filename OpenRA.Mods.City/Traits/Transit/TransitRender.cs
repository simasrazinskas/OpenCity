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

		/// <summary>World position and facing of a vehicle for rendering. False when the vehicle should not be drawn by us (a traffic service draws it, or it is in the depot).</summary>
		public bool TryGetVehiclePose(TransitVehicle v, int now, out WPos pos, out WAngle facing)
		{
			pos = WPos.Zero;
			facing = WAngle.Zero;
			var map = world.Map;
			switch (v.State)
			{
				case TransitVehicleState.Leg:
				case TransitVehicleState.ToDepot:
				case TransitVehicleState.ToPickup:
				case TransitVehicleState.Carrying:
				{
					if (!v.Virtual || v.LegCells.Length == 0)
						return false;

					var cells = v.LegCells;
					if (cells.Length == 1)
					{
						pos = map.CenterOfCell(cells[0]);
						return true;
					}

					var t = (long)Math.Clamp(now - v.LegStartTick, 0, v.LegTicks) * (cells.Length - 1) * 1024 / Math.Max(1, v.LegTicks);
					var i = Math.Min(cells.Length - 2, (int)(t / 1024));
					var frac = (int)(t - i * 1024L);
					var a = map.CenterOfCell(cells[i]);
					var b = map.CenterOfCell(cells[i + 1]);
					pos = WPos.Lerp(a, b, frac, 1024);
					facing = (b - a).Yaw;
					return true;
				}

				case TransitVehicleState.Dwell:
				case TransitVehicleState.Idle:
				case TransitVehicleState.WaitingPassenger:
				{
					pos = map.CenterOfCell(v.Cell);
					if (v.LegCells.Length >= 2)
						facing = (map.CenterOfCell(v.LegCells[^1]) - map.CenterOfCell(v.LegCells[^2])).Yaw;

					return true;
				}

				default:
					return false;
			}
		}
	}

	/// <summary>Draws stops, vehicles driven by the virtual timer (or standing at a stop) and the highlighted line routes.</summary>
	public sealed class TransitRender : IEffect, IEffectAnnotation
	{
		readonly TransitLayer layer;
		readonly World world;
		ISpriteSequence stopSequence;
		ISpriteSequence busSequence;
		ISpriteSequence taxiSequence;
		ISpriteSequence tramSequence;
		ISpriteSequence trainSequence;
		ISpriteSequence trainCarSequence;
		readonly List<(WPos Pos, WAngle Facing)> trainPoses = [];
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
			if (sequences.HasSequence(layer.Info.StopImage, "idle"))
				stopSequence = sequences.GetSequence(layer.Info.StopImage, "idle");

			if (sequences.HasSequence(layer.Info.BusImage, "idle"))
				busSequence = sequences.GetSequence(layer.Info.BusImage, "idle");

			if (sequences.HasSequence(layer.Info.TaxiImage, "idle"))
				taxiSequence = sequences.GetSequence(layer.Info.TaxiImage, "idle");

			tramSequence = sequences.HasSequence(layer.Info.TramImage, "idle") ? sequences.GetSequence(layer.Info.TramImage, "idle") : busSequence;
			if (sequences.HasSequence(layer.Info.TrainImage, "idle"))
				trainSequence = sequences.GetSequence(layer.Info.TrainImage, "idle");

			trainCarSequence = sequences.HasSequence(layer.Info.TrainCarImage, "idle") ? sequences.GetSequence(layer.Info.TrainCarImage, "idle") : trainSequence;
		}

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer wr)
		{
			if (!resolved)
				Resolve();

			var palette = wr.Palette("city");
			var map = world.Map;
			var stops = layer.Stops;
			if (stopSequence != null)
			{
				for (var i = 0; i < stops.Count; i++)
				{
					var s = stops[i];
					if (s.StationActorId != 0)
						continue;

					var frame = Math.Min(stopSequence.Length - 1, (s.Mode == TransitMode.Taxi ? 4 : 0) + s.Side);
					yield return new SpriteRenderable(stopSequence.GetSprite(frame), map.CenterOfCell(s.Cell), WVec.Zero, 2, palette,
						1f, 1f, Vector3.One, TintModifiers.None, false);
				}
			}

			var now = world.WorldTick;
			var vehicles = layer.Vehicles;
			for (var i = 0; i < vehicles.Count; i++)
			{
				var v = vehicles[i];
				if (v.Mode == TransitMode.Metro)
					continue;

				if (v.Mode == TransitMode.Train)
				{
					if (trainSequence == null || v.State == TransitVehicleState.Gone || v.State == TransitVehicleState.Idle)
						continue;

					trainPoses.Clear();
					layer.GetTrainPoses(v, now, v.Intercity ? 4 : 3, trainPoses);
					for (var k = 0; k < trainPoses.Count; k++)
					{
						var unit = k == 0 ? trainSequence : trainCarSequence;
						yield return new SpriteRenderable(unit.GetSprite(0, trainPoses[k].Facing), trainPoses[k].Pos, WVec.Zero, 3, palette,
							1f, 1f, Vector3.One, TintModifiers.None, false);
					}

					continue;
				}

				var seq = v.Mode == TransitMode.Taxi ? taxiSequence : (v.Mode == TransitMode.Tram ? tramSequence : busSequence);
				if (seq == null || !layer.TryGetVehiclePose(v, now, out var pos, out var facing))
					continue;

				// Drive on the right: shift sideways like WithLaneOffset does for actors.
				var forward = new WVec(0, -1024, 0).Rotate(WRot.FromYaw(facing));
				var lane = new WVec(-forward.Y, forward.X, 0) * 192 / 1024;
				yield return new SpriteRenderable(seq.GetSprite(0, facing), pos + lane, WVec.Zero, 4, palette, 1f, 1f, Vector3.One, TintModifiers.None, false);
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
