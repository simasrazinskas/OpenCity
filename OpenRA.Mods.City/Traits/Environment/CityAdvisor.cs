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
	/// <summary>One advisor hint. Text keys: Key (the message, `$arg` available) and Key + "-hint" (what to do about it).</summary>
	public struct AdvisorMessage
	{
		/// <summary>Stable id of the rule, e.g. "no-power" (for dismissing or tracking).</summary>
		public string Id;

		/// <summary>Fluent key, "advisor-" + Id.</summary>
		public string Key;

		public string Arg;
		public ProblemTier Severity;

		/// <summary>Where to look (a sample building), valid when HasCell.</summary>
		public CPos Cell;
		public bool HasCell;

		/// <summary>Tick the condition started.</summary>
		public int Since;
	}

	/// <summary>Read side of the advisor/tutorial hints for the UI. Owner: ENV (CityAdvisor).</summary>
	public interface IAdvisorSource
	{
		/// <summary>Bumped whenever the message list changes.</summary>
		int Version { get; }

		/// <summary>Active hints, most severe first.</summary>
		IReadOnlyList<AdvisorMessage> Messages { get; }
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Evaluates simple rules about the city (no power, unemployment, deficit...) and publishes them as advisor messages with fluent keys.",
		"Read only for the UI; the rules read other systems' public state and never change anything.")]
	public class CityAdvisorInfo : TraitInfo
	{
		[Desc("Ticks between evaluations.")]
		public readonly int EvaluateTicks = 100;

		[Desc("A rule must hold this many consecutive evaluations before its message appears (filters flicker).")]
		public readonly int ConfirmChecks = 2;

		[Desc("Unemployment percent of the workforce that triggers a hint.")]
		public readonly int UnemploymentPercent = 15;

		[Desc("Residents without a home that trigger a hint.")]
		public readonly int HomelessLimit = 5;

		[Desc("Demand bar value (-100..100) that suggests zoning more of that kind.")]
		public readonly int DemandHint = 60;

		[Desc("Exposure (0..100) that triggers a pollution hint.")]
		public readonly int PollutionExposure = 40;

		[Desc("City traffic flow percent below which a congestion hint appears.")]
		public readonly int TrafficFlowBelow = 60;

		[Desc("Buildings sharing a problem before it becomes a hint.")]
		public readonly int ProblemBuildings = 3;

		public override object Create(ActorInitializer init) { return new CityAdvisor(init.Self, this); }
	}

	public class CityAdvisor : IAdvisorSource, ITick, ISync, ICityAutoTestReporter
	{
		public readonly CityAdvisorInfo Info;
		readonly World world;
		readonly List<AdvisorMessage> messages = [];
		readonly List<AdvisorMessage> candidates = [];
		readonly Dictionary<string, int> streak = [];
		readonly Dictionary<string, int> firstSeen = [];
		readonly List<string> tracked = [];
		CityManager manager;
		bool resolved;
		ICitizenPopulation citizens;
		IUtilityNetwork utilities;
		ICityProblems board;
		CityClimate climate;
		CityStatistics stats;
		CityDisasters disasters;

		public CityAdvisor(Actor self, CityAdvisorInfo info)
		{
			Info = info;
			world = self.World;
		}

		public int Version { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; }

		public IReadOnlyList<AdvisorMessage> Messages => messages;

		void Resolve()
		{
			if (resolved)
				return;

			resolved = true;
			var wa = world.WorldActor;
			board = wa.TraitsImplementing<ICityProblems>().FirstOrDefault();
			utilities = wa.TraitsImplementing<IUtilityNetwork>().FirstOrDefault();
			climate = wa.TraitOrDefault<CityClimate>();
			stats = wa.TraitOrDefault<CityStatistics>();
			disasters = wa.TraitOrDefault<CityDisasters>();
			citizens = wa.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
			foreach (var p in world.Players)
			{
				if (!p.Playable)
					continue;

				manager = p.PlayerActor.TraitOrDefault<CityManager>();
				citizens ??= p.PlayerActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
				break;
			}
		}

		void ITick.Tick(Actor self)
		{
			if (world.WorldTick % Math.Max(1, Info.EvaluateTicks) != 31)
				return;

			Resolve();
			if (manager == null)
				return;

			candidates.Clear();
			Evaluate();
			Merge();
		}

		void Add(string id, ProblemTier severity, string arg = null, CPos? cell = null)
		{
			candidates.Add(new AdvisorMessage
			{
				Id = id,
				Key = "advisor-" + id,
				Arg = arg,
				Severity = severity,
				Cell = cell ?? CPos.Zero,
				HasCell = cell.HasValue && cell.Value != CPos.Zero
			});
		}

		CPos? SampleCell(CityProblem problem)
		{
			if (board == null)
				return null;

			var summary = board.Summary;
			for (var i = 0; i < summary.Count; i++)
				if (summary[i].Problem == problem)
					return summary[i].Cell;

			return null;
		}

		bool ManyProblems(CityProblem problem, out int count)
		{
			count = board?.Count(problem) ?? 0;
			return count >= Info.ProblemBuildings;
		}

		static string S(int v) { return v.ToString(CultureInfo.InvariantCulture); }

		void Evaluate()
		{
			var m = manager;
			var buildings = m.BuildingCount;
			if (buildings == 0)
			{
				Add("first-steps", ProblemTier.Info);
				return;
			}

			var power = utilities?.PowerProduced ?? m.PowerProduced;
			var powerUse = utilities?.PowerConsumed ?? m.PowerConsumed;
			var water = utilities?.WaterProduced ?? m.WaterProduced;
			var waterUse = utilities?.WaterConsumed ?? m.WaterConsumed;

			if (board != null && board.Count(CityProblem.NoPower) > 0 && power == 0)
				Add("no-power", ProblemTier.Major, S(board.Count(CityProblem.NoPower)), SampleCell(CityProblem.NoPower));
			else if (ManyProblems(CityProblem.NoPower, out var nopower))
				Add("blackout", ProblemTier.Warning, S(nopower), SampleCell(CityProblem.NoPower));

			if (board != null && board.Count(CityProblem.NoWater) > 0 && water == 0)
				Add("no-water", ProblemTier.Major, S(board.Count(CityProblem.NoWater)), SampleCell(CityProblem.NoWater));
			else if (ManyProblems(CityProblem.NoWater, out var nowater))
				Add("water-outage", ProblemTier.Warning, S(nowater), SampleCell(CityProblem.NoWater));

			if (powerUse > 0 && power * 100 < powerUse * 95)
				Add("power-shortage", ProblemTier.Warning, S(100 - power * 100 / powerUse));
			else if (climate != null && climate.PowerDemandPercent >= 140 && powerUse > 0 && power * 100 < powerUse * 125)
				Add("winter-power", ProblemTier.Info, S(climate.PowerDemandPercent));

			if (waterUse > 0 && water * 100 < waterUse * 95)
				Add("water-shortage", ProblemTier.Warning, S(100 - water * 100 / waterUse));
			else if (climate != null && climate.WaterDemandPercent >= 120 && waterUse > 0 && water * 100 < waterUse * 125)
				Add("summer-water", ProblemTier.Info, S(climate.WaterDemandPercent));

			var workers = citizens?.Workers ?? m.Workers;
			var unemployed = citizens?.Unemployed ?? m.Unemployed;
			if (workers >= 20 && unemployed * 100 >= workers * Info.UnemploymentPercent)
				Add("unemployment", ProblemTier.Warning, S(unemployed * 100 / Math.Max(1, workers)));
			else if (workers >= 20 && m.Jobs > (workers - unemployed) * 3 / 2 + 20)
				Add("labor-shortage", ProblemTier.Info, S(m.Jobs - (workers - unemployed)));

			if (m.Funds < 0)
				Add("broke", ProblemTier.Major);
			else if (m.MonthlyBalance < 0)
				Add("deficit", ProblemTier.Warning, CityUtils.FormatMoney(-m.MonthlyBalance));

			if (ManyProblems(CityProblem.NoRoad, out var noroad))
				Add("no-road", ProblemTier.Problem, S(noroad), SampleCell(CityProblem.NoRoad));

			if (ManyProblems(CityProblem.Garbage, out var garbage))
				Add("garbage", ProblemTier.Warning, S(garbage), SampleCell(CityProblem.Garbage));

			if (ManyProblems(CityProblem.Crime, out var crime))
				Add("crime", ProblemTier.Warning, S(crime), SampleCell(CityProblem.Crime));

			if (ManyProblems(CityProblem.Fire, out var fire))
				Add("fire", ProblemTier.Major, S(fire), SampleCell(CityProblem.Fire));

			if (ManyProblems(CityProblem.Sick, out var sick))
				Add("sick", ProblemTier.Problem, S(sick), SampleCell(CityProblem.Sick));

			if (ManyProblems(CityProblem.Abandoned, out var abandoned))
				Add("abandoned", ProblemTier.Problem, S(abandoned), SampleCell(CityProblem.Abandoned));

			if (ManyProblems(CityProblem.Traffic, out var jam) || (stats != null && stats.Latest("traffic.flow") is > 0 and var flow && flow < Info.TrafficFlowBelow))
				Add("traffic", ProblemTier.Warning, S(jam), SampleCell(CityProblem.Traffic));

			if (stats != null)
			{
				var air = stats.Latest("pollution.air");
				var ground = stats.Latest("pollution.ground");
				var noise = stats.Latest("pollution.noise");
				if (Math.Max(air, Math.Max(ground, noise)) >= Info.PollutionExposure)
					Add("pollution", ProblemTier.Warning, S(Math.Max(air, Math.Max(ground, noise))));
			}

			if ((citizens?.Homeless ?? 0) >= Info.HomelessLimit)
				Add("homeless", ProblemTier.Problem, S(citizens.Homeless));

			if (disasters != null && disasters.Enabled && disasters.FloodLevel > 0)
				Add("flood", ProblemTier.Major, S(disasters.FloodedBuildings), SampleCell(CityProblem.Flooded));

			if (m.DemandResidential >= Info.DemandHint)
				Add("zone-residential", ProblemTier.Info, S(m.DemandResidential));

			if (m.DemandCommercial >= Info.DemandHint)
				Add("zone-commercial", ProblemTier.Info, S(m.DemandCommercial));

			if (m.DemandIndustrial >= Info.DemandHint)
				Add("zone-industrial", ProblemTier.Info, S(m.DemandIndustrial));
		}

		// Keeps a rule's message only after it held for ConfirmChecks evaluations, and drops it as soon as it stops holding.
		void Merge()
		{
			var now = world.WorldTick;
			var confirm = Math.Max(1, Info.ConfirmChecks);
			var next = new List<AdvisorMessage>(candidates.Count);
			var seen = new HashSet<string>();
			for (var i = 0; i < candidates.Count; i++)
			{
				var c = candidates[i];
				seen.Add(c.Id);
				streak.TryGetValue(c.Id, out var n);
				n++;
				streak[c.Id] = n;
				if (n == 1)
				{
					firstSeen[c.Id] = now;
					tracked.Add(c.Id);
				}

				if (n < confirm)
					continue;

				c.Since = firstSeen[c.Id];
				next.Add(c);
			}

			// Forget rules that no longer hold (iterates the id list, never the dictionary).
			for (var i = tracked.Count - 1; i >= 0; i--)
			{
				if (seen.Contains(tracked[i]))
					continue;

				streak.Remove(tracked[i]);
				firstSeen.Remove(tracked[i]);
				tracked.RemoveAt(i);
			}

			next.Sort((a, b) =>
			{
				var c = ProblemCatalog.Priority(b.Severity).CompareTo(ProblemCatalog.Priority(a.Severity));
				return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
			});

			var changed = next.Count != messages.Count;
			for (var i = 0; !changed && i < next.Count; i++)
				changed = next[i].Id != messages[i].Id || next[i].Arg != messages[i].Arg || next[i].Severity != messages[i].Severity;

			if (!changed)
				return;

			messages.Clear();
			messages.AddRange(next);
			Version++;
			unchecked
			{
				var h = 17;
				for (var i = 0; i < messages.Count; i++)
					h = h * 31 + messages[i].Id.Length * 7 + (int)messages[i].Severity + messages[i].Id[0];

				StateHash = h;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var ids = messages.Count == 0 ? "none" : string.Join(',', messages.Select(m => m.Id));
			return "advisor " + ids + " hash=" + StateHash.ToString(CultureInfo.InvariantCulture);
		}
	}
}
