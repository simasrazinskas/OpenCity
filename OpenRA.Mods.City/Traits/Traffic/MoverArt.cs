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
using OpenRA.Graphics;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>One LIFE iso vehicle model (image = model name, ART's export; sequences/vehicles.yaml). 8 facings clockwise from world north.</summary>
	public sealed class MoverModel
	{
		public string Name;
		public ISpriteSequence Body, Lit, Beam, Alert, AlertLit, Shadow;

		/// <summary>Body colour sequences (cars and vans), or null.</summary>
		public ISpriteSequence[] Colours;

		/// <summary>Body variants: colours, else frames per facing (rotor and propeller frames).</summary>
		public int Variants = 1;

		/// <summary>Length along the travel direction in cells (queue spacing, consist spacing).</summary>
		public float Length = 0.42f;
	}

	/// <summary>
	/// Render-only helpers shared by every mover (traffic, transit, service vehicles, helicopters): art lookup, the mapping from the
	/// simulation's legacy image names to the iso models, whole-pixel snapping in the iso projection and the night light layers.
	/// Nothing here reads or writes synced state.
	/// </summary>
	public sealed class MoverArt
	{
		// Straight runs move in steps of 1/16 cell (64 world units): exactly 2 px across and 1 px down at zoom 1.
		const float StraightStep = 16f;

		public static readonly string[] PrivateCars = ["hatchback", "sedan", "estate", "suv", "mpv", "pickup", "sports"];
		static readonly string[] Trucks = ["box-truck", "delivery-van", "tanker-truck", "tipper-truck", "timber-truck", "semi-tractor"];
		static readonly string[] Trailers = ["trailer-box", "trailer-container", "trailer-flat", "trailer-tank"];
		static readonly string[] Vans = ["delivery-van", "postal-van", "maintenance"];

		// Model lengths in cells (NET lane contract: cars 0.42 x 0.2 cells).
		static readonly Dictionary<string, float> Lengths = new()
		{
			{ "bus", 0.78f }, { "artic-bus-front", 0.55f }, { "artic-bus-rear", 0.5f }, { "box-truck", 0.6f }, { "tanker-truck", 0.62f },
			{ "tipper-truck", 0.6f }, { "timber-truck", 0.62f }, { "semi-tractor", 0.3f }, { "trailer-box", 0.58f }, { "trailer-container", 0.58f },
			{ "trailer-flat", 0.58f }, { "trailer-tank", 0.58f }, { "fire-engine", 0.68f }, { "garbage-truck", 0.6f }, { "tram-cab", 0.6f },
			{ "tram-middle", 0.5f }, { "tram-rear", 0.6f }, { "train-loco", 0.72f }, { "train-coach", 0.72f }, { "metro-cab", 0.68f },
			{ "metro-car", 0.68f }, { "delivery-van", 0.46f }, { "postal-van", 0.46f }, { "maintenance", 0.46f }, { "ambulance", 0.5f },
			{ "hearse", 0.5f }, { "snowplough", 0.55f }, { "farm-tractor", 0.4f },
		};

		// Colour sequences of cars and vans in ART's export (idle = red).
		static readonly string[] BodyColours = ["red", "blue", "white", "black", "silver", "green", "yellow", "teal", "brown", "orange", "purple", "beige"];

		readonly SequenceSet sequences;
		readonly Dictionary<string, MoverModel> models = [];

		public MoverArt(World world)
		{
			sequences = world.Map.Sequences;
		}

		/// <summary>The model, or null when the iso art is not available (callers fall back to the legacy sprite).</summary>
		public MoverModel Get(string model)
		{
			if (models.TryGetValue(model, out var m))
				return m;

			var image = model;
			if (sequences.HasSequence(image, "idle"))
			{
				m = new MoverModel
				{
					Name = model,
					Body = sequences.GetSequence(image, "idle"),
					Lit = Optional(image, "idle-lit") ?? Optional(image, "lit"),
					Beam = Optional(image, "beam"),
					Alert = Optional(image, "siren") ?? Optional(image, "beacon"),
					AlertLit = Optional(image, "siren-lit") ?? Optional(image, "beacon-lit"),
					Shadow = Optional(image, "shadow"),
				};

				if (sequences.HasSequence(image, BodyColours[0]))
					m.Colours = Array.ConvertAll(BodyColours, c => Optional(image, c) ?? m.Body);

				m.Variants = m.Colours?.Length ?? Math.Max(1, m.Body.Length);
				m.Length = Lengths.TryGetValue(model, out var len) ? len : 0.42f;
			}

			models[model] = m;
			return m;
		}

		ISpriteSequence Optional(string image, string sequence)
		{
			return sequences.HasSequence(image, sequence) ? sequences.GetSequence(image, sequence) : null;
		}

		/// <summary>
		/// Iso model for a legacy image name used by the simulation rules (car-a, truck, bus, ...). `hash` picks a model and colour
		/// deterministically per vehicle. Returns null for unknown names. `trailer` is set for articulated lorries.
		/// </summary>
		public MoverModel ForLegacy(string image, int hash, out int variant, out MoverModel trailer)
		{
			trailer = null;
			variant = 0;
			if (image == null)
				return null;

			var h = (uint)hash;
			string model;
			if (image.StartsWith("car-", StringComparison.Ordinal))
				model = PrivateCars[h % (uint)PrivateCars.Length];
			else
			{
				model = image switch
				{
					"truck" => Trucks[h % (uint)Trucks.Length],
					"van" => Vans[h % (uint)Vans.Length],
					"bus" => "bus",
					"garbage" => "garbage-truck",
					"ambulance" => "ambulance",
					"firetruck" => "fire-engine",
					"policecar" => "police",
					"hearse" => "hearse",
					"taxi" => "taxi",
					"tram" => "tram-cab",
					_ => image,
				};
			}

			var m = Get(model);
			if (m == null)
				return null;

			variant = (int)(h / 7 % (uint)m.Variants);
			if (model == "semi-tractor")
				trailer = Get(Trailers[h / 13 % (uint)Trailers.Length]);

			return m;
		}

		/// <summary>Pose at progress t (0 = entry edge, 1 = exit edge) of a cell entered heading `h` and left heading `r`, `offset` right of the travel direction.</summary>
		public static void CellPose(WPos center, int h, int r, float t, int offset, out WPos pos, out WAngle facing)
		{
			var din = CityUtils.Neighbours4[h];
			var dout = CityUtils.Neighbours4[r];
			float px, py, tx, ty;
			if (h == r)
			{
				// Straight: lane line from edge to edge.
				t = MathF.Round(t * StraightStep) / StraightStep;
				var along = (t - 0.5f) * 1024f;
				px = din.X * along - din.Y * offset;
				py = din.Y * along + din.X * offset;
				tx = din.X;
				ty = din.Y;
			}
			else if (din.X == -dout.X && din.Y == -dout.Y)
			{
				// U-turn (dead end): drive in to the cell centre, swing over to the opposite lane and back out through the same edge.
				var lateral = offset >= 0 ? Math.Max(64, offset) : Math.Min(-64, offset);
				var a = t * MathF.PI;
				var rx = -din.Y;
				var ry = din.X;
				var along = 512f * (MathF.Sin(a) - 1f);
				var side = lateral * MathF.Cos(a);
				px = din.X * along + rx * side;
				py = din.Y * along + ry * side;
				tx = din.X * 512f * MathF.Cos(a) - rx * lateral * MathF.Sin(a);
				ty = din.Y * 512f * MathF.Cos(a) - ry * lateral * MathF.Sin(a);
			}
			else
			{
				// Turn: quarter arc around the corner shared by the entry and exit edges.
				var kx = (dout.X - din.X) * 512f;
				var ky = (dout.Y - din.Y) * 512f;

				// The lane offset points away from the corner on a left turn and towards it on a right turn.
				var outward = -din.Y * -dout.X + din.X * -dout.Y;
				var radius = 512f + offset * outward;
				var a = t * MathF.PI * 0.5f;
				var c = MathF.Cos(a);
				var s = MathF.Sin(a);
				px = kx + radius * (-dout.X * c + din.X * s);
				py = ky + radius * (-dout.Y * c + din.Y * s);
				tx = din.X * c + dout.X * s;
				ty = din.Y * c + dout.Y * s;
			}

			pos = SnapToPixel(new WPos(center.X + (int)px, center.Y + (int)py, 0));
			facing = new WVec((int)(tx * 1024), (int)(ty * 1024), 0).Yaw;
		}

		/// <summary>
		/// Pose along a path through cell centres (transit legs, trains). `p` runs from 0 (centre of cells[0]) to cells.Length - 1; turns use
		/// the same quarter arcs as road traffic, `offset` is right of the travel direction. Non-adjacent steps are interpolated straight.
		/// </summary>
		public static void CellPathPose(Map map, CPos[] cells, float p, int offset, out WPos pos, out WAngle facing)
		{
			var n = cells.Length;
			p = Math.Clamp(p, 0f, n - 1);
			var k = Math.Clamp((int)MathF.Floor(p + 0.5f), 0, n - 1);
			var t = p + 0.5f - k;
			var hIn = k > 0 ? DirIndex(cells[k] - cells[k - 1]) : n > 1 ? DirIndex(cells[1] - cells[0]) : 0;
			var hOut = k < n - 1 ? DirIndex(cells[k + 1] - cells[k]) : hIn;
			if (hIn < 0 || hOut < 0)
			{
				var i = Math.Min(n - 2, (int)p);
				var a = map.CenterOfCell(cells[i]);
				var b = map.CenterOfCell(cells[i + 1]);
				pos = SnapToPixel(WPos.Lerp(a, b, (int)((p - i) * 1024), 1024));
				facing = (b - a).Yaw;
				return;
			}

			CellPose(map.CenterOfCell(cells[k]), hIn, hOut, t, offset, out pos, out facing);
		}

		static int DirIndex(CVec d)
		{
			for (var i = 0; i < 4; i++)
				if (CityUtils.Neighbours4[i] == d)
					return i;

			return -1;
		}

		/// <summary>
		/// Snaps a world position to whole screen pixels of the 2:1 iso projection at zoom 1 (sx = (X - Y) / 32, sy = (X + Y) / 64 - Z / 32).
		/// Cell centres are whole pixels, so every mover lands on the pixel grid and diagonal motion forms clean 2:1 stairs.
		/// </summary>
		public static WPos SnapToPixel(WPos p)
		{
			var u = RoundTo(p.X - p.Y, 32);
			var v = RoundTo(p.X + p.Y, 64);
			return new WPos((u + v) / 2, (v - u) / 2, RoundTo(p.Z, 32));
		}

		static int RoundTo(int value, int step)
		{
			return (int)Math.Round(value / (double)step, MidpointRounding.AwayFromZero) * step;
		}

		/// <summary>
		/// Ambient multiply for sorted mover sprites (BLD's CityAtmosphere.SpriteTint): white while the night tint post-process runs after
		/// the actors, the ambient colour once it runs before them (ENGINE-PLAN 3.5). The -lit layers skip it and stay bright.
		/// </summary>
		public static Vector3 Ambient(CityAtmosphere atmosphere)
		{
			return atmosphere?.SpriteTint ?? Vector3.One;
		}

		/// <summary>0 by day, 1 at full night (vehicle lamps on).</summary>
		public static float Night(CityAtmosphere atmosphere)
		{
			return atmosphere == null ? 0f : Math.Clamp((atmosphere.Darkness - 0.35f) / 0.4f, 0f, 1f);
		}

		/// <summary>
		/// Adds the renderables of one mover: body (ambient tinted), alert frames (sirens/beacons, `alertFrame` &gt;= 0), and at night the
		/// lamp layer and the additive road beam, sorted with the body (ZOffset +1) so cars in front still hide them.
		/// </summary>
		public static void Draw(List<IRenderable> into, MoverModel m, WPos pos, WAngle facing, int variant, int alertFrame, float night,
			Vector3 ambient, PaletteReference palette, int zOffset = 0)
		{
			var sprite = alertFrame >= 0 && m.Alert != null ? m.Alert.GetSprite(alertFrame, facing)
				: m.Colours != null ? m.Colours[variant % m.Colours.Length].GetSprite(0, facing)
				: m.Body.GetSprite(variant, facing);
			into.Add(new SpriteRenderable(sprite, pos, WVec.Zero, zOffset, palette, 1f, 1f, ambient, TintModifiers.None, false));

			// Beacon lamps have one lit frame (on); sirens alternate their lit frames with the body frames.
			if (alertFrame >= 0 && m.AlertLit != null && alertFrame % Math.Max(1, m.Alert.Length) < m.AlertLit.Length)
				into.Add(new SpriteRenderable(m.AlertLit.GetSprite(alertFrame, facing), pos, WVec.Zero, zOffset + 1, palette, 1f, 1f, Vector3.One,
					TintModifiers.IgnoreWorldTint, false));

			if (night <= 0.3f)
				return;

			if (m.Lit != null)
				into.Add(new SpriteRenderable(m.Lit.GetSprite(0, facing), pos, WVec.Zero, zOffset + 1, palette, 1f, 1f, Vector3.One,
					TintModifiers.IgnoreWorldTint, false));

			if (m.Beam != null)
				into.Add(new SpriteRenderable(m.Beam.GetSprite(0, facing), pos, WVec.Zero, zOffset + 1, palette, 1f, 0.55f * night, Vector3.One,
					TintModifiers.IgnoreWorldTint, false));
		}
	}
}
