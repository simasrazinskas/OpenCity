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
	/// <summary>One unlock rule of a signature building with the current value, for the placement panel.</summary>
	public struct SignatureRuleStatus
	{
		/// <summary>ZoneCells, Level5Count, Happiness, Population, Milestone or ServiceBuilt.</summary>
		public string Kind;

		/// <summary>Zone type name or service actor name, or null.</summary>
		public string Target;

		public int Have;
		public int Need;
		public bool Met;
	}

	/// <summary>Everything the UI needs to list a signature building and explain what is missing.</summary>
	public sealed class SignatureStatus
	{
		public string Name;
		public ZoneType Zone;

		/// <summary>Footprint in cells (for the placement ghost).</summary>
		public int Width, Depth;

		public bool Unlocked;
		public bool Built;

		/// <summary>0..100 over all rules.</summary>
		public int Progress;

		public SignatureRuleStatus[] Rules;

		/// <summary>Bonuses shown in the panel.</summary>
		public int WellBeing, WellBeingRadius, Attractiveness, EfficiencyPercent, PollutionReductionPercent;

		/// <summary>Fluent key of the description (actor-sig-*.description).</summary>
		public string DescriptionKey;
	}

	/// <summary>Player trait of the zoning WP: which signature buildings may be placed, and what the built ones add.</summary>
	public interface ISignatureUnlocks
	{
		/// <summary>Actor names of all signature buildings, ordered.</summary>
		IReadOnlyList<string> Signatures { get; }

		/// <summary>True if the unlock rules were met (latched) and none of this type exists yet (once per city).</summary>
		bool IsUnlocked(string actorName);

		bool IsBuilt(string actorName);

		/// <summary>0..100 progress over all rules of the signature (100 = rules met).</summary>
		int Progress(string actorName);

		/// <summary>Full status for the placement panel (rules with have/need, footprint, bonuses), or null for unknown names.</summary>
		SignatureStatus GetStatus(string actorName);

		/// <summary>Well-being points (happiness) from signature buildings around a cell.</summary>
		int WellBeingAt(CPos cell);

		/// <summary>Tourist attractiveness of all built signature buildings.</summary>
		int Attractiveness { get; }
	}

	[TraitLocation(SystemActors.Player)]
	[Desc("Evaluates the unlock rules of signature buildings every pulse and answers ISignatureUnlocks.")]
	public class SignatureUnlocksInfo : TraitInfo
	{
		[Desc("Ticks between evaluations.")]
		public readonly int Interval = 100;

		public override object Create(ActorInitializer init) { return new SignatureUnlocks(init.Self, this); }
	}

	public class SignatureUnlocks : ISignatureUnlocks, ITick, IWorldLoaded, ICityAutoTestReporter
	{
		sealed class Rule
		{
			public string Kind;
			public ZoneType Zone;
			public string Text;
			public int Amount;
		}

		sealed class Entry
		{
			public string Name;
			public SignatureBuildingInfo Info;
			public Rule[] Rules;
			public bool Unlocked;
			public int Progress;
			public bool Built;
		}

		readonly SignatureUnlocksInfo info;
		readonly Actor self;
		readonly bool sandbox;
		readonly List<Entry> entries = [];
		readonly List<string> names = [];
		readonly List<Actor> built = [];
		PropertyRegistry registry;
		CityManager manager;
		ICitizenPopulation citizens;
		IProgression progression;

		public SignatureUnlocks(Actor self, SignatureUnlocksInfo info)
		{
			this.info = info;
			this.self = self;
			sandbox = CitySandbox.EnabledFor(self.World);
		}

		public IReadOnlyList<string> Signatures => names;

		public int Attractiveness { get; private set; }

		void IWorldLoaded.WorldLoaded(World w, Graphics.WorldRenderer wr)
		{
			registry = w.WorldActor.TraitOrDefault<PropertyRegistry>();
			manager = self.TraitOrDefault<CityManager>();
			citizens = ZoningLookup.Find<ICitizenPopulation>(w);
			progression = self.TraitsImplementing<IProgression>().FirstOrDefault() ?? ZoningLookup.Find<IProgression>(w);

			foreach (var kv in w.Map.Rules.Actors.OrderBy(a => a.Key, StringComparer.Ordinal))
			{
				var sig = kv.Value.TraitInfoOrDefault<SignatureBuildingInfo>();
				if (sig == null)
					continue;

				var rules = new List<Rule>();
				foreach (var text in sig.Unlock)
					rules.Add(Parse(text));

				entries.Add(new Entry { Name = kv.Key, Info = sig, Rules = rules.ToArray(), Unlocked = sandbox, Progress = sandbox ? 100 : 0 });
				names.Add(kv.Key);
			}
		}

		static Rule Parse(string text)
		{
			var parts = text.Split(':');
			var rule = new Rule { Kind = parts[0].Trim() };
			switch (rule.Kind)
			{
				case "ZoneCells":
				case "Level5Count":
					rule.Zone = Enum.Parse<ZoneType>(parts[1].Trim());
					rule.Amount = int.Parse(parts[2], CultureInfo.InvariantCulture);
					break;
				case "ServiceBuilt":
					rule.Text = parts[1].Trim();
					rule.Amount = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 1;
					break;
				default:
					rule.Amount = int.Parse(parts[1], CultureInfo.InvariantCulture);
					break;
			}

			return rule;
		}

		Entry Find(string name)
		{
			foreach (var e in entries)
				if (e.Name == name)
					return e;

			return null;
		}

		public bool IsUnlocked(string actorName)
		{
			var e = Find(actorName);
			return e != null && e.Unlocked && !e.Built;
		}

		public bool IsBuilt(string actorName) { return Find(actorName)?.Built ?? false; }

		public int Progress(string actorName) { return Find(actorName)?.Progress ?? 0; }

		public SignatureStatus GetStatus(string actorName)
		{
			var e = Find(actorName);
			if (e == null)
				return null;

			var rules = new SignatureRuleStatus[e.Rules.Length];
			for (var i = 0; i < rules.Length; i++)
			{
				var r = e.Rules[i];
				var have = RuleHave(r);
				rules[i] = new SignatureRuleStatus
				{
					Kind = r.Kind,
					Target = r.Kind == "ServiceBuilt" ? r.Text : r.Zone != ZoneType.None ? r.Zone.ToString() : null,
					Have = have,
					Need = r.Amount,
					Met = sandbox || r.Amount <= 0 || have >= r.Amount,
				};
			}

			var dims = self.World.Map.Rules.Actors[actorName].TraitInfoOrDefault<Mods.Common.Traits.BuildingInfo>()?.Dimensions ?? new CVec(3, 3);
			var growable = self.World.Map.Rules.Actors[actorName].TraitInfoOrDefault<GrowableBuildingInfo>();
			return new SignatureStatus
			{
				Name = actorName,
				Zone = growable?.Zone ?? ZoneType.None,
				Width = dims.X,
				Depth = dims.Y,
				Unlocked = e.Unlocked,
				Built = e.Built,
				Progress = e.Progress,
				Rules = rules,
				WellBeing = e.Info.WellBeing,
				WellBeingRadius = e.Info.WellBeingRadius,
				Attractiveness = e.Info.Attractiveness,
				EfficiencyPercent = e.Info.EfficiencyPercent,
				PollutionReductionPercent = e.Info.PollutionReductionPercent,
				DescriptionKey = e.Info.Description,
			};
		}

		public int WellBeingAt(CPos cell)
		{
			var total = 0;
			foreach (var a in built)
			{
				var sig = a.TraitOrDefault<SignatureBuilding>();
				if (sig == null || sig.Info.WellBeing == 0 || a.Disposed)
					continue;

				var d = Math.Max(Math.Abs(a.Location.X - cell.X), Math.Abs(a.Location.Y - cell.Y));
				if (d <= sig.Info.WellBeingRadius)
					total += sig.Info.WellBeing;
			}

			return total;
		}

		void ITick.Tick(Actor self)
		{
			if (entries.Count == 0 || self.World.WorldTick % Math.Max(1, info.Interval) != 0)
				return;

			// Which signatures exist (in ActorID order), and their attractiveness.
			built.Clear();
			Attractiveness = 0;
			foreach (var e in entries)
				e.Built = false;

			foreach (var a in self.World.ActorsHavingTrait<SignatureBuilding>())
			{
				if (a.Owner != self.Owner)
					continue;

				built.Add(a);
				var sig = a.Trait<SignatureBuilding>();
				Attractiveness += sig.Info.Attractiveness;
				var e = Find(a.Info.Name);
				if (e != null)
					e.Built = true;
			}

			foreach (var e in entries)
			{
				if (e.Unlocked)
				{
					e.Progress = 100;
					continue;
				}

				var met = 0;
				var progress = 0;
				foreach (var r in e.Rules)
				{
					var p = RuleProgress(r);
					progress += p;
					if (p >= 100)
						met++;
				}

				e.Progress = e.Rules.Length == 0 ? 100 : progress / e.Rules.Length;
				e.Unlocked = met == e.Rules.Length;
			}
		}

		// 0..100 progress of one rule.
		int RuleProgress(Rule r)
		{
			if (r.Kind != "ZoneCells" && r.Kind != "Level5Count" && r.Kind != "Happiness" && r.Kind != "Population" && r.Kind != "Milestone" && r.Kind != "ServiceBuilt")
				return 100;

			var have = RuleHave(r);
			return r.Amount <= 0 ? 100 : Math.Clamp(have * 100 / r.Amount, 0, 100);
		}

		int RuleHave(Rule r)
		{
			switch (r.Kind)
			{
				case "ZoneCells": return CellsOf(r.Zone, 1);
				case "Level5Count": return LotsOf(r.Zone, 5);
				case "Happiness": return citizens != null ? citizens.AverageHappiness : manager?.AverageHappiness ?? 0;
				case "Population": return citizens != null ? citizens.Population : manager?.Population ?? 0;
				case "Milestone": return progression != null ? progression.MilestoneIndex : -1;
				case "ServiceBuilt": return CountActors(r.Text);
				default: return 0;
			}
		}

		int CellsOf(ZoneType zone, int level)
		{
			if (registry == null)
				return 0;

			registry.CountLots(zone, level, out _, out var cells);
			return cells;
		}

		int LotsOf(ZoneType zone, int level)
		{
			if (registry == null)
				return 0;

			registry.CountLots(zone, level, out var lots, out _);
			return lots;
		}

		int CountActors(string type)
		{
			var n = 0;
			foreach (var a in self.World.Actors)
				if (a.Owner == self.Owner && !a.Disposed && a.Info.Name == type)
					n++;

			return n;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var parts = entries.Select(e => $"{e.Name}:{(e.Built ? "built" : e.Unlocked ? "open" : e.Progress + "%")}");
			var detail = "";
			foreach (var e in entries)
			{
				if (e.Built)
					continue;

				var st = GetStatus(e.Name);
				detail = $" next={e.Name}({st.Width}x{st.Depth}) rules=[{string.Join(" ", st.Rules.Select(r => r.Kind + ":" + r.Have + "/" + r.Need))}]";
				break;
			}

			return "signatures [" + string.Join(" ", parts) + "]" + detail;
		}
	}
}
