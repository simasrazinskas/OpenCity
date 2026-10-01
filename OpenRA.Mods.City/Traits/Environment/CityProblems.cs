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
	/// <summary>Problems and events that can be shown above a building. The numeric value is also the glyph frame + 1 and the bit index in masks.</summary>
	public enum CityProblem : byte
	{
		None = 0,
		NoPower,
		NoWater,
		NoSewage,
		DirtyWater,
		NoRoad,
		Abandoned,
		Unhappy,
		NoWorkers,
		NoCustomers,
		NoGoods,
		Garbage,
		Fire,
		Crime,
		Sick,
		Ambulance,
		HighRent,
		Traffic,
		AirPollution,
		GroundPollution,
		Noise,
		LeveledUp,
		Collapsed,
		NoService,
		Flooded,
	}

	/// <summary>Severity tier (the background tile of the icon), as in CS2: grey, light blue, yellow, orange, red, dark red, black. Good is green.</summary>
	public enum ProblemTier : byte
	{
		Minimal = 0,
		Info,
		Problem,
		Warning,
		Major,
		Error,
		Fatal,
		Good,
	}

	/// <summary>Static description of one problem kind.</summary>
	public readonly struct ProblemKind
	{
		public readonly CityProblem Problem;

		/// <summary>Fluent key of the human-readable name.</summary>
		public readonly string NameKey;

		public readonly ProblemTier BaseTier;

		/// <summary>Tier after the problem persisted for EscalateTicks.</summary>
		public readonly ProblemTier EscalatedTier;

		public readonly int EscalateTicks;

		/// <summary>The icon only appears after the condition held this long (filters flicker).</summary>
		public readonly int DelayTicks;

		public ProblemKind(CityProblem problem, string nameKey, ProblemTier baseTier, ProblemTier escalatedTier = ProblemTier.Minimal,
			int escalateTicks = 0, int delayTicks = 0)
		{
			Problem = problem;
			NameKey = nameKey;
			BaseTier = baseTier;
			EscalatedTier = escalatedTier < baseTier ? baseTier : escalatedTier;
			EscalateTicks = escalateTicks;
			DelayTicks = delayTicks;
		}
	}

	/// <summary>The problem catalogue (one row per CityProblem). Tune tiers and delays here.</summary>
	public static class ProblemCatalog
	{
		public const int Count = 24;

		static readonly ProblemKind[] Kinds =
		[
			default,
			new(CityProblem.NoPower, "problem-nopower", ProblemTier.Warning, ProblemTier.Major, 1200),
			new(CityProblem.NoWater, "problem-nowater", ProblemTier.Warning, ProblemTier.Major, 1200),
			new(CityProblem.NoSewage, "problem-nosewage", ProblemTier.Warning, ProblemTier.Major, 2400),
			new(CityProblem.DirtyWater, "problem-dirtywater", ProblemTier.Warning, ProblemTier.Major, 2400),
			new(CityProblem.NoRoad, "problem-noroad", ProblemTier.Problem),
			new(CityProblem.Abandoned, "problem-abandoned", ProblemTier.Problem),
			new(CityProblem.Unhappy, "problem-unhappy", ProblemTier.Info, ProblemTier.Warning, 2400),
			new(CityProblem.NoWorkers, "problem-noworkers", ProblemTier.Info, ProblemTier.Minimal, 0, 1200),
			new(CityProblem.NoCustomers, "problem-nocustomers", ProblemTier.Info, ProblemTier.Minimal, 0, 1200),
			new(CityProblem.NoGoods, "problem-nogoods", ProblemTier.Problem, ProblemTier.Problem, 0, 600),
			new(CityProblem.Garbage, "problem-garbage", ProblemTier.Warning, ProblemTier.Major, 600),
			new(CityProblem.Fire, "problem-fire", ProblemTier.Major, ProblemTier.Fatal, 1200),
			new(CityProblem.Crime, "problem-crime", ProblemTier.Info, ProblemTier.Warning, 1200),
			new(CityProblem.Sick, "problem-sick", ProblemTier.Problem),
			new(CityProblem.Ambulance, "problem-ambulance", ProblemTier.Major),
			new(CityProblem.HighRent, "problem-highrent", ProblemTier.Info),
			new(CityProblem.Traffic, "problem-traffic", ProblemTier.Warning, ProblemTier.Warning, 0, 400),
			new(CityProblem.AirPollution, "problem-airpollution", ProblemTier.Warning, ProblemTier.Warning, 0, 600),
			new(CityProblem.GroundPollution, "problem-groundpollution", ProblemTier.Warning, ProblemTier.Warning, 0, 600),
			new(CityProblem.Noise, "problem-noise", ProblemTier.Warning, ProblemTier.Warning, 0, 600),
			new(CityProblem.LeveledUp, "problem-leveledup", ProblemTier.Good),
			new(CityProblem.Collapsed, "problem-collapsed", ProblemTier.Error),
			new(CityProblem.NoService, "problem-noservice", ProblemTier.Problem),
			new(CityProblem.Flooded, "problem-flooded", ProblemTier.Warning, ProblemTier.Major, 1200),
		];

		public static ProblemKind Get(CityProblem problem) { return Kinds[(int)problem]; }

		public static string NameKey(CityProblem problem) { return Kinds[(int)problem].NameKey; }

		/// <summary>Tier of a problem that has been active for `age` ticks.</summary>
		public static ProblemTier TierFor(CityProblem problem, int age)
		{
			var k = Kinds[(int)problem];
			return k.EscalateTicks > 0 && age >= k.EscalateTicks ? k.EscalatedTier : k.BaseTier;
		}

		/// <summary>Sort key: higher tier first, then lower enum value (Good is the least urgent).</summary>
		public static int Priority(ProblemTier tier) { return tier == ProblemTier.Good ? -1 : (int)tier; }
	}

	/// <summary>One line of the city-wide problem summary (for an alert strip).</summary>
	public struct ProblemSummary
	{
		public CityProblem Problem;
		public ProblemTier Tier;

		/// <summary>Buildings affected.</summary>
		public int Count;

		/// <summary>One affected building's cell, for click-to-locate.</summary>
		public CPos Cell;
		public uint ActorId;
	}

	/// <summary>
	/// World service for raising problem flags on buildings. Every work package uses it instead of drawing its own icons:
	///   var problems = world.WorldActor.TraitsImplementing&lt;ICityProblems&gt;().FirstOrDefault();   // may be null
	///   problems?.Raise(buildingActor, CityProblem.Garbage);   // call every pulse while the condition holds
	/// A raised flag expires after `durationTicks` unless raised again (default: two pulses), so callers never need to clear it.
	/// Synced callers only.
	/// </summary>
	public interface ICityProblems
	{
		/// <summary>Flags a problem on a building. `durationTicks` &lt;= 0 uses the default (51 ticks).</summary>
		void Raise(Actor building, CityProblem problem, int durationTicks = 0);

		/// <summary>Removes an explicitly raised flag immediately.</summary>
		void Clear(Actor building, CityProblem problem);

		/// <summary>Whether the problem is currently shown on the building.</summary>
		bool Has(Actor building, CityProblem problem);

		/// <summary>Bit mask (1 &lt;&lt; (int)problem) of the active problems of a building.</summary>
		uint Mask(Actor building);

		/// <summary>Buildings with this problem.</summary>
		int Count(CityProblem problem);

		/// <summary>Worst first, one entry per problem kind with at least one affected building.</summary>
		IReadOnlyList<ProblemSummary> Summary { get; }

		/// <summary>Bumped when the summary changes.</summary>
		int Version { get; }
	}

	[Desc("Per-building problem flags: built-in checks (utilities, road access, pollution...) plus flags raised by other systems through ICityProblems.")]
	public class CityProblemsInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new CityProblems(); }
	}

	public class CityProblems
	{
		const int Bits = ProblemCatalog.Count + 1;
		readonly int[] raisedUntil = new int[Bits];
		readonly int[] since = new int[Bits];
		uint raisedBits;

		/// <summary>Active problems (shown), refreshed each pulse by the board. Bit i = (CityProblem)i.</summary>
		public uint Active { get; private set; }

		internal void Raise(CityProblem p, int until)
		{
			var i = (int)p;
			if (until > raisedUntil[i])
				raisedUntil[i] = until;

			raisedBits |= 1u << i;
		}

		internal void Clear(CityProblem p)
		{
			var i = (int)p;
			raisedUntil[i] = 0;
			raisedBits &= ~(1u << i);
		}

		/// <summary>Recomputes the active mask from the built-in conditions (`derived`) and the raised flags.</summary>
		internal void Update(uint derived, int now)
		{
			var raw = derived;
			var bits = raisedBits;
			while (bits != 0)
			{
				var i = System.Numerics.BitOperations.TrailingZeroCount(bits);
				bits &= bits - 1;
				if (raisedUntil[i] > now)
					raw |= 1u << i;
				else
					raisedBits &= ~(1u << i);
			}

			var active = 0u;
			for (var i = 1; i < Bits; i++)
			{
				if ((raw & (1u << i)) == 0)
				{
					since[i] = 0;
					continue;
				}

				if (since[i] == 0)
					since[i] = now + 1;

				if (now + 1 - since[i] >= ProblemCatalog.Get((CityProblem)i).DelayTicks)
					active |= 1u << i;
			}

			Active = active;
		}

		/// <summary>Ticks the problem has been continuously present.</summary>
		public int Age(CityProblem p, int now) { return since[(int)p] == 0 ? 0 : Math.Max(0, now + 1 - since[(int)p]); }

		public bool Has(CityProblem p) { return (Active & (1u << (int)p)) != 0; }

		public ProblemTier Tier(CityProblem p, int now) { return ProblemCatalog.TierFor(p, Age(p, now)); }
	}
}
