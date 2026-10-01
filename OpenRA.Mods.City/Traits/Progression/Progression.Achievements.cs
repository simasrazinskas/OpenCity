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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("One achievement (data only). The instance name (after @) is the id: fluent keys 'achievement-<id>' / 'achievement-<id>-desc'.")]
	public class ProgressionAchievementInfo : TraitInfo
	{
		[Desc("All conditions must hold ('stat>=value', also <=, >, <, ==). Stats: population, happiness, funds, monthly_balance, ",
			"service_categories, transit_passengers, pollution, industrial_jobs, companies, educated_pct, tiles_owned, city_policies, ",
			"districts, districts_with_policies, signatures, attractiveness, tourist_visits, milestone, nodes_owned, roads, achievements.")]
		public readonly string[] Conditions = [];

		[Desc("The conditions must hold at the end of this many consecutive months (0 = unlock as soon as they hold).")]
		public readonly int Months = 0;

		[Desc("XP granted once when unlocked (0 = none).")]
		public readonly int Xp = 0;

		public override object Create(ActorInitializer init) { return new ProgressionDataMarker(); }
	}

	/// <summary>Runtime view of an achievement for the UI panel.</summary>
	public sealed class AchievementData
	{
		public int Index;
		public string Id;
		public string NameKey, DescKey;
		public bool Unlocked;

		/// <summary>World tick of the unlock, -1 while locked.</summary>
		public int UnlockedTick = -1;

		/// <summary>0..100 progress towards the first condition (or the month streak while the conditions hold).</summary>
		public int Progress;

		public int Months, Streak, Xp;
	}

	// Achievements: yaml catalogue evaluated every few pulses against a stat snapshot; unlocks are permanent for the game.
	public partial class Progression
	{
		sealed class Cond
		{
			public int Stat;
			public string Op;
			public int Value;
		}

		readonly List<AchievementData> achievements = [];
		readonly List<Cond[]> achievementConds = [];
		bool[] achieved = [];
		int[] achievedTick = [];
		int[] streaks = [];

		static readonly string[] StatNames =
		[
			"population", "happiness", "funds", "monthly_balance", "service_categories", "transit_passengers", "pollution",
			"industrial_jobs", "companies", "educated_pct", "tiles_owned", "city_policies", "districts", "districts_with_policies",
			"signatures", "attractiveness", "tourist_visits", "milestone", "nodes_owned", "roads", "achievements"
		];

		readonly int[] stats = new int[StatNames.Length];

		void BuildAchievements()
		{
			foreach (var ai in self.Info.TraitInfos<ProgressionAchievementInfo>())
			{
				var id = ai.InstanceName;
				if (string.IsNullOrEmpty(id))
					continue;

				var conds = new List<Cond>();
				foreach (var text in ai.Conditions)
				{
					var c = ParseCondition(text);
					if (c == null)
						CatalogueWarnings.Add($"achievement {id}: bad condition '{text}'");
					else
						conds.Add(c);
				}

				achievements.Add(new AchievementData
				{
					Index = achievements.Count,
					Id = id,
					NameKey = "achievement-" + id,
					DescKey = "achievement-" + id + "-desc",
					Months = ai.Months,
					Xp = ai.Xp
				});
				achievementConds.Add(conds.ToArray());
			}

			achieved = new bool[achievements.Count];
			achievedTick = new int[achievements.Count];
			streaks = new int[achievements.Count];
		}

		static Cond ParseCondition(string text)
		{
			string[] ops = [">=", "<=", "==", ">", "<"];
			foreach (var op in ops)
			{
				var i = text.IndexOf(op, StringComparison.Ordinal);
				if (i <= 0)
					continue;

				var stat = Array.IndexOf(StatNames, text[..i].Trim());
				if (stat < 0 || !int.TryParse(text[(i + op.Length)..].Trim(), out var v))
					return null;

				return new Cond { Stat = stat, Op = op, Value = v };
			}

			return null;
		}

		static bool Holds(Cond c, int value)
		{
			return c.Op switch
			{
				">=" => value >= c.Value,
				"<=" => value <= c.Value,
				">" => value > c.Value,
				"<" => value < c.Value,
				_ => value == c.Value
			};
		}

		public int AchievementCount => achievements.Count;

		public int UnlockedAchievementCount
		{
			get
			{
				var n = 0;
				for (var i = 0; i < achieved.Length; i++)
					if (achieved[i])
						n++;

				return n;
			}
		}

		/// <summary>Current stat snapshot value by name (-1 for unknown names). Read-only, for tooltips and debugging.</summary>
		public int GetStat(string name)
		{
			var i = Array.IndexOf(StatNames, name);
			return i < 0 ? -1 : stats[i];
		}

		/// <summary>Fresh snapshot of all achievements (UI only).</summary>
		public IReadOnlyList<AchievementData> Achievements
		{
			get
			{
				for (var i = 0; i < achievements.Count; i++)
				{
					var a = achievements[i];
					a.Unlocked = achieved[i];
					a.UnlockedTick = achieved[i] ? achievedTick[i] : -1;
					a.Streak = streaks[i];
					a.Progress = AchievementProgress(i);
				}

				return achievements;
			}
		}

		int AchievementProgress(int i)
		{
			if (achieved[i])
				return 100;

			var conds = achievementConds[i];
			var a = achievements[i];
			var met = true;
			var progress = 100;
			var first = true;
			foreach (var c in conds)
			{
				var v = stats[c.Stat];
				if (!Holds(c, v))
				{
					met = false;
					if (first && (c.Op == ">=" || c.Op == ">") && c.Value > 0)
						progress = Math.Clamp((int)(100L * v / c.Value), 0, 99);
					else if (first)
						progress = 0;
				}

				first = false;
			}

			if (met && a.Months > 0)
				return Math.Clamp(streaks[i] * 100 / a.Months, 0, 99);

			return met ? 99 : progress;
		}

		// Called every few pulses (snapshot + immediate achievements) and with monthly = true on a new month (streaks).
		void UpdateAchievements(bool monthly)
		{
			if (achievements.Count == 0)
				return;

			ComputeStats();
			for (var i = 0; i < achievements.Count; i++)
			{
				if (achieved[i])
					continue;

				var all = true;
				foreach (var c in achievementConds[i])
					if (!Holds(c, stats[c.Stat]))
						all = false;

				if (achievementConds[i].Length == 0)
					all = false;

				var months = achievements[i].Months;
				if (months > 0)
				{
					if (!monthly)
						continue;

					streaks[i] = all ? streaks[i] + 1 : 0;
					all = all && streaks[i] >= months;
				}

				if (all)
					Unlock(i);
			}
		}

		void Unlock(int i)
		{
			achieved[i] = true;
			achievedTick[i] = world.WorldTick;
			var a = achievements[i];
			if (a.Xp > 0)
				AddXpFrom(XpSource.Achievement, a.Xp);

			var name = FluentProvider.GetMessage(a.NameKey);
			Notify("notification-prg-achievement", name, "name", name);
		}

		int AchievementHash()
		{
			unchecked
			{
				var h = 0;
				for (var i = 0; i < achieved.Length; i++)
					h = h * 31 + (achieved[i] ? achievedTick[i] + 1 : streaks[i]);

				return h;
			}
		}
	}
}
