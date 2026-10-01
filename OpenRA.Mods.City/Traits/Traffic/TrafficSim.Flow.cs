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
	// The mesoscopic link model: discharge, spillback, junction control, gridlock breaker.
	public sealed partial class TrafficSim
	{
		int[] lastRelU, nodeNextU, sigShift;
		byte[] sigExt;
		byte[] roundRobin;
		int blockedFronts, gridlockReleases;
		int replanBudget;

		void InitFlowState()
		{
			lastRelU = new int[linkCount];
			nodeNextU = new int[cellCount];
			sigShift = new int[cellCount];
			sigExt = new byte[cellCount];
			roundRobin = new byte[cellCount];
			Array.Fill(lastRelU, -1000000);
		}

		void TickFlow()
		{
			replanBudget = Math.Max(1, Info.PlanBudget / 2);
			var dischargeU = Info.DischargeMilliTicks * U / 1000;
			for (var k = 0; k < roadListCount; k++)
			{
				var cell = roadList[k];
				var l = cell * 4;
				if (ctl[cell] == (byte)JunctionControl.Signal && Info.SignalExtendTicks > 0)
					UpdateSignal(cell);

				if (qHead[l] >= 0)
					ProcessLink(l, cell, 0, dischargeU);

				if (qHead[l + 1] >= 0)
					ProcessLink(l + 1, cell, 1, dischargeU);

				if (qHead[l + 2] >= 0)
					ProcessLink(l + 2, cell, 2, dischargeU);

				if (qHead[l + 3] >= 0)
					ProcessLink(l + 3, cell, 3, dischargeU);
			}
		}

		void ProcessLink(int l, int cell, int h, int dischargeU)
		{
			var lanes = cellLanes[cell];
			if (laneBlock[l] > 0)
			{
				// An accident blocks lanes of this link until the wreck is cleared.
				if (laneBlock[l] >= lanes)
				{
					if (vReadyU[qHead[l]] <= nowU)
						Blocked(l, qHead[l], cell, h);

					return;
				}

				lanes -= laneBlock[l];
			}

			var interval = Math.Max(1, dischargeU / lanes);
			if (qCount[l] > 1 && vTrip[qHead[l]].Kind != KindEmergency)
				PromoteEmergency(l, cellLanes[cell]);

			for (var iter = 0; iter < 8; iter++)
			{
				var v = qHead[l];
				if (v < 0 || vReadyU[v] > nowU || nextRelU[l] > nowU)
					return;

				var route = vRoute[v];
				var step = vStep[v];
				var t = vTrip[v];

				if (step >= route.Length)
				{
					if (TryParkSearch(v, cell, h))
						continue;

					PopHead(l);
					Traversed(l, v);
					var arrived = t;
					ReleaseVehicle(v);
					arrived.Vehicle = NoVehicle;
					FinishArrival(arrived);
					Released(l, interval);
					continue;
				}

				int r = route[step];
				if ((exitMask[cell] & (1 << r)) == 0)
				{
					// The road changed under the vehicle: plan again from here.
					if (!Replan(v, cell, h))
					{
						FailVehicle(v, TripFailure.NoRoute);
						Released(l, interval);
					}

					continue;
				}

				var next = cell + dIdx[r];
				var nl = next * 4 + r;
				var kind = t.Kind;

				if (!CanPassNode(l, cell, h, r, v, kind))
				{
					Blocked(l, v, cell, h);
					return;
				}

				if (crossingCells > 0 && kind != KindTram && CrossingClosed(next))
				{
					Blocked(l, v, cell, h);
					return;
				}

				var storage = Storage(next);
				var wreck = laneBlock[nl];
				if (wreck > 0)
					storage -= wreck * Info.SlotsPerLane;

				if (wreck >= cellLanes[next] || (qOcc[nl] > 0 && qOcc[nl] + vSlots[v] > storage))
				{
					var since = blockedSince[l];
					if (since >= 0 && nowU - since > Info.GridlockTicks * U && kind != KindEmergency)
						gridlockReleases++;
					else
					{
						Blocked(l, v, cell, h);
						return;
					}
				}

				PopHead(l);
				Traversed(l, v);
				vStep[v] = step + 1;
				EnterLink(v, nl, Math.Max(vReadyU[v], nowU - U));
				MaybeCrash(v, nl);
				if (ctl[cell] == (byte)JunctionControl.Stop)
				{
					nodeNextU[cell] = nowU + 2 * U;
					roundRobin[cell] = (byte)((h + 1) & 3);
				}

				Released(l, interval);
			}
		}

		// Emergency vehicles waiting among the first `lanes` positions of a link overtake the others (other traffic yields).
		void PromoteEmergency(int l, int lanes)
		{
			var prev = qHead[l];
			var v = vNext[prev];
			for (var pos = 1; pos < lanes && v >= 0; pos++)
			{
				if (vTrip[v].Kind == KindEmergency && vReadyU[v] <= nowU)
				{
					vNext[prev] = vNext[v];
					if (qTail[l] == v)
						qTail[l] = prev;

					vNext[v] = qHead[l];
					qHead[l] = v;
					return;
				}

				prev = v;
				v = vNext[v];
			}
		}

		void Released(int l, int interval)
		{
			nextRelU[l] = Math.Max(nextRelU[l], nowU - U) + interval;
			lastRelU[l] = nowU;
			blockedSince[l] = -1;
		}

		// The front vehicle of link l cannot move. Handles rerouting around long blocks and stuck removal.
		void Blocked(int l, int v, int cell, int h)
		{
			if (blockedSince[l] < 0)
			{
				blockedSince[l] = nowU;
				return;
			}

			var waited = nowU - blockedSince[l];
			if (waited > Info.StuckTicks * U)
			{
				FailVehicle(v, TripFailure.Stuck);
				blockedSince[l] = -1;
				return;
			}

			var t = vTrip[v];
			if (waited > 40 * U && replanBudget > 0 && tick - t.LastReplan > 80 && vStep[v] < vRoute[v].Length)
			{
				replanBudget--;
				t.LastReplan = tick;
				Replan(v, cell, h);
			}
		}

		bool Replan(int v, int cell, int h)
		{
			var t = vTrip[v];
			var dest = t.ParkCell >= 0 ? t.ParkCell : t.Destination;
			if (!FindRouteForTrip(t, cell, h, dest, t.SpeedPct, t.Kind == KindEmergency, Hash(t.Id, tick) | 1, out var route, out _))
				return false;

			vRoute[v] = route;
			vStep[v] = 0;
			return true;
		}

		int SignalPhase(int cell, int cycle)
		{
			var p = (tick + (Hash(cell, 7) & 0xffff) % cycle + sigShift[cell]) % cycle;
			return p < 0 ? p + cycle : p;
		}

		// Green extension: on the last green tick, a phase whose traffic is still flowing is held for up to SignalExtendTicks.
		void UpdateSignal(int cell)
		{
			var cycle = Math.Max(8, Info.SignalCycleTicks);
			var half = cycle / 2;
			var p = SignalPhase(cell, cycle);
			if (p == 0 || p == half)
			{
				sigExt[cell] = 0;
				return;
			}

			var axis = p < half ? 0 : 1;
			var lastGreen = axis == 0 ? half - 3 : cycle - 3;
			if (p != lastGreen || sigExt[cell] >= Info.SignalExtendTicks)
				return;

			var l = cell * 4;
			var flowing = false;
			for (var h = axis; h < 4; h += 2)
				if (qCount[l + h] > 0 && nowU - lastRelU[l + h] <= 2 * U)
					flowing = true;

			if (!flowing)
				return;

			sigShift[cell]--;
			sigExt[cell]++;
		}

		// Whether the front vehicle v on a link of `cell` has somewhere to go (its next link has room, or it arrives).
		bool CanAdvance(int v, int cell)
		{
			var route = vRoute[v];
			var step = vStep[v];
			if (step >= route.Length)
				return true;

			int r = route[step];
			if ((exitMask[cell] & (1 << r)) == 0)
				return true;

			var next = cell + dIdx[r];
			var nl = next * 4 + r;
			return qOcc[nl] == 0 || qOcc[nl] + vSlots[v] <= Storage(next);
		}

		bool CanPassNode(int l, int cell, int h, int r, int v, byte kind)
		{
			var control = (JunctionControl)ctl[cell];
			if (control == JunctionControl.None || kind == KindEmergency)
				return true;

			var turn = (r - h) & 3;
			switch (control)
			{
				case JunctionControl.Stop:
				{
					if (nodeNextU[cell] > nowU || vReadyU[v] + 2 * U > nowU)
						return false;

					// Round robin over the approaches that have a vehicle waiting.
					var rr = roundRobin[cell];
					for (var i = 0; i < 4; i++)
					{
						var hh = (rr + i) & 3;
						var f = qHead[cell * 4 + hh];
						if (f >= 0 && vReadyU[f] + 2 * U <= nowU && CanAdvance(f, cell))
							return hh == h;
					}

					return true;
				}

				case JunctionControl.Signal:
				{
					var cycle = Math.Max(8, Info.SignalCycleTicks);
					var half = cycle / 2;
					var phase = SignalPhase(cell, cycle);
					var green = (h & 1) == 0 ? phase < half - 2 : phase >= half && phase < cycle - 2;
					if (!green)
						return false;

					if (turn == 3)
					{
						// Left turns give way to the opposing through traffic, unless the green is ending or they waited long.
						var lo = cell * 4 + ((h + 2) & 3);
						var f = qHead[lo];
						var clearing = phase % half >= half - 4;
						var waited = blockedSince[l] >= 0 && nowU - blockedSince[l] > 10 * U;
						if (f >= 0 && vReadyU[f] <= nowU && !clearing && !waited)
							return false;
					}

					return true;
				}

				default:
				{
					// Priority: yield to waiting or just released vehicles of higher-ranking approaches. Right turns are free.
					if (turn == 1 || turn == 2)
						return true;

					var rank = approachRank[l];
					for (var hh = 0; hh < 4; hh++)
					{
						if (hh == h)
							continue;

						var l2 = cell * 4 + hh;
						if (approachRank[l2] <= rank)
							continue;

						var f = qHead[l2];
						if ((f >= 0 && vReadyU[f] <= nowU + 2 * U && CanAdvance(f, cell)) || nowU - lastRelU[l2] < 3 * U)
						{
							// Do not starve: after a long wait the minor road forces its way in.
							return blockedSince[l] >= 0 && nowU - blockedSince[l] > 12 * U;
						}
					}

					return true;
				}
			}
		}
	}
}
