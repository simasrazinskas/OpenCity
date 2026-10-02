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

namespace OpenRA.Mods.City.Traits
{
	// Render-only mover showcase for headless screenshots (CityAutoTest "movers=demo"): hash-driven synthetic traffic on every visible
	// road link, with every vehicle model, turns through junctions and full queues, laid out by the same lane, arc and fit rules as the
	// real traffic. Never touches synced state.
	public sealed partial class TrafficSim
	{
		/// <summary>Local test switch: draw synthetic traffic on every visible road (screenshots only).</summary>
		public static bool Showcase;

		static readonly string[] ShowcaseModels =
		[
			"car", "car", "car", "car", "car", "car", "car", "car", "taxi", "bus", "artic", "tram", "delivery-van", "box-truck", "semi",
			"tanker-truck", "garbage-truck", "fire-engine", "police", "ambulance", "postal-van", "maintenance", "hearse", "snowplough",
		];

		readonly List<(float S, int Hash, int Exit)> showcaseQueue = [];

		void DrawShowcase(int x0, int y0, int x1, int y1, int renderU)
		{
			var time = renderU / (float)U;
			for (var y = y0; y <= y1; y++)
			{
				for (var x = x0; x <= x1; x++)
				{
					var cell = y * width + x;
					if (roadFlag[cell] == 0)
						continue;

					for (var h = 0; h < 4; h++)
					{
						var from = new CPos(x, y) - CityUtils.Neighbours4[h];
						if (!InMap(from) || roadFlag[Cell(from)] == 0 || !net.CanEnter(from, h))
							continue;

						DrawShowcaseLink(cell, x, y, h, time);
					}
				}
			}
		}

		void DrawShowcaseLink(int cell, int cx, int cy, int h, float time)
		{
			var profile = ProfileOfCell(cell);
			var center = map.CenterOfCell(new CPos(cx, cy));
			var seed = Hash(cell, h + 101);
			var queued = (uint)seed % 7 == 0;
			for (var lane = 0; lane < profile.Lanes.Length; lane++)
			{
				showcaseQueue.Clear();
				var n = queued ? 3 : (int)((uint)Hash(seed, lane) % 3 / 2);
				for (var i = 0; i < n; i++)
				{
					var vh = Hash(seed, lane * 8 + i + 1);
					var s = queued ? 1f : (time * 0.015f + (vh & 0xffff) / 65536f) % 1f;
					showcaseQueue.Add((s, vh, ShowcaseExit(cell, cx, cy, h, vh)));
				}

				showcaseQueue.Sort((a, b) => b.S.CompareTo(a.S));
				var limit = 1f;
				foreach (var (s, vh, exit) in showcaseQueue)
				{
					var look = ShowcaseLook(vh);
					if (look.A == null)
						continue;

					var pos = Math.Min(s, limit);
					limit = pos - look.Length - QueueGap;
					if (pos < s - 0.001f && pos - look.A.Length * 0.5f < 0f)
						continue;

					var offset = profile.Lanes[lane];
					PathPose(center, h, exit, pos, offset, out var wpos, out var facing);
					var alert = look.A.Alert != null ? ((int)(time / 3f) + vh) & 1 : -1;
					MoverArt.Draw(frame, look.A, wpos, facing, look.Variant, alert, night, ambient, palette);

					var back = 0f;
					var prevLength = look.A.Length;
					foreach (var seg in new[] { look.B, look.C })
					{
						if (seg == null)
							break;

						back += prevLength * 0.5f + seg.Length * 0.5f + 0.02f;
						PathPose(center, h, exit, Math.Max(0f, pos - back), offset, out var sp, out var sf);
						MoverArt.Draw(frame, seg, sp, sf, look.Variant % seg.Variants, -1, night, ambient, palette);
						prevLength = seg.Length;
					}
				}
			}
		}

		int ShowcaseExit(int cell, int cx, int cy, int h, int hash)
		{
			var options = 0;
			Span<int> exits = stackalloc int[4];
			for (var d = 0; d < 4; d++)
			{
				var to = new CPos(cx, cy) + CityUtils.Neighbours4[d];
				if (d != ((h + 2) & 3) && InMap(to) && roadFlag[Cell(to)] != 0 && net.CanEnter(new CPos(cx, cy), d))
					exits[options++] = d;
			}

			_ = cell;
			return options == 0 ? (h + 2) & 3 : exits[(int)((uint)hash % (uint)options)];
		}

		Look ShowcaseLook(int hash)
		{
			var h = (uint)hash;
			var kind = ShowcaseModels[h % (uint)ShowcaseModels.Length];
			var look = default(Look);
			switch (kind)
			{
				case "car":
					look.A = art.ForLegacy("car-a", (int)(h >> 5), out look.Variant, out _);
					break;
				case "semi":
					look.A = art.ForLegacy("truck", 5, out look.Variant, out look.B);
					break;
				case "artic":
					look.A = art.Get("artic-bus-front");
					look.B = art.Get("artic-bus-rear");
					break;
				case "tram":
					look.A = art.Get("tram-cab");
					look.B = art.Get("tram-middle");
					look.C = art.Get("tram-rear");
					break;
				default:
					look.A = art.Get(kind);
					break;
			}

			if (look.A != null)
				look.Length = look.A.Length + (look.B?.Length ?? 0f) + (look.C?.Length ?? 0f);

			return look;
		}
	}
}
