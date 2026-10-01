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
	/// <summary>Read-only crime statistics of the citizen simulation (implemented by CitizenSim; SVC and the UI may read it).</summary>
	public interface ICrimeSource
	{
		/// <summary>Citizens currently serving a jail sentence in the city.</summary>
		int Inmates { get; }

		/// <summary>Citizens serving a prison sentence outside the city.</summary>
		int PrisonersAway { get; }

		int CrimesLastMonth { get; }
		int ArrestsLastMonth { get; }

		/// <summary>0..100 recent (3 days) unsolved crime at a property.</summary>
		int GetCrimeRisk(int propertyId);
	}

	// Crime: unhappy, unemployed or poor adults sometimes turn to crime, pick a target, and are arrested depending on police coverage.
	// With SVC (ICityCrime) the police response, jail and release are SVC's; without it the sim resolves crimes from police coverage.
	// Arrested citizens cannot work or study until released.
	public partial class CitizenSim : ICrimeSource, ICrimeListener
	{
		struct PendingCrime
		{
			public int Citizen;
			public int Birth;
			public int Property;
			public int Tick;
		}

		readonly List<PendingCrime> pendingCrimes = [];
		readonly Dictionary<int, (int Count, int Day)> crimeHeat = [];
		ICityCrime cityCrime;
		int crimes, arrests, escapes, crimesMonth, arrestsMonth;

		public int Inmates { get; private set; }
		public int PrisonersAway { get; private set; }
		public int CrimesLastMonth { get; private set; }
		public int ArrestsLastMonth { get; private set; }

		public int GetCrimeRisk(int propertyId)
		{
			if (!crimeHeat.TryGetValue(propertyId, out var h) || Today - h.Day > 3)
				return 0;

			return Math.Min(100, h.Count * 20);
		}

		int CrimeHeatPoints(int propertyId)
		{
			return Math.Min(3, GetCrimeRisk(propertyId) / 20);
		}

		void UpdateCrime(int ci, int age, int today)
		{
			var c = cits[ci];
			if (age < info.WorkAge || age > info.AdultMaxAge || (c.Flags & (CitFlags.Jailed | CitFlags.Sick)) != 0 || c.Household < 0)
				return;

			var hh = c.Household;
			var home = registry.Get(hhs[hh].Home);
			if (home == null)
				return;

			var points = Math.Max(0, 50 - c.Wellbeing) * 2;
			if ((c.Flags & (CitFlags.Worker | CitFlags.Student)) == 0)
				points += 30;

			if (hhs[hh].Cash <= 0)
				points += 20;

			if (points == 0)
				return;

			var coverage = Satisfaction(ServiceKind.Police, CityService.Police, home.AccessCell);
			var ppm = (long)info.CrimeBasePPM * points / 100 * (100 - coverage * 70 / 100) / 100 / Math.Max(1, info.DaysPerCitizenYear);
			if (Hash(ci, today, 110) % 1000000 >= ppm)
				return;

			var target = PickCrimeTarget(home);
			if (target == null)
				return;

			if (cityCrime != null)
			{
				// SVC runs the police response, jail and release; it answers through ICrimeListener.
				if (cityCrime.IsCrimeActive(target.Id) || !cityCrime.ReportCrime(ci + 1, target.Id, this))
					return;

				pendingCrimes.Add(new PendingCrime { Citizen = ci, Birth = c.BirthDay, Property = target.Id, Tick = world.WorldTick });
				crimes++;
				crimesMonth++;
				return;
			}

			crimes++;
			crimesMonth++;
			var arrestPercent = info.ArrestBasePercent + Satisfaction(ServiceKind.Police, CityService.Police, target.AccessCell) * info.ArrestPerCoveragePercent / 100;
			FinishCrime(ci, target.Id, Hash(ci, today, 111) % 100 < arrestPercent);
		}

		/// <summary>The most tempting of a few sampled residential or commercial properties (high crime pressure, little police).</summary>
		Property PickCrimeTarget(Property own)
		{
			var all = registry.All;
			if (all.Count == 0)
				return null;

			Property best = null;
			var bestScore = int.MinValue;
			for (var s = 0; s < info.CrimeTargetSamples; s++)
			{
				var p = all[NextRandom(all.Count)];
				if (p.Id == own.Id || (p.Kind != PropertyKind.Residential && p.Kind != PropertyKind.Commercial))
					continue;

				var score = (cityCrime != null ? cityCrime.GetCrimePressure(p.Id) : 0)
					+ 100 - Satisfaction(ServiceKind.Police, CityService.Police, p.AccessCell);
				score -= Math.Abs(p.Origin.X - own.Origin.X) + Math.Abs(p.Origin.Y - own.Origin.Y);
				if (score > bestScore)
				{
					bestScore = score;
					best = p;
				}
			}

			return best;
		}

		void FinishCrime(int ci, int propertyId, bool arrested)
		{
			if (arrested)
			{
				Jail(ci, false);
				return;
			}

			escapes++;
			crimeHeat.TryGetValue(propertyId, out var h);
			crimeHeat[propertyId] = (Today - h.Day > 3 ? 1 : h.Count + 1, Today);
		}

		/// <summary>Removes and returns the pending crime of a criminal, or -1.</summary>
		int TakePending(int criminalId, out int property)
		{
			var ci = criminalId - 1;
			property = 0;
			for (var i = 0; i < pendingCrimes.Count; i++)
			{
				var pc = pendingCrimes[i];
				if (pc.Citizen != ci)
					continue;

				pendingCrimes.RemoveAt(i);
				if (ci < citCount && (cits[ci].Flags & CitFlags.Alive) != 0 && cits[ci].BirthDay == pc.Birth)
				{
					property = pc.Property;
					return ci;
				}

				return -1;
			}

			return -1;
		}

		void ICrimeListener.OnArrested(int criminalId, int jailProperty)
		{
			var ci = TakePending(criminalId, out _);
			if (ci >= 0)
				Jail(ci, true);
		}

		void ICrimeListener.OnEscaped(int criminalId)
		{
			var ci = TakePending(criminalId, out var property);
			if (ci >= 0)
				FinishCrime(ci, property, false);
		}

		void ICrimeListener.OnReleased(int criminalId, int jailProperty)
		{
			var ci = criminalId - 1;
			if (ci < 0 || ci >= citCount || (cits[ci].Flags & (CitFlags.Alive | CitFlags.Jailed)) != (CitFlags.Alive | CitFlags.Jailed))
				return;

			cits[ci].Flags &= ~CitFlags.Jailed;
			cits[ci].JailDays = 0;
			cits[ci].Wellbeing = (byte)Math.Max(0, cits[ci].Wellbeing - 10);
		}

		/// <summary>Crimes SVC never answered in time count as escapes.</summary>
		void ExpirePendingCrimes()
		{
			for (var i = pendingCrimes.Count - 1; i >= 0; i--)
			{
				var pc = pendingCrimes[i];
				if (world.WorldTick - pc.Tick < 600)
					continue;

				pendingCrimes.RemoveAt(i);
				if (pc.Citizen < citCount && (cits[pc.Citizen].Flags & CitFlags.Alive) != 0 && cits[pc.Citizen].BirthDay == pc.Birth)
					FinishCrime(pc.Citizen, pc.Property, false);
			}
		}

		void Jail(int ci, bool bySvc)
		{
			ReleaseWork(ci);

			// With SVC the sentence is its jail's business (released through OnReleased); otherwise we count days.
			var prison = !bySvc && Hash(ci, Today, 112) % 100 < info.PrisonPercent;
			cits[ci].Flags |= CitFlags.Jailed;
			cits[ci].InPrison = prison;
			cits[ci].JailDays = bySvc ? (byte)255 : (byte)Math.Clamp(prison ? info.PrisonDays : info.JailDays, 1, 250);
			cits[ci].Loc = 0;
			arrests++;
			arrestsMonth++;
			Announce(3, ci);
		}

		/// <summary>Daily: count down the sentence and release.</summary>
		void ServeSentence(int ci)
		{
			if (cits[ci].JailDays == 255)
				return;

			if (cits[ci].JailDays > 0)
				cits[ci].JailDays--;

			if (cits[ci].JailDays == 0)
			{
				cits[ci].Flags &= ~CitFlags.Jailed;
				cits[ci].InPrison = false;
				cits[ci].Wellbeing = (byte)Math.Max(0, cits[ci].Wellbeing - 10);
			}
		}
	}
}
