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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("XP, 20 milestones, unlock registry, development tree, map tiles, policies, districts and tourism (design 09).",
		"Implements IProgression. The catalogue is data: ProgressionMilestone / ProgressionNode / ProgressionPolicy trait instances.")]
	public class ProgressionInfo : TraitInfo
	{
		[Desc("World ticks between passive-XP / tourism updates (one aggregate pulse).")]
		public readonly int UpdateTicks = 25;

		[Desc("Passive XP: per resident above the all-time peak, per employed worker above the peak, per road cell above the peak.")]
		public readonly int XpPerResident = 2;
		public readonly int XpPerWorker = 1;
		public readonly int XpPerRoadCell = 1;

		[Desc("Total XP roads can ever give (laying a huge grid is not a strategy).")]
		public readonly int RoadXpCap = 150;

		[Desc("Monthly happiness XP: (average - HappinessXpAbove) * population / HappinessXpDivisor, capped.")]
		public readonly int HappinessXpAbove = 60;
		public readonly int HappinessXpDivisor = 200;
		public readonly int HappinessXpMonthlyCap = 50;

		[Desc("Active XP: each placeable type gives full XP for the first PlacementFullCount placements, then PlacementReducedPercent",
			"until PlacementReducedCount placements, then nothing. Landmarks pay once.")]
		public readonly int PlacementFullCount = 5;
		public readonly int PlacementReducedCount = 15;
		public readonly int PlacementReducedPercent = 25;

		[Desc("Placement XP by CityPlaceable category ('category=xp') for actors without a ProgressionValue trait.")]
		public readonly string[] CategoryXp = [];

		[Desc("Map tiles: grid size per side, side of the initially owned square block (5x5 = about 70x70 cells on a 130 cell map).")]
		public readonly int TileGrid = 9;
		public readonly int StartTileBlock = 5;

		[Desc("Tile price = (TileBasePrice + TilePricePerCell * buildableCells) * (100 + TilePriceStepPercent * purchased) / 100.")]
		public readonly int TileBasePrice = 2000;
		public readonly int TilePricePerCell = 8;
		public readonly int TilePriceStepPercent = 6;

		[Desc("Monthly tile upkeep = price paid * pct / 100 / TileUpkeepDivisor, pct = TileUpkeepMinPct + TileUpkeepRangePct * purchased / (tiles - start).")]
		public readonly int TileUpkeepMinPct = 5;
		public readonly int TileUpkeepRangePct = 20;
		public readonly int TileUpkeepDivisor = 20;

		[Desc("Maximum number of districts (ids 1..MaxDistricts, at most 63).")]
		public readonly int MaxDistricts = 63;

		[Desc("Largest rectangle (cells) one district paint order may cover.")]
		public readonly int MaxPaintCells = 4096;

		[Desc("Tourism: attractiveness = 100 * x / (x + AttractionHalf); visitor groups per month = attractiveness^2 / VisitorDivisor + connection bonus.")]
		public readonly int AttractionHalf = 150;
		public readonly int VisitorDivisor = 400;
		public readonly int ConnectionVisitors = 1;

		[Desc("Percent of the visitor arrivals lost per tourist without a bed (CIT's lodging report), at most 50.")]
		public readonly int UnhousedPenaltyPercent = 5;

		public override object Create(ActorInitializer init) { return new Progression(init.Self, this); }
	}

	/// <summary>Runtime view of a milestone for the UI.</summary>
	public sealed class MilestoneData
	{
		public int Index;
		public string Name;
		public int Xp, Money, DevPoints, Tiles;
		public string[] Unlocks;
	}

	public struct ProgressionNotice
	{
		public int Tick;
		public string Key;
		public string Arg;
	}

	public partial class Progression : IProgression, IProgressionUiSource, IResolveOrder, ITick, IWorldLoaded, INotifyActorDisposing, ISync, ICityAutoTestReporter
	{
		public readonly ProgressionInfo Info;
		readonly Actor self;
		readonly World world;
		readonly List<MilestoneData> milestones = [];
		readonly Dictionary<string, int> placed = [];
		readonly Dictionary<string, int> categoryXp = [];
		readonly List<ProgressionNotice> notices = [];

		CityManager manager;
		ICitizenPopulation citizens;
		ICityStatistics statistics;
		ISignatureUnlocks signatures;
		RoadLayer roadLayer;
		CityClock clock;
		bool resolved;
		bool baselined;
		bool disposed;
		int peakEmployed;
		int peakRoads;
		int roadXp;
		int lastClockDay;

		public Progression(Actor self, ProgressionInfo info)
		{
			this.self = self;
			Info = info;
			world = self.World;

			// Milestone 0 is the implicit start.
			milestones.Add(new MilestoneData { Index = 0, Name = "Village", Unlocks = [] });
			foreach (var m in self.Info.TraitInfos<ProgressionMilestoneInfo>())
				milestones.Add(new MilestoneData
				{
					Index = milestones.Count,
					Name = m.Name,
					Xp = m.Xp,
					Money = m.Money,
					DevPoints = m.DevPoints,
					Tiles = m.Tiles,
					Unlocks = m.Unlocks
				});

			foreach (var entry in info.CategoryXp)
			{
				var kv = entry.Split('=');
				if (kv.Length == 2 && int.TryParse(kv[1].Trim(), out var xp))
					categoryXp[kv[0].Trim()] = xp;
			}

			BuildUnlockRegistry();
			BuildTree();
			BuildPolicies();
			InitStats();
			BuildAchievements();
		}

		// ---- XP and milestones ----
		[VerifySync]
		public int Xp { get; private set; }

		[VerifySync]
		public int MilestoneIndex { get; private set; }

		[VerifySync]
		public int DevPoints { get; private set; }

		/// <summary>Map tile purchase permits in hand.</summary>
		[VerifySync]
		public int Permits { get; private set; }

		[VerifySync]
		public int PeakPopulation { get; private set; }

		public int MilestoneCount => milestones.Count - 1;

		public MilestoneData GetMilestone(int index) { return milestones[Math.Clamp(index, 0, milestones.Count - 1)]; }

		public string MilestoneName => milestones[MilestoneIndex].Name;

		/// <summary>Cumulative XP of the next milestone (the last one's own XP when everything is reached).</summary>
		public int NextMilestoneXp => milestones[Math.Min(MilestoneIndex + 1, milestones.Count - 1)].Xp;

		/// <summary>0..100 progress from the current to the next milestone.</summary>
		public int MilestoneProgress
		{
			get
			{
				if (MilestoneIndex >= milestones.Count - 1)
					return 100;

				var lo = milestones[MilestoneIndex].Xp;
				var hi = milestones[MilestoneIndex + 1].Xp;
				return Math.Clamp((int)((long)(Xp - lo) * 100 / Math.Max(1, hi - lo)), 0, 100);
			}
		}

		/// <summary>Recent progression messages (milestones, tile/node purchases), newest last.</summary>
		public IReadOnlyList<ProgressionNotice> Notices => notices;

		public void AddXp(int amount, string reason)
		{
			AddXpFrom(reason == "passive" ? XpSource.Population : reason != null && placed.ContainsKey(reason) ? XpSource.Placement : XpSource.Other, amount);
		}

		void AddXpFrom(XpSource source, int amount)
		{
			if (amount <= 0)
				return;

			Xp += amount;
			xpBySource[(int)source] += amount;
			CheckMilestones();
		}

		void CheckMilestones()
		{
			while (MilestoneIndex < milestones.Count - 1 && Xp >= milestones[MilestoneIndex + 1].Xp)
			{
				MilestoneIndex++;
				var m = milestones[MilestoneIndex];
				DevPoints += m.DevPoints;
				Permits += m.Tiles;
				if (m.Money > 0)
					manager?.AddFunds(m.Money, "milestone");

				GrantAutoNodes();
				OnMilestoneReached(MilestoneIndex);
				Notify("notification-prg-milestone", m.Name, "money", CityUtils.FormatMoney(m.Money),
					"points", m.DevPoints, "tiles", m.Tiles);
			}
		}

		void Notify(string key, string arg, params object[] fluentArgs)
		{
			notices.Add(new ProgressionNotice { Tick = world.WorldTick, Key = key, Arg = arg });
			if (notices.Count > 20)
				notices.RemoveAt(0);

			if (statistics != null)
				statistics.Chirp("chirp-prg-" + key["notification-prg-".Length..], 0, arg);
			else if (self.Owner == world.LocalPlayer || world.LocalPlayer == null)
			{
				var args = new object[fluentArgs.Length + 2];
				args[0] = "name";
				args[1] = arg ?? "";
				Array.Copy(fluentArgs, 0, args, 2, fluentArgs.Length);
				TextNotificationsManager.AddFeedbackLine(key, args);
			}
		}

		// ---- placement XP ----
		void OnActorAdded(Actor a)
		{
			if (a.Owner != self.Owner || world.WorldTick == 0)
				return;

			var value = a.Info.TraitInfoOrDefault<ProgressionValueInfo>();
			var placeable = a.Info.TraitInfoOrDefault<CityPlaceableInfo>();
			if (value == null && placeable == null)
				return;

			var xp = value?.Xp ?? 0;
			if (xp == 0 && placeable != null)
				categoryXp.TryGetValue(placeable.Category, out xp);

			placed.TryGetValue(a.Info.Name, out var count);
			placed[a.Info.Name] = count + 1;
			if (xp <= 0)
				return;

			if (value != null && value.Landmark)
				xp = count == 0 ? xp : 0;
			else if (count >= Info.PlacementFullCount + Info.PlacementReducedCount)
				xp = 0;
			else if (count >= Info.PlacementFullCount)
				xp = xp * Info.PlacementReducedPercent / 100;

			AddXpFrom(XpSource.Placement, xp);
		}

		// ---- lifecycle ----
		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			world.ActorAdded += OnActorAdded;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			disposed = true;
			world.ActorAdded -= OnActorAdded;
		}

		void Resolve()
		{
			resolved = true;
			manager = self.TraitOrDefault<CityManager>();
			citizens = world.WorldActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault()
				?? self.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
			statistics = world.WorldActor.TraitsImplementing<ICityStatistics>().FirstOrDefault()
				?? self.TraitsImplementing<ICityStatistics>().FirstOrDefault();
			roadLayer = world.WorldActor.TraitOrDefault<RoadLayer>();
			signatures = ZoningLookup.Find<ISignatureUnlocks>(world);
			ResolveStats();
			clock = world.WorldActor.TraitOrDefault<CityClock>();
			lastClockDay = clock?.DayIndex ?? 0;
			pollution = world.WorldActor.TraitsImplementing<IPollutionMap>().FirstOrDefault();
			EnsureDistricts();
			InitTiles();
			GrantAutoNodes();
		}

		int CurrentPopulation => citizens?.Population ?? manager?.Population ?? 0;

		int CurrentEmployed => citizens != null ? citizens.Workers : manager?.Employed ?? 0;

		void ITick.Tick(Actor self)
		{
			if (!resolved)
				Resolve();

			if (world.WorldTick % Math.Max(1, Info.UpdateTicks) != 0)
				return;

			var pop = CurrentPopulation;
			var employed = CurrentEmployed;
			var roads = roadLayer?.RoadCellCount ?? 0;
			if (!baselined)
			{
				// The highway stub laid at load must not count as player effort.
				baselined = true;
				peakRoads = roads;
			}

			var gain = 0;
			if (pop > PeakPopulation)
			{
				AddXpFrom(XpSource.Population, (pop - PeakPopulation) * Info.XpPerResident);
				PeakPopulation = pop;
			}

			if (employed > peakEmployed)
			{
				AddXpFrom(XpSource.Workers, (employed - peakEmployed) * Info.XpPerWorker);
				peakEmployed = employed;
			}

			if (roads > peakRoads)
			{
				var roadGain = Math.Min((roads - peakRoads) * Info.XpPerRoadCell, Math.Max(0, Info.RoadXpCap - roadXp));
				roadXp += roadGain;
				AddXpFrom(XpSource.Roads, roadGain);
				peakRoads = roads;
			}

			if (clock != null && clock.DayIndex != lastClockDay)
			{
				lastClockDay = clock.DayIndex;
				gain += MonthlyHappinessXp(pop);
				OnNewMonth();
			}

			if (gain > 0)
				AddXpFrom(XpSource.Happiness, gain);

			UpdateTourism();
			if (world.WorldTick % (Math.Max(1, Info.UpdateTicks) * 4) == 0)
			{
				UpdateDistrictStats();
				UpdateAchievements(false);
			}
		}

		int MonthlyHappinessXp(int pop)
		{
			var avg = citizens?.AverageHappiness ?? manager?.AverageHappiness ?? 50;
			if (avg <= Info.HappinessXpAbove || pop <= 0)
				return 0;

			return Math.Min(Info.HappinessXpMonthlyCap, (avg - Info.HappinessXpAbove) * pop / Math.Max(1, Info.HappinessXpDivisor));
		}

		void OnNewMonth()
		{
			ChargeUpkeep();
			CleanupDistricts();
			UpdateDistrictStats();
			UpdateAchievements(true);
			OnMonthStats();
		}

		/// <summary>Spends monthly money (tile and policy upkeep). Skipped when the city cannot pay.</summary>
		bool Spend(int amount, string category)
		{
			return manager == null || manager.TrySpend(amount, category);
		}

		// ---- orders ----
		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			switch (order.OrderString)
			{
				case ProgressionOrders.UnlockNode:
					BuyNode(order.TargetString);
					break;
				case ProgressionOrders.BuyTile:
					BuyTile(order.ExtraLocation.X, order.ExtraLocation.Y);
					break;
				case ProgressionOrders.SetPolicy:
					if (order.ExtraLocation.X >= 0 && order.ExtraLocation.X < policies.Count)
						SetPolicy(policies[order.ExtraLocation.X].Id, order.ExtraLocation.Y, (int)Math.Min(order.ExtraData, 1000u));

					break;
				case ProgressionOrders.DistrictPaint:
				{
					var end = order.Target.Type == TargetType.Invalid ? order.ExtraLocation : world.Map.CellContaining(order.Target.CenterPosition);
					PaintDistrict(order.ExtraLocation, end, order.ExtraData);
					break;
				}

				case ProgressionOrders.DistrictName:
					RenameDistrict((int)Math.Min(order.ExtraData, 255u), order.TargetString);
					break;
				case ProgressionOrders.DistrictDelete:
					DeleteDistrict((int)Math.Min(order.ExtraData, 255u));
					break;
			}
		}

		// ---- determinism / autotest ----
		public int StateHash
		{
			get
			{
				unchecked
				{
					var h = Xp;
					h = h * 31 + MilestoneIndex;
					h = h * 31 + DevPoints;
					h = h * 31 + Permits;
					h = h * 31 + PeakPopulation;
					h = h * 31 + peakEmployed;
					h = h * 31 + peakRoads;
					h = h * 31 + roadXp;
					h = h * 31 + TileHash();
					h = h * 31 + NodeHash();
					h = h * 31 + PolicyHash();
					h = h * 31 + DistrictHash();
					h = h * 31 + Attractiveness;
					h = h * 31 + AchievementHash();
					h = h * 31 + TourismHash();
					foreach (var kv in placed)
						h += StableHash(kv.Key) * 7919 + kv.Value * 104729; // commutative: dictionary order must not matter

					return h;
				}
			}
		}

		/// <summary>Process independent string hash (string.GetHashCode is randomised per run).</summary>
		internal static int StableHash(string s)
		{
			unchecked
			{
				var h = 17;
				if (s != null)
					for (var i = 0; i < s.Length; i++)
						h = h * 31 + s[i];

				return h;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var names = placed.Keys.ToList();
			names.Sort(StringComparer.Ordinal);
			var placedText = string.Join(",", names.Select(n => n + "x" + placed[n]));
			return $"progression placed[{placedText}] xp={Xp} ms={MilestoneIndex}/{MilestoneCount} {MilestoneName} " +
				$"next={NextMilestoneXp} dp={DevPoints} permits={Permits} " +
				$"tiles={OwnedTileCount}/{TileGrid * TileGrid} ownedRect={OwnedBounds()} nodes={OwnedNodeCount}/{NodeCount} " +
				$"policies={ActivePolicyCount} districts={DistrictCount} " +
				$"ach={UnlockedAchievementCount}/{AchievementCount} xpSrc=[{string.Join("/", xpBySource)}] " +
				$"attr={Attractiveness} visitors={VisitorGroupsPerMonth} hash={StateHash}";
		}
	}
}
