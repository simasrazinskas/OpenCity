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

namespace OpenRA.Mods.City.Traits
{
	// Selectable / followable vehicles (IVehicleInspector). Render-side reads only: nothing here changes sim state.
	public sealed partial class TrafficSim
	{
		bool IVehicleInspector.TryGetVehicleAt(WPos position, int radius, out VehicleView view)
		{
			view = default;
			var best = (long)radius * radius;
			var found = -1;
			for (var i = 0; i < drawn.Count; i++)
			{
				var d = drawn[i].Pos - position;
				var dist = (long)d.X * d.X + (long)d.Y * d.Y;
				if (dist <= best)
				{
					best = dist;
					found = i;
				}
			}

			if (found < 0)
				return false;

			var p = drawn[found];
			var t = vTrip[p.Vehicle];
			if (t == null)
				return false;

			view = MakeView(t, p.Pos, p.Facing, p.Progress);
			return true;
		}

		bool IVehicleInspector.TryGetVehicle(int tripId, out VehicleView view)
		{
			view = default;
			if (!trips.TryGetValue(tripId, out var t))
				return false;

			if (t.Vehicle != NoVehicle)
			{
				var v = t.Vehicle;
				var l = vLink[v];
				if (l < 0)
					return false;

				var cell = l >> 2;
				LayoutLink(l, cell % width, cell / width, l & 3, RenderU());
				foreach (var p in poses)
				{
					if (p.Vehicle == v)
					{
						view = MakeView(t, p.Pos, p.Facing, p.Progress);
						return true;
					}
				}

				return false;
			}

			if (t.Crashed)
			{
				foreach (var inc in incidents)
				{
					if (inc.Crashed != t)
						continue;

					var cell = inc.Link >> 2;
					PoseOf(map.CenterOfCell(new CPos(cell % width, cell / width)), inc.Link & 3, inc.Link & 3, 0.5f, 0, cellLanes[cell], out var wpos, out var facing);
					view = MakeView(t, wpos, facing, 50);
					view.Crashed = true;
					return true;
				}
			}

			// Walking trips and trips still waiting at their origin have no vehicle on the road.
			return false;
		}

		VehicleView MakeView(TripRec t, WPos pos, WAngle facing, int progress)
		{
			return new VehicleView
			{
				TripId = t.Id,
				OwnerId = t.Owner,
				Image = t.Sprite < spriteNames.Count ? spriteNames[t.Sprite] : null,
				Purpose = t.Purpose,
				Mode = ModeOf(t),
				OriginRoad = ToCPos(t.Origin),
				DestinationRoad = ToCPos(t.Destination),
				OriginProperty = t.OriginProperty,
				DestinationProperty = t.DestinationProperty,
				DepartTick = t.DepartTick,
				ProgressPercent = Math.Clamp(progress, 0, 100),
				Position = pos,
				Facing = facing,
			};
		}
	}
}
