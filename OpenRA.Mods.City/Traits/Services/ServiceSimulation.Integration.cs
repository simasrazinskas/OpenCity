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
	// Hooks into the other work packages: problem flags (ENV), chirps (ENV), policies (PRG).
	public partial class ServiceSimulation
	{
		IProgression progression;
		ICityProblems problems;
		CityStatistics statistics;
		ICityStatistics statsInterface;

		RoadLayer roadLayer;

		void ResolveIntegration()
		{
			roadLayer = world.WorldActor.TraitOrDefault<RoadLayer>();
			progression = Find<IProgression>();
			problems = Find<ICityProblems>();
			ResolveDisasters();
			statistics = Find<CityStatistics>();
			statsInterface = statistics == null ? Find<ICityStatistics>() : null;
		}

		/// <summary>Integer policy effect (percent or flag) at a cell, 0 without a progression provider.</summary>
		int Policy(string key, CPos cell)
		{
			return progression?.GetPolicy(key, cell) ?? 0;
		}

		/// <summary>Scales a value by (100 + policy percent) / 100.</summary>
		int WithPolicy(int value, string key, CPos cell)
		{
			var pct = Policy(key, cell);
			return pct == 0 ? value : (int)Math.Max(0L, value * (100L + pct) / 100);
		}

		bool ImportPolicy(CPos cell) { return Policy("ImportServices", cell) > 0; }

		void Chirp(string key, int variants, PropData d, string arg = null)
		{
			var k = variants > 1 ? key + "-" + (1 + ServiceMath.Hash(d?.Prop.Id ?? 0, Now, 91) % variants) : key;
			if (statistics != null)
				statistics.Post(k, 0, arg, 5, d != null ? d.Prop.Origin : CPos.Zero);
			else
				statsInterface?.Chirp(k, 0, arg);
		}

		/// <summary>Per-property conditions (re-raised every need interval; the flag lasts a little longer than that).</summary>
		void RaiseProblems(PropData d)
		{
			if (problems == null || d.Prop.Actor == null || d.Prop.Actor.Disposed)
				return;

			var a = d.Prop.Actor;
			var duration = Math.Max(1, Info.NeedIntervalPulses) * 25 + 30;
			if (d.GarbageKg >= Info.PileKg)
				problems.Raise(a, CityProblem.Garbage, duration);

			if (d.SickWaiting > 0)
			{
				problems.Raise(a, CityProblem.Sick, duration);
				if (Now - d.SickSince > 300)
					problems.Raise(a, CityProblem.Ambulance, duration);
			}

			if (d.CrimePressure >= 60)
				problems.Raise(a, CityProblem.Crime, duration);

			if (d.Service == null && Residents(d) + Workers(d) > 0 && d.Sat[(int)CatchmentGroup.Health] == 0
				&& d.Sat[(int)CatchmentGroup.Fire] == 0 && d.Sat[(int)CatchmentGroup.Police] == 0)
				problems.Raise(a, CityProblem.NoService, duration);
		}

		/// <summary>Ongoing events (fires, crimes in progress) every pulse.</summary>
		void RaiseEventProblems()
		{
			if (problems == null)
				return;

			for (var i = 0; i < fires.Count; i++)
			{
				var d = Data(fires[i]);
				if (d != null && d.Alive && d.Prop.Actor != null)
					problems.Raise(d.Prop.Actor, CityProblem.Fire);
			}

			for (var i = 0; i < crimeList.Count; i++)
			{
				var d = crimeList[i];
				if (d.Alive && d.CrimeEnd > 0 && d.Prop.Actor != null)
					problems.Raise(d.Prop.Actor, CityProblem.Crime);
			}
		}
	}
}
