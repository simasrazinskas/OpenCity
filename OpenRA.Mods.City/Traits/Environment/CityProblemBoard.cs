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
using System.Globalization;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("City-wide problem board: evaluates the built-in building problems every pulse, merges flags raised through ICityProblems and keeps the summary.")]
	public class CityProblemBoardInfo : TraitInfo
	{
		[Desc("Ticks between evaluations.")]
		public readonly int PulseTicks = 25;

		[Desc("Default lifetime of a raised flag in ticks.")]
		public readonly int DefaultDuration = 51;

		[Desc("Happiness below this shows the unhappy icon.")]
		public readonly int UnhappyBelow = 25;

		[Desc("Workplaces with fewer filled jobs than this percent show 'not enough workers'.")]
		public readonly int WorkersBelowPercent = 50;

		[Desc("Pollution exposure (0..100) that raises a warning for homes and offices.")]
		public readonly int ResidentialPollutionLimit = 50;

		[Desc("Pollution exposure (0..100) that raises a warning for commercial buildings.")]
		public readonly int CommercialPollutionLimit = 70;

		[Desc("Road congestion (0..100) at a building's access road that counts as a traffic jam.")]
		public readonly int TrafficJamLoad = 85;

		[Desc("Crime pressure (0..100) of a building that shows the crime icon.")]
		public readonly int CrimeProblemPressure = 75;

		[Desc("A chirp is posted when at least this many buildings share a problem.")]
		public readonly int ChirpMinBuildings = 3;

		public override object Create(ActorInitializer init) { return new CityProblemBoard(init.Self, this); }
	}

	public class CityProblemBoard : ICityProblems, ITick, ISync, ICityAutoTestReporter
	{
		public readonly CityProblemBoardInfo Info;
		readonly World world;
		readonly int[] counts = new int[ProblemCatalog.Count + 1];
		readonly int[] tiers = new int[ProblemCatalog.Count + 1];
		readonly uint[] sampleActor = new uint[ProblemCatalog.Count + 1];
		readonly CPos[] sampleCell = new CPos[ProblemCatalog.Count + 1];
		readonly int[] minPriority = new int[ProblemCatalog.Count + 1];
		readonly List<ProblemSummary> summary = [];
		bool resolved;
		CityStatistics stats;
		IUtilityNetwork utilities;
		IPropertyRegistry properties;
		ITrafficService traffic;
		PollutionLayer pollution;
		ServiceSimulation services;
		bool hasCitizens;

		public CityProblemBoard(Actor self, CityProblemBoardInfo info)
		{
			Info = info;
			world = self.World;
		}

		[VerifySync]
		public int StateHash { get; private set; }

		public int Version { get; private set; }

		public IReadOnlyList<ProblemSummary> Summary => summary;

		public int Count(CityProblem problem) { return counts[(int)problem]; }

		/// <summary>Number of buildings showing a problem of at least this tier.</summary>
		public int CountAtLeast(ProblemTier tier)
		{
			var n = 0;
			for (var i = 0; i < summary.Count; i++)
				if (ProblemCatalog.Priority(summary[i].Tier) >= ProblemCatalog.Priority(tier))
					n += summary[i].Count;

			return n;
		}

		public void Raise(Actor building, CityProblem problem, int durationTicks = 0)
		{
			var p = building?.TraitOrDefault<CityProblems>();
			if (p == null || problem == CityProblem.None)
				return;

			p.Raise(problem, world.WorldTick + (durationTicks > 0 ? durationTicks : Info.DefaultDuration));
		}

		public void Clear(Actor building, CityProblem problem)
		{
			building?.TraitOrDefault<CityProblems>()?.Clear(problem);
		}

		public bool Has(Actor building, CityProblem problem)
		{
			var p = building?.TraitOrDefault<CityProblems>();
			return p != null && p.Has(problem);
		}

		public uint Mask(Actor building)
		{
			return building?.TraitOrDefault<CityProblems>()?.Active ?? 0;
		}

		void ResolveProviders()
		{
			if (resolved)
				return;

			resolved = true;
			var wa = world.WorldActor;
			stats = wa.TraitOrDefault<CityStatistics>();
			utilities = wa.TraitsImplementing<IUtilityNetwork>().FirstOrDefault();
			properties = wa.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			traffic = wa.TraitsImplementing<ITrafficService>().FirstOrDefault();
			pollution = wa.TraitOrDefault<PollutionLayer>();
			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				services = p.PlayerActor.TraitOrDefault<ServiceSimulation>();
				hasCitizens = p.PlayerActor.TraitsImplementing<ICitizenPopulation>().Any() || wa.TraitsImplementing<ICitizenPopulation>().Any();
				break;
			}
		}

		void ITick.Tick(Actor self)
		{
			if (world.WorldTick % Math.Max(1, Info.PulseTicks) != 7)
				return;

			ResolveProviders();
			var now = world.WorldTick;
			Array.Clear(counts);
			Array.Clear(sampleActor);
			Array.Fill(minPriority, -2);

			foreach (var pair in world.ActorsWithTrait<CityProblems>())
			{
				var actor = pair.Actor;
				var p = pair.Trait;
				var building = actor.TraitOrDefault<CityBuilding>();
				p.Update(building != null ? Evaluate(actor, building) : 0, now);

				var active = p.Active;
				while (active != 0)
				{
					var i = System.Numerics.BitOperations.TrailingZeroCount(active);
					active &= active - 1;
					counts[i]++;
					var tier = p.Tier((CityProblem)i, now);
					var pri = ProblemCatalog.Priority(tier);
					if (pri > minPriority[i])
					{
						minPriority[i] = pri;
						tiers[i] = (int)tier;
						sampleActor[i] = actor.ActorID;
						sampleCell[i] = building != null && building.Cells.Length > 0 ? building.Cells[0] : actor.Location;
					}
				}
			}

			RebuildSummary();
			PostChirps();
		}

		void RebuildSummary()
		{
			summary.Clear();
			unchecked
			{
				var h = 17;
				for (var i = 1; i < counts.Length; i++)
				{
					if (counts[i] == 0)
						continue;

					summary.Add(new ProblemSummary
					{
						Problem = (CityProblem)i,
						Tier = (ProblemTier)tiers[i],
						Count = counts[i],
						Cell = sampleCell[i],
						ActorId = sampleActor[i]
					});
					h = h * 31 + i * 1000 + counts[i];
				}

				StateHash = h;
			}

			// Worst tier first, then the more widespread problem.
			summary.Sort((a, b) =>
			{
				var c = ProblemCatalog.Priority(b.Tier).CompareTo(ProblemCatalog.Priority(a.Tier));
				if (c != 0)
					return c;

				c = b.Count.CompareTo(a.Count);
				return c != 0 ? c : ((int)a.Problem).CompareTo((int)b.Problem);
			});

			Version++;
		}

		static readonly (CityProblem Problem, string Key)[] ChirpProblems =
		[
			(CityProblem.NoPower, "chirp-problem-nopower"),
			(CityProblem.NoWater, "chirp-problem-nowater"),
			(CityProblem.Garbage, "chirp-problem-garbage"),
			(CityProblem.Fire, "chirp-problem-fire"),
			(CityProblem.Crime, "chirp-problem-crime"),
			(CityProblem.Abandoned, "chirp-problem-abandoned"),
			(CityProblem.NoRoad, "chirp-problem-noroad"),
			(CityProblem.Traffic, "chirp-problem-traffic"),
		];

		void PostChirps()
		{
			if (stats == null)
				return;

			foreach (var (problem, key) in ChirpProblems)
			{
				var n = counts[(int)problem];
				if (n < Info.ChirpMinBuildings)
					continue;

				var variant = 1 + EnvHash.Hash(world.WorldTick, (int)problem) % 2;
				stats.Post(key + "-" + variant.ToString(CultureInfo.InvariantCulture), 0, n.ToString(CultureInfo.InvariantCulture), n, sampleCell[(int)problem]);
			}
		}

		// Built-in conditions, derived from the simulation state of one building.
		uint Evaluate(Actor actor, CityBuilding b)
		{
			// Decoration owned by a player without a city (e.g. the shellmap town) never shows problems.
			if (b.Manager == null)
				return 0;

			var g = b.Growable;
			if (g != null && g.Abandoned)
				return 1u << (int)CityProblem.Abandoned;

			if (g != null && g.UnderConstruction)
				return 0;

			var mask = 0u;
			if (!b.HasPower && b.Info.PowerUse > 0)
				mask |= 1u << (int)CityProblem.NoPower;

			if (!b.HasWater && b.Info.WaterUse > 0)
				mask |= 1u << (int)CityProblem.NoWater;
			else if (utilities != null && b.Info.WaterUse > 0 && !utilities.HasSewage(actor))
				mask |= 1u << (int)CityProblem.NoSewage;

			if (!b.HasRoadAccess)
				mask |= 1u << (int)CityProblem.NoRoad;

			if (!b.IsOperational || b.Category == ZoneCategory.None)
				return mask;

			if (b.Happiness < Info.UnhappyBelow)
				mask |= 1u << (int)CityProblem.Unhappy;

			// Look the property up once: citizens and services keep their per-building state there.
			var prop = properties?.GetByActor(actor);
			if (b.Category != ZoneCategory.Residential)
			{
				// With the citizen simulation the property registry holds the real job numbers.
				var slots = hasCitizens && prop != null ? prop.TotalJobSlots : b.Info.MaxJobs;
				var filled = hasCitizens && prop != null ? prop.TotalJobsFilled : b.Workers;
				if (slots > 0 && filled * 100 < slots * Info.WorkersBelowPercent)
					mask |= 1u << (int)CityProblem.NoWorkers;
			}

			// City services publish their per-building needs through public getters.
			if (services != null && prop != null)
			{
				if (services.IsBurning(prop.Id))
					mask |= 1u << (int)CityProblem.Fire;

				if (services.GetPileLevel(prop.Id) >= 2)
					mask |= 1u << (int)CityProblem.Garbage;

				if (services.GetSickWaiting(prop.Id) > 0)
					mask |= 1u << (int)CityProblem.Sick;

				if (services.GetCrimePressure(prop.Id) >= Info.CrimeProblemPressure)
					mask |= 1u << (int)CityProblem.Crime;
			}

			if (b.Category != ZoneCategory.Industrial && b.Cells.Length > 0 && pollution != null)
			{
				var limit = b.Category == ZoneCategory.Commercial ? Info.CommercialPollutionLimit : Info.ResidentialPollutionLimit;
				pollution.Exposure(b.Cells[0], out var eg, out var ea, out var en);
				if (ea > limit)
					mask |= 1u << (int)CityProblem.AirPollution;
				if (eg > limit)
					mask |= 1u << (int)CityProblem.GroundPollution;
				if (en > limit)
					mask |= 1u << (int)CityProblem.Noise;
			}

			if (traffic != null && prop != null && prop.AccessRoad != CPos.Zero && traffic.GetTrafficLoad(prop.AccessRoad) >= Info.TrafficJamLoad)
				mask |= 1u << (int)CityProblem.Traffic;

			return mask;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var parts = new List<string>();
			for (var i = 1; i < counts.Length; i++)
				if (counts[i] > 0)
					parts.Add(((CityProblem)i).ToString() + "=" + counts[i].ToString(CultureInfo.InvariantCulture));

			return "problems " + (parts.Count == 0 ? "none" : string.Join(' ', parts)) + " hash=" + StateHash.ToString(CultureInfo.InvariantCulture);
		}
	}
}
