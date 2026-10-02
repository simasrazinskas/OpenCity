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

namespace OpenRA.Mods.City.Traits
{
	// Progression policies (IProgression.GetPolicy, per district) that affect traffic. Everything is optional: without a
	// progression provider or active policies nothing changes.
	//   TrafficSpeedPct / HighwaySpeedPct : signed percent change of the free-flow speed (-25 = 25% slower)
	//   AccidentPct                       : signed percent change of the accident odds
	//   TruckBan / FuelVehicleBan / ThroughTrafficBan : soft bans - routes avoid the district (destinations stay reachable)
	//   ParkingFee                        : the fee makes short car trips walkable (see WalkLimit)
	public sealed partial class TrafficSim
	{
		const int PolSpeed = 0, PolHighway = 1, PolAccident = 2, PolTruck = 3, PolThrough = 4, PolFuel = 5, PolParking = 6, PolCount = 7;
		const int MaxDistrictIds = 64;

		static readonly string[] PolicyKeys =
			["TrafficSpeedPct", "HighwaySpeedPct", "AccidentPct", "TruckBan", "ThroughTrafficBan", "FuelVehicleBan", "ParkingFee"];

		IProgression progression;
		bool progressionSearched;
		byte[] cellDistrict;
		byte[] policyFlags;          // bit0 truck ban, bit1 through ban, bit2 fuel ban
		int[] ffBase;
		bool flagsActive;
		bool policyAny;
		bool routeThrough;
		bool policyDirty;
		int lastPolicyCheck;
		readonly int[] polValues = new int[MaxDistrictIds * PolCount];
		readonly int[] districtSample = new int[MaxDistrictIds];

		void FindProgression()
		{
			if (progressionSearched)
				return;

			progressionSearched = true;
			progression = world.WorldActor.TraitsImplementing<IProgression>().FirstOrDefault()
				?? world.Players.Where(p => p.Playable).Select(p => p.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault()).FirstOrDefault(x => x != null);
		}

		// After a graph rebuild: remember the base speeds and districts, then apply the current policies.
		void PolicyAfterRebuild()
		{
			FindProgression();
			cellDistrict ??= new byte[cellCount];
			policyFlags ??= new byte[cellCount];
			ffBase ??= new int[cellCount];
			Array.Clear(cellDistrict);
			Array.Fill(districtSample, -1);
			for (var k = 0; k < roadListCount; k++)
			{
				var i = roadList[k];
				ffBase[i] = ffU[i];
				var d = progression != null ? Math.Clamp(progression.GetDistrict(ToCPos(i)), 0, MaxDistrictIds - 1) : 0;
				cellDistrict[i] = (byte)d;
				if (districtSample[d] < 0)
					districtSample[d] = i;
			}

			Array.Clear(polValues);
			lastPolicyCheck = tick;
			policyDirty = true;
			ReadPolicies();
			ApplyPolicies();
		}

		// Polls the policy values of every district that has road cells; re-applies them when something changed.
		void ReadPolicies()
		{
			if (progression == null)
				return;

			for (var d = 0; d < MaxDistrictIds; d++)
			{
				if (districtSample[d] < 0)
					continue;

				var sample = ToCPos(districtSample[d]);
				for (var k = 0; k < PolCount; k++)
				{
					var value = progression.GetPolicy(PolicyKeys[k], sample);
					if (polValues[d * PolCount + k] != value)
					{
						polValues[d * PolCount + k] = value;
						policyDirty = true;
					}
				}
			}
		}

		void CheckPolicies()
		{
			if (progression == null || tick - lastPolicyCheck < 50)
				return;

			lastPolicyCheck = tick;
			ReadPolicies();
			if (policyDirty)
				ApplyPolicies();
		}

		int PolicyAt(int cell, int key)
		{
			return cellDistrict == null ? 0 : polValues[cellDistrict[cell] * PolCount + key];
		}

		void ApplyPolicies()
		{
			policyDirty = false;
			var anyFlag = false;
			minFfU = int.MaxValue;
			for (var k = 0; k < roadListCount; k++)
			{
				var i = roadList[k];
				var factor = 100 + PolicyAt(i, PolSpeed);
				if (cellClass[i] == (byte)RoadClass.Highway)
					factor += PolicyAt(i, PolHighway);

				ffU[i] = Math.Max(8, (int)((long)ffBase[i] * 100 / Math.Max(20, factor)));
				minFfU = Math.Min(minFfU, ffU[i]);

				byte f = 0;
				if (PolicyAt(i, PolTruck) > 0)
					f |= 1;

				if (PolicyAt(i, PolThrough) > 0)
					f |= 2;

				if (PolicyAt(i, PolFuel) > 0)
					f |= 4;

				policyFlags[i] = f;
				anyFlag |= f != 0;
			}

			if (minFfU == int.MaxValue)
				minFfU = 1;

			policyAny = anyFlag;
			flagsActive = anyFlag || anyLaneFlag;
			RefreshCostSnapshot();
		}

		// Soft bans as route cost factors (percent) for entering `next`.
		int PolicyCostPercent(byte kind, int next)
		{
			var f = policyFlags[next];
			if (f == 0)
				return 100;

			long pct = 100;
			if ((f & 1) != 0 && kind == KindTruck)
				pct *= 30;

			if ((f & 2) != 0 && routeThrough)
				pct *= 30;

			if ((f & 4) != 0 && (kind == KindCar || kind == KindTruck))
				pct *= 10;

			return (int)Math.Min(pct, 100000);
		}

		// Accident odds scale with AccidentPct of the district (percent, min 10).
		int AccidentPolicyPercent(int cell)
		{
			return Math.Max(10, 100 + PolicyAt(cell, PolAccident));
		}

		// Maximum cells of a trip that is walked; parking fees make short drives unattractive.
		int WalkLimit(int destinationCell)
		{
			if (Info.WalkMaxCells <= 0)
				return 0;

			return Info.WalkMaxCells + PolicyAt(destinationCell, PolParking) / 5;
		}

		bool IsThrough(TripRec t)
		{
			return t.Purpose == TripPurpose.Through || (gateFlag[t.Origin] && gateFlag[t.Destination]);
		}

		bool FindRouteForTrip(TripRec t, int from, int fromHeading, int to, int speedPct, bool emergency, int seed, out byte[] route, out int costU)
		{
			routeThrough = IsThrough(t);
			var ok = FindRouteFor(t.Kind, from, fromHeading, to, speedPct, emergency, seed, out route, out costU, t.SearchNodes);
			routeThrough = false;
			return ok;
		}
	}
}
