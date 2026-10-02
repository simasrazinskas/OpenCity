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
	// Render-only vehicle paths through a road cell: straight runs along the lane, turns on NET's quarter arcs (radius 0.5 cell around
	// the shared cell corner, +/- the lane offset), whole-pixel snapping in the iso projection.
	public sealed partial class TrafficSim
	{
		static void PathPose(WPos center, int h, int r, float t, int offset, out WPos pos, out WAngle facing)
		{
			MoverArt.CellPose(center, h, r, t, offset, out pos, out facing);
		}

		/// <summary>Lane offset of a vehicle in a cell (NET lane contract).</summary>
		int LaneOffsetOf(int cell, int vehicle)
		{
			var p = ProfileOfCell(cell);
			var lanes = Math.Max(1, (int)cellLanes[cell]);
			var simLane = vehicle >= 0 ? vLane[vehicle] % lanes : 0;
			return p.Lanes[RenderLane(p, simLane, lanes, Math.Max(0, vehicle))];
		}

		// Kept for the inspector and wrecks: pose by simulation lane.
		void PoseOf(WPos center, int h, int r, float t, int lane, int lanes, out WPos pos, out WAngle facing)
		{
			var cell = Cell(map.CellContaining(center));
			var p = ProfileOfCell(cell);
			PathPose(center, h, r, t, p.Lanes[RenderLane(p, lane, Math.Max(1, lanes), 0)], out pos, out facing);
		}

		/// <summary>
		/// Pose of a point `back` cells behind progress `t` of vehicle `v` (trailers, tram and bus segments). Steps back into the cell the
		/// vehicle came from when needed, so segments follow the arc through the junction behind them.
		/// </summary>
		void TrailingPose(int v, int cell, int h, int r, float t, float back, int offset, out WPos pos, out WAngle facing)
		{
			var tb = t - back;
			if (tb >= 0f || vStep[v] <= 0)
			{
				PathPose(map.CenterOfCell(ToCPos(cell)), h, r, Math.Max(0f, tb), offset, out pos, out facing);
				return;
			}

			var route = vRoute[v];
			var prevEntry = vStep[v] >= 2 && vStep[v] - 2 < route.Length ? route[vStep[v] - 2] : h;
			var prev = ToCPos(cell) - CityUtils.Neighbours4[h];
			PathPose(map.CenterOfCell(prev), prevEntry, h, Math.Max(0f, 1f + tb), offset, out pos, out facing);
		}
	}
}
