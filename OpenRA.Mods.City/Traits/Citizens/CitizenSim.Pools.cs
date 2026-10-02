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
	// Array pools (free-lists), household membership lists and the per-pulse candidate lists for homes, jobs and seats.
	public partial class CitizenSim
	{
		IRoadNetwork roads;
		readonly List<Property> homeCands = [];
		readonly List<Property>[] jobCands = [[], [], [], [], []];
		readonly List<Property>[] schoolCands = [[], [], [], [], []];
		readonly int[] vacantByEdu = new int[5];
		int freeHomeSlots;
		int commuterTotal;
		readonly Dictionary<int, int[]> commuterAt = [];

		static void ReleaseCommuters(Property p, int[] comm)
		{
			for (var e = 0; e < 5; e++)
			{
				p.JobsFilled[e] = Math.Max(0, p.JobsFilled[e] - comm[e]);
				comm[e] = 0;
			}
		}

		/// <summary>A resident takes a slot held by an outside commuter, if no free slot is left.</summary>
		void DisplaceCommuter(Property p, int level)
		{
			if (p.JobsFilled[level] >= p.JobSlots[level] && commuterAt.TryGetValue(p.Id, out var comm) && comm[level] > 0)
			{
				comm[level]--;
				p.JobsFilled[level]--;
			}
		}

		/// <summary>Monthly wages of the outside commuters are paid by their employers (income leaves the city, no income tax).</summary>
		void PayCommuters()
		{
			if (economy == null)
				return;

			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
				if (commuterAt.TryGetValue(all[i].Id, out var comm))
					for (var e = 0; e < 5; e++)
						if (comm[e] > 0)
							economy.ChargeWages(all[i].Id, comm[e] * WageOf(e));
		}

		readonly List<Property> overfull = [];

		/// <summary>Shops: commercial lots and mixed-use lots (which carry both household slots and shop jobs).</summary>
		static bool IsShop(Property p)
		{
			return p.Kind == PropertyKind.Commercial || (p.Zone == ZoneType.ResidentialMixed && p.TotalJobSlots > 0);
		}

		static bool IsLeisure(Property p)
		{
			if (IsShop(p))
				return true;

			return p.Actor != null && p.Actor.Info.HasTraitInfo<ServiceBuildingInfo>()
				&& p.Actor.Info.TraitInfo<ServiceBuildingInfo>().Kind == ServiceKind.Parks;
		}

		int nextCitizenGeneration;

		int AllocCitizen()
		{
			int i;
			if (freeCitN > 0)
				i = freeCits[--freeCitN];
			else
			{
				if (citCount == cits.Length)
					Array.Resize(ref cits, cits.Length * 2);

				i = citCount++;
			}

			cits[i] = new Citizen { Generation = ++nextCitizenGeneration, PlannedThroughDay = -1, Household = -1, NextInHousehold = -1, Flags = CitFlags.Alive };
			return i;
		}

		void FreeCitizen(int i)
		{
			cits[i] = new Citizen { Household = -1, NextInHousehold = -1 };
			if (freeCitN == freeCits.Length)
				Array.Resize(ref freeCits, freeCits.Length * 2);

			freeCits[freeCitN++] = i;
		}

		int AllocHousehold()
		{
			int i;
			if (freeHhN > 0)
				i = freeHhs[--freeHhN];
			else
			{
				if (hhCount == hhs.Length)
					Array.Resize(ref hhs, hhs.Length * 2);

				i = hhCount++;
			}

			hhs[i] = new Household { FirstMember = -1, Flags = HhFlags.Alive, Happiness = 50 };
			return i;
		}

		void FreeHousehold(int i)
		{
			hhs[i] = new Household { FirstMember = -1 };
			if (freeHhN == freeHhs.Length)
				Array.Resize(ref freeHhs, freeHhs.Length * 2);

			freeHhs[freeHhN++] = i;
		}

		void AddMember(int hh, int ci)
		{
			cits[ci].Household = hh;
			cits[ci].NextInHousehold = hhs[hh].FirstMember;
			hhs[hh].FirstMember = ci;
			hhs[hh].Size++;
			if (AgeOf(ci) <= info.TeenMaxAge)
				hhs[hh].Kids++;
		}

		void RemoveMember(int hh, int ci)
		{
			if (hhs[hh].FirstMember == ci)
				hhs[hh].FirstMember = cits[ci].NextInHousehold;
			else
			{
				for (var m = hhs[hh].FirstMember; m >= 0; m = cits[m].NextInHousehold)
					if (cits[m].NextInHousehold == ci)
					{
						cits[m].NextInHousehold = cits[ci].NextInHousehold;
						break;
					}
			}

			cits[ci].NextInHousehold = -1;
			hhs[hh].Size--;
			if (AgeOf(ci) <= info.TeenMaxAge && hhs[hh].Kids > 0)
				hhs[hh].Kids--;
		}

		/// <summary>Removes a citizen from the world: releases job/school seat, adjusts counters, frees the slot.</summary>
		void KillCitizen(int ci)
		{
			ReleaseWork(ci);
			var hh = cits[ci].Household;
			if (hh >= 0)
			{
				RemoveMember(hh, ci);
				var p = registry.Get(hhs[hh].Home);
				if (p != null && p.Residents > 0)
					p.Residents--;

				if (hhs[hh].Size == 0)
					DissolveHousehold(hh);
			}

			FreeCitizen(ci);
		}

		/// <summary>Frees a household with no members left (its home slot is released).</summary>
		void DissolveHousehold(int hh)
		{
			var p = registry.Get(hhs[hh].Home);
			if (p != null && p.Households > 0)
				p.Households--;

			FreeHousehold(hh);
		}

		/// <summary>The whole household leaves the city (emigration).</summary>
		void Emigrate(int hh)
		{
			var m = hhs[hh].FirstMember;
			while (m >= 0)
			{
				var next = cits[m].NextInHousehold;
				ReleaseWork(m);
				FreeCitizen(m);
				m = next;
			}

			var p = registry.Get(hhs[hh].Home);
			if (p != null)
			{
				p.Residents = Math.Max(0, p.Residents - hhs[hh].Size);
				if (p.Households > 0)
					p.Households--;
			}

			emigrants++;
			FreeHousehold(hh);
		}

		/// <summary>The household loses its home but stays in the city (looking for a new one, or homeless).</summary>
		void Evict(int hh)
		{
			var p = registry.Get(hhs[hh].Home);
			if (p != null)
			{
				p.Residents = Math.Max(0, p.Residents - hhs[hh].Size);
				if (p.Households > 0)
					p.Households--;
			}

			hhs[hh].Home = 0;
			hhs[hh].Rent = 0;
			hhs[hh].SearchDays = 0;
			hhs[hh].Flags |= HhFlags.Homeless;
		}

		bool Alive(Property p) { return p != null && registry.Get(p.Id) == p; }

		/// <summary>Rebuilds the candidate lists once per pulse from the registry (O(properties)).</summary>
		void RebuildCandidates()
		{
			homeCands.Clear();
			freeHomeSlots = 0;
			overfull.Clear();
			commuterTotal = 0;
			shopCands.Clear();
			leisureCands.Clear();
			for (var e = 0; e < 5; e++)
			{
				jobCands[e].Clear();
				schoolCands[e].Clear();
				vacantByEdu[e] = 0;
			}

			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];
				var eligible = p.Operational && p.HasRoadAccess
					&& !(roads != null && p.AccessRoad != CPos.Zero && !roads.IsConnectedToOutside(p.AccessRoad));
				if (!eligible)
				{
					if (commuterAt.TryGetValue(p.Id, out var gone))
						ReleaseCommuters(p, gone);

					continue;
				}

				if (p.Kind == PropertyKind.Residential && p.HouseholdSlots > p.Households)
				{
					homeCands.Add(p);
					freeHomeSlots += p.HouseholdSlots - p.Households;
				}

				commuterAt.TryGetValue(p.Id, out var comm);
				for (var e = 0; e < 5; e++)
				{
					var cur = comm?[e] ?? 0;
					var residentFilled = p.JobsFilled[e] - cur;
					var free = p.JobSlots[e] - residentFilled;
					if (free < 0 && overfull.Count < 4096)
						overfull.Add(p);

					// Outside commuters fill a share of the vacancies (ECO production counts them); residents displace them.
					var want = free > 0 ? Math.Max(free * info.CommuterPercentOfVacancies / 100, 0) : 0;
					if (want != cur)
					{
						if (comm == null)
						{
							comm = new int[5];
							commuterAt[p.Id] = comm;
						}

						p.JobsFilled[e] += want - cur;
						comm[e] = want;
					}

					commuterTotal += want;

					if (free > 0)
					{
						jobCands[e].Add(p);
						vacantByEdu[e] += free;
					}
				}

				if (IsShop(p))
					shopCands.Add(p);

				if (IsLeisure(p))
					leisureCands.Add(p);

				var level = SchoolLevelOf(p);
				if (level > 0 && SeatsOf(p) > p.Students)
					schoolCands[level].Add(p);
			}
		}

		/// <summary>School level of a property: ZON/SVC value if set, else a fallback from the service building info.</summary>
		static int SchoolLevelOf(Property p)
		{
			if (p.SchoolLevel > 0)
				return Math.Min(4, p.SchoolLevel);

			if (p.Kind == PropertyKind.Service && p.Actor != null && p.Actor.Info.HasTraitInfo<ServiceBuildingInfo>())
			{
				var sb = p.Actor.Info.TraitInfo<ServiceBuildingInfo>();
				if (sb.Kind == ServiceKind.Education)
					return 1;
			}

			return 0;
		}

		int SeatsOf(Property p)
		{
			if (p.StudentSeats > 0)
				return p.StudentSeats;

			return SchoolLevelOf(p) > 0 ? info.FallbackSchoolSeats : 0;
		}

		static int ManhattanRoad(Property a, Property b)
		{
			return Math.Abs(a.AccessRoad.X - b.AccessRoad.X) + Math.Abs(a.AccessRoad.Y - b.AccessRoad.Y);
		}
	}
}
