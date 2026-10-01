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
	// Lot planning (design/04 3.2 and 3.3): frontage run -> largest shape that fits -> reserve -> spawn.
	public partial class ZoneGrowth
	{
		struct Plan
		{
			public ZoneType Zone;
			public string Actor;
			public CPos Origin;
			public int Cx, Cy;
			public int Score;
			public int Level;
		}

		static bool IsHighDensity(ZoneType zone)
		{
			return zone == ZoneType.ResidentialHigh || zone == ZoneType.ResidentialLowRent || zone == ZoneType.ResidentialMixed
				|| zone == ZoneType.CommercialHigh || zone == ZoneType.OfficeHigh || zone == ZoneType.Office;
		}

		const int SpanCap = 7;

		void TrySpawn(ZoneType zone, List<Anchor> list)
		{
			var best = default(Plan);
			var have = false;
			var found = 0;
			for (var tries = 0; tries < Info.Candidates * 4 && found < Info.Candidates && list.Count > 0; tries++)
			{
				var index = world.SharedRandom.Next(list.Count);
				var anchor = list[index];
				if (!Usable(anchor.Cell, zone) || !PlanLot(zone, anchor, out var plan))
				{
					// Re-added by the next rebuild if it becomes usable again.
					list[index] = list[^1];
					list.RemoveAt(list.Count - 1);
					rejected++;
					continue;
				}

				var lv = LandValueAt(anchor.Cell, zone);
				plan.Score = (IsHighDensity(zone) ? lv / 2 : 0) + world.SharedRandom.Next(16);
				found++;
				if (!have || plan.Score > best.Score)
				{
					best = plan;
					have = true;
				}
			}

			if (have)
				Spawn(best);
		}

		bool FrontOk(CPos cell, int side, ZoneType zone)
		{
			if (!Usable(cell, zone))
				return false;

			var road = cell + CityUtils.Neighbours4[side];
			return zones.IsFrontageRoad(road) && roads.IsConnectedToOutside(road);
		}

		bool PlanLot(ZoneType zone, Anchor a, out Plan plan)
		{
			plan = default;
			var shapes = catalog.Shapes(zone);
			var side = a.Side;
			var axis = side % 2 == 0 ? new CVec(1, 0) : new CVec(0, 1);
			var step = CityUtils.Neighbours4[side];
			var inward = new CVec(-step.X, -step.Y);
			var reach = Info.RunReach;

			// Frontage run: contiguous usable cells along the road that all touch a connected road.
			var lo = 0;
			while (lo > -reach && FrontOk(a.Cell + new CVec(axis.X * (lo - 1), axis.Y * (lo - 1)), side, zone))
				lo--;

			var hi = 0;
			while (hi < reach && FrontOk(a.Cell + new CVec(axis.X * (hi + 1), axis.Y * (hi + 1)), side, zone))
				hi++;

			// Which lot depths each column of the run allows. A lot never leaves a sliver too thin for the narrowest shape between
			// itself and the road on the far side of the block: a block 4 deep is split 2 + 2, 5 deep 3 + 2, 6 deep 3 + 3.
			var minDepth = catalog.MinDepth(zone);
			Span<byte> depth = stackalloc byte[2 * 12 + 1];
			for (var t = lo; t <= hi; t++)
			{
				var c = a.Cell + new CVec(axis.X * t, axis.Y * t);
				var span = 0;
				while (span < SpanCap && Usable(c + new CVec(inward.X * span, inward.Y * span), zone))
					span++;

				var endsAtRoad = span < SpanCap && zones.IsFrontageRoad(c + new CVec(inward.X * span, inward.Y * span));
				byte mask = 0;
				for (var d = 1; d <= Math.Min(span, LotCatalog.MaxDepth); d++)
				{
					var rest = span - d;
					if (!endsAtRoad || rest == 0 || rest >= minDepth)
						mask |= (byte)(1 << (d - 1));
				}

				depth[t - lo] = mask;
			}

			var runLen = hi - lo + 1;
			var archetype = catalog.InfoOf(zone);
			var maxArea = int.MaxValue;
			if (archetype != null && archetype.AreaByLandValue)
				maxArea = Math.Clamp(5 - LandValueAt(a.Cell, zone) / 20, 1, 4);

			var minWidth = catalog.MinWidth(zone);
			for (var pass = 0; pass < 2; pass++)
			{
				foreach (var shape in shapes)
				{
					if (shape.W > runLen || shape.Area > maxArea)
						continue;

					var remainder = runLen - shape.W;
					if (pass == 0 && remainder > 0 && remainder < minWidth)
						continue;

					if (!FindStart(shape, lo, hi, depth, pass == 0, out var start))
						continue;

					BuildPlan(zone, shape, a, side, axis, inward, start, out plan);
					return true;
				}
			}

			return false;
		}

		// Prefer a lot flush with one end of the run (nearest to the anchor), so blocks fill from their corners.
		static bool FindStart(LotShape shape, int lo, int hi, Span<byte> depth, bool flushOnly, out int start)
		{
			start = 0;
			var first = lo;
			var last = hi - shape.W + 1;
			var dFirst = Math.Abs(2 * first + shape.W - 1);
			var dLast = Math.Abs(2 * last + shape.W - 1);
			if (dLast < dFirst)
			{
				(first, last) = (last, first);
			}

			if (Fits(shape, first, lo, depth))
			{
				start = first;
				return true;
			}

			if (Fits(shape, last, lo, depth))
			{
				start = last;
				return true;
			}

			if (flushOnly)
				return false;

			var bestDist = int.MaxValue;
			var found = false;
			for (var s = lo; s <= hi - shape.W + 1; s++)
			{
				if (!Fits(shape, s, lo, depth))
					continue;

				var dist = Math.Abs(2 * s + shape.W - 1);
				if (dist < bestDist)
				{
					bestDist = dist;
					start = s;
					found = true;
				}
			}

			return found;
		}

		static bool Fits(LotShape shape, int start, int lo, Span<byte> depth)
		{
			var bit = 1 << (shape.D - 1);
			for (var t = start; t < start + shape.W; t++)
				if ((depth[t - lo] & bit) == 0)
					return false;

			return true;
		}

		void BuildPlan(ZoneType zone, LotShape shape, Anchor a, int side, CVec axis, CVec inward, int start, out Plan plan)
		{
			var c0 = a.Cell + new CVec(axis.X * start, axis.Y * start);
			var c1 = a.Cell + new CVec(axis.X * (start + shape.W - 1) + inward.X * (shape.D - 1), axis.Y * (start + shape.W - 1) + inward.Y * (shape.D - 1));
			var origin = new CPos(Math.Min(c0.X, c1.X), Math.Min(c0.Y, c1.Y));
			var archetype = catalog.InfoOf(zone);
			var level = 1;
			if (archetype != null && archetype.SpawnLevel2LandValue > 0 && LandValueAt(a.Cell, zone) >= archetype.SpawnLevel2LandValue)
				level = 2;

			plan = new Plan
			{
				Zone = zone,
				Actor = side % 2 == 0 ? shape.ActorNS : shape.ActorEW,
				Origin = origin,
				Cx = side % 2 == 0 ? shape.W : shape.D,
				Cy = side % 2 == 0 ? shape.D : shape.W,
				Level = level,
			};
		}

		void Spawn(Plan plan)
		{
			for (var y = 0; y < plan.Cy; y++)
				for (var x = 0; x < plan.Cx; x++)
					reserved.Add(plan.Origin + new CVec(x, y));

			spawned[(int)plan.Zone]++;
			var owner = cityPlayer;
			world.AddFrameEndTask(w =>
			{
				clearList.Clear();
				for (var y = 0; y < plan.Cy; y++)
				{
					for (var x = 0; x < plan.Cx; x++)
					{
						var cell = plan.Origin + new CVec(x, y);
						if (zones.GetZone(cell) != plan.Zone || !IsFree(cell, clearList))
						{
							dirty = true;
							clearList.Clear();
							return;
						}
					}
				}

				foreach (var a in clearList)
					a.Dispose();

				clearList.Clear();
				w.CreateActor(plan.Actor, [new LocationInit(plan.Origin), new OwnerInit(owner), new GrowableLevelInit(plan.Level)]);
			});
		}
	}
}
