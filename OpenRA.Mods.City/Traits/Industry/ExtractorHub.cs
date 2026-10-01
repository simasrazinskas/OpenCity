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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[Desc("Hub building of a specialized industry area (farm, forestry, quarry, mine, oil, design/06 3.2/3.3). The player paints",
		"an area around it with the area tool; capacity, jobs, depletion and pollution derive from the area cells.")]
	public class ExtractorHubInfo : TraitInfo, Requires<CityBuildingInfo>
	{
		[Desc("Which natural resource layer the hub draws on. Fertile = farm, Forest = forestry, Stone = quarry, Ore = mine, Oil = oil well.")]
		public readonly NaturalResourceKind Kind = NaturalResourceKind.Fertile;

		[Desc("Products the hub can produce (resource names). The first one is the default; farm hubs have a crop selector.")]
		public readonly string[] Products = ["Grain"];

		[Desc("Area cells must lie within this Chebyshev distance of the hub footprint.")]
		public readonly int Radius = 8;

		public readonly int MaxAreaCells = 144;

		[Desc("Upper bound of jobs, whatever the area size.")]
		public readonly int MaxJobs = 40;

		[Desc("Output per area cell at full richness, milli-units per clock day, per product.")]
		public readonly Dictionary<string, int> CapPerCellMilli = new()
		{
			{ "Grain", 3000 }, { "Vegetables", 2000 }, { "Livestock", 1500 }, { "Cotton", 2000 },
			{ "Wood", 4000 }, { "Stone", 3000 }, { "Ore", 1500 }, { "Oil", 1000 }, { "Fish", 2000 },
		};

		[Desc("Units produced per fully staffed worker per clock day, milli-units (design/05 q values).")]
		public readonly Dictionary<string, int> UnitsPerWorkerMilli = new()
		{
			{ "Grain", 7000 }, { "Vegetables", 4700 }, { "Livestock", 3500 }, { "Cotton", 4000 },
			{ "Wood", 5600 }, { "Stone", 7800 }, { "Ore", 3100 }, { "Oil", 2300 }, { "Fish", 4000 },
		};

		[Desc("Output percent while clear-cutting (forestry).")]
		public readonly int ClearCutBonusPercent = 125;

		[Desc("Cost per painted area cell.")]
		public readonly int CostPerCell = 2;

		[Desc("Extra cost per tree cleared when painting a non-forestry area.")]
		public readonly int ClearTreeCost = 5;

		[Desc("Wood units that fell one tree.")]
		public readonly int WoodPerTree = 20;

		[Desc("Ticks between replanted trees (forestry, unless clear-cut). 200 = 12 trees per clock day.")]
		public readonly int ReplantTicks = 200;

		[Desc("Percent of output removed from the deposit per unit produced (100 normal, 0 sandbox, 300 hard).")]
		public readonly int DepletionMultiplier = 100;

		[Desc("Output taper starts when a cell has this percent of its initial stock left (ore and oil).")]
		public readonly int TaperPercent = 25;

		[Desc("Lowest output factor (percent) of the taper.")]
		public readonly int TaperFloorPercent = 20;

		[Desc("Sequence (image 'extractor-area') of the field tiles. Farm hubs append '-' and the lowercase product. Empty = no field art.")]
		public readonly string AreaSequence = null;

		[Desc("Emission at 100% output, 0..100 (ENV samples IPollutionEmitter).")]
		public readonly int Ground = 0;
		public readonly int Air = 0;
		public readonly int Noise = 0;
		public readonly int Water = 0;

		[Desc("Emission spread radius in cells (0 = ENV default).")]
		public readonly int EmissionRadius = 0;

		[Desc("Cosmetic prop actors (oil derricks) placed on the richest area cells, up to this many (0 = none).")]
		public readonly int MaxProps = 0;

		public readonly string PropActor = "oil-derrick";

		[Desc("Percent of the emission while no economy reports production yet (a hub with an area and no ECO still works a bit).")]
		public readonly int FallbackOutputPercent = 50;

		public override object Create(ActorInitializer init) { return new ExtractorHub(init.Self, this); }
	}

	public class ExtractorHub : INotifyAddedToWorld, INotifyRemovedFromWorld, ITick, ISync, IPollutionEmitter
	{
		public readonly ExtractorHubInfo Info;
		readonly Actor self;

		/// <summary>Area cells in row-major order. Mutated only by ExtractorAreaLayer.</summary>
		internal readonly List<CPos> CellList = [];

		NaturalResourceLayer resources;
		ExtractorAreaLayer areas;
		CityClock clock;

		int fellMilli;
		int plantTimer;
		int producedTodayMilli;
		int lastDay = -1;
		int cursor;
		long depleteCarryMilli;
		bool everProduced;

		public ExtractorHub(Actor self, ExtractorHubInfo info)
		{
			this.self = self;
			Actor = self;
			Info = info;
			Product = info.Products.Length > 0 ? info.Products[0] : "Grain";
			plantTimer = info.ReplantTicks;
		}

		public Actor Actor { get; }

		public IReadOnlyList<CPos> Cells => CellList;

		public string Product { get; internal set; }

		[VerifySync]
		public int ProductHash => IndustryHash.HashString(Product);

		[VerifySync]
		public bool ClearCut { get; internal set; }

		/// <summary>Cached maximum output, milli-units per clock day (refreshed on area/product change and every 625 ticks).</summary>
		[VerifySync]
		public int CapMilliPerDay { get; private set; }

		/// <summary>Jobs the area supports.</summary>
		[VerifySync]
		public int JobsMax { get; private set; }

		/// <summary>0..100 share of capacity produced during the last full clock day.</summary>
		[VerifySync]
		public int OutputPercent { get; private set; }

		/// <summary>Units taken out of deposits / trees felled so far.</summary>
		[VerifySync]
		public int TotalProducedMilli { get; private set; }

		public int TreesFelled { get; private set; }

		bool IsFarm => Info.Kind == NaturalResourceKind.Fertile;

		NaturalResourceLayer Resources => resources ??= self.World.WorldActor.TraitOrDefault<NaturalResourceLayer>();

		ExtractorAreaLayer Areas => areas ??= self.World.WorldActor.TraitOrDefault<ExtractorAreaLayer>();

		CityClock Clock => clock ??= self.World.WorldActor.TraitOrDefault<CityClock>();

		public int ProductId
		{
			get
			{
				var economy = self.World.WorldActor.TraitsImplementing<ICityEconomy>().FirstOrDefault()
					?? self.Owner.PlayerActor.TraitsImplementing<ICityEconomy>().FirstOrDefault();
				if (economy != null)
				{
					for (var i = 1; i <= economy.ResourceCount; i++)
						if (string.Equals(economy.ResourceName(i), Product, StringComparison.OrdinalIgnoreCase))
							return i;
				}

				return IndustryResources.DefaultId(Product);
			}
		}

		public bool CanProduce(string product)
		{
			return Info.Products.Any(p => string.Equals(p, product, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Output factor 0..1000 of one cell for the current product.</summary>
		public int CellFactor(CPos cell)
		{
			var nr = Resources;
			if (nr == null)
				return 1000;

			switch (Info.Kind)
			{
				case NaturalResourceKind.Fertile:
					// Livestock needs no fertile soil, only open land.
					return string.Equals(Product, "Livestock", StringComparison.OrdinalIgnoreCase)
						? nr.GetAmount(NaturalResourceKind.Stone, cell) > 0 ? 1000 : 0
						: nr.GetAmount(NaturalResourceKind.Fertile, cell);
				case NaturalResourceKind.Forest:
					return nr.GetAmount(NaturalResourceKind.Forest, cell) * 10;
				case NaturalResourceKind.Stone:
					return nr.GetAmount(NaturalResourceKind.Stone, cell) * 10;
				case NaturalResourceKind.Fish:
					return nr.GetAmount(NaturalResourceKind.Fish, cell);
				case NaturalResourceKind.Ore:
				case NaturalResourceKind.Oil:
					var initial = nr.GetInitial(Info.Kind, cell);
					var now = nr.GetAmount(Info.Kind, cell);
					if (initial <= 0 || now <= 0)
						return 0;

					// Full output until TaperPercent of the stock is left, then a linear taper down to the floor.
					var rf = Math.Clamp(100L * now * 100 / (initial * Math.Max(1, Info.TaperPercent)), Info.TaperFloorPercent, 100);
					return (int)rf * 10;
				default:
					return 0;
			}
		}

		/// <summary>Whether the cell would give any output (preview: flag resource-less cells yellow).</summary>
		public bool HasResource(CPos cell) { return CellFactor(cell) > 0; }

		public int RemainingStock
		{
			get
			{
				var nr = Resources;
				if (nr == null || (Info.Kind != NaturalResourceKind.Ore && Info.Kind != NaturalResourceKind.Oil))
					return -1;

				var sum = 0;
				foreach (var c in CellList)
					sum += nr.GetAmount(Info.Kind, c);

				return sum;
			}
		}

		/// <summary>Clock days (= months) left at the current capacity, -1 for renewable products.</summary>
		public int MonthsLeft
		{
			get
			{
				var stock = RemainingStock;
				if (stock < 0)
					return -1;

				var perDay = Math.Max(1, CapMilliPerDay * Math.Max(1, OutputPercent) / 100 / 1000);
				return stock / perDay;
			}
		}

		public void RefreshCapacity()
		{
			Info.CapPerCellMilli.TryGetValue(Product, out var capPerCell);
			long cap = 0;
			foreach (var c in CellList)
				cap += (long)capPerCell * CellFactor(c) / 1000;

			// Clear-cutting (no replanting) yields more now at the price of the forest.
			if (ClearCut && Info.Kind == NaturalResourceKind.Forest)
				cap = cap * Info.ClearCutBonusPercent / 100;

			CapMilliPerDay = (int)Math.Min(int.MaxValue, cap);
			Info.UnitsPerWorkerMilli.TryGetValue(Product, out var q);
			q = Math.Max(1, q);
			JobsMax = CapMilliPerDay <= 0 ? 0 : (int)Math.Min(Info.MaxJobs, (CapMilliPerDay + q - 1L) / q);
			CheckDeposit();
		}

		Logistics Owner_Logistics => self.Owner?.PlayerActor?.TraitOrDefault<Logistics>();

		// Depletion warnings (chirps): once when the area is down to the taper mark, once when it runs dry.
		int peakStock;
		int peakCells = -1;
		bool lowNotified;
		bool depletedNotified;

		void CheckDeposit()
		{
			var stock = RemainingStock;
			if (stock < 0)
				return;

			if (peakCells != CellList.Count || stock > peakStock)
			{
				if (peakCells != CellList.Count)
				{
					lowNotified = false;
					depletedNotified = false;
				}

				peakCells = CellList.Count;
				peakStock = stock;
			}

			if (peakStock <= 0)
				return;

			var stats = self.World.WorldActor.TraitsImplementing<ICityStatistics>().FirstOrDefault();
			if (stock <= 0 && !depletedNotified)
			{
				depletedNotified = true;
				stats?.Chirp("chirp-industry-deposit-depleted", 0, Product);
			}
			else if (stock > 0 && (long)stock * 100 <= (long)peakStock * Info.TaperPercent && !lowNotified)
			{
				lowNotified = true;
				stats?.Chirp("chirp-industry-deposit-low", 0, Product);
			}
		}

		/// <summary>Called by the economy after production (milli-units). Depletes deposits and fells trees.</summary>
		public void OnProducedMilli(int milli)
		{
			if (milli <= 0 || CellList.Count == 0)
				return;

			everProduced = true;
			producedTodayMilli += milli;
			TotalProducedMilli += milli;
			var nr = Resources;
			Owner_Logistics?.ReportProducedMilli(ProductId, milli);
			switch (Info.Kind)
			{
				case NaturalResourceKind.Ore:
				case NaturalResourceKind.Oil:
					Deplete(nr, (int)((long)milli * Info.DepletionMultiplier / 100));
					RefreshCapacity();
					break;
				case NaturalResourceKind.Fish:
					Deplete(nr, milli);
					RefreshCapacity();
					break;
				case NaturalResourceKind.Forest:
					fellMilli += milli;
					var perTree = Math.Max(1, Info.WoodPerTree) * 1000;
					while (fellMilli >= perTree)
					{
						fellMilli -= perTree;
						FellOneTree();
					}

					break;
			}
		}

		// Spreads whole units over the area cells proportionally to the remaining stock (deterministic: row-major order, rotating remainder).
		void Deplete(NaturalResourceLayer nr, int milli)
		{
			if (nr == null)
				return;

			depleteCarryMilli += milli;
			var units = (int)(depleteCarryMilli / 1000);
			if (units <= 0)
				return;

			depleteCarryMilli -= units * 1000L;
			long total = 0;
			foreach (var c in CellList)
				total += nr.GetAmount(Info.Kind, c);

			if (total <= 0)
			{
				depleteCarryMilli = 0;
				return;
			}

			var left = units;
			if (units < total)
			{
				foreach (var c in CellList)
				{
					var share = (int)(units * (long)nr.GetAmount(Info.Kind, c) / total);
					left -= nr.Remove(Info.Kind, c, share);
				}
			}

			// Remainder: one unit at a time, rotating through the cells.
			var guard = CellList.Count * 4 + 8;
			while (left > 0 && guard-- > 0)
			{
				cursor = (cursor + 1) % CellList.Count;
				left -= nr.Remove(Info.Kind, CellList[cursor], 1);
			}
		}

		static bool IsTreeActor(Actor a) { return a.Info.Name.StartsWith("tree-", StringComparison.Ordinal); }

		void FellOneTree()
		{
			// The tree with the lowest cell hash goes first (deterministic, independent of enumeration order).
			Actor best = null;
			var bestHash = int.MaxValue;
			foreach (var c in CellList)
				foreach (var a in self.World.ActorMap.GetActorsAt(c))
				{
					if (!IsTreeActor(a) || a.Disposed)
						continue;

					var h = IndustryHash.Mix(c, TreesFelled);
					if (h < bestHash)
					{
						bestHash = h;
						best = a;
					}
				}

			if (best == null)
				return;

			TreesFelled++;
			self.World.AddFrameEndTask(_ =>
			{
				if (!best.Disposed)
					best.Dispose();
			});
		}

		void PlantOneTree()
		{
			var w = self.World;
			if (CellList.Count == 0)
				return;

			var roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			var start = IndustryHash.Mix(self.ActorID == 0 ? 1 : (int)self.ActorID, w.WorldTick, 7) % CellList.Count;
			for (var i = 0; i < CellList.Count; i++)
			{
				var c = CellList[(start + i) % CellList.Count];
				var terrain = w.Map.GetTerrainInfo(c).Type;
				if ((terrain != "Clear" && terrain != "Rough") || (roads != null && roads.IsRoad(c)) || w.ActorMap.AnyActorsAt(c))
					continue;

				var variant = 1 + IndustryHash.Mix(c, 31) % 4;
				var owner = w.WorldActor.Owner;
				w.AddFrameEndTask(ww =>
				{
					if (!ww.ActorMap.AnyActorsAt(c))
						ww.CreateActor("tree-" + variant, [new LocationInit(c), new OwnerInit(owner)]);
				});
				return;
			}
		}

		readonly List<Actor> props = [];

		/// <summary>Keeps up to MaxProps cosmetic props (derricks) on the richest free area cells. Call after the area changed.</summary>
		internal void SyncProps()
		{
			if (Info.MaxProps <= 0 || !self.World.Map.Rules.Actors.ContainsKey(Info.PropActor))
				return;

			var nr = Resources;
			var want = new List<CPos>();
			if (nr != null && self.IsInWorld)
			{
				var candidates = CellList.Where(c =>
					(nr.GetInitial(Info.Kind, c) > 0 && !self.World.ActorMap.AnyActorsAt(c)) || props.Any(p => !p.Disposed && p.Location == c)).ToList();
				candidates.Sort((a, b) =>
				{
					var d = nr.GetInitial(Info.Kind, b).CompareTo(nr.GetInitial(Info.Kind, a));
					return d != 0 ? d : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
				});

				// Spread them: skip candidates adjacent to an already chosen cell.
				foreach (var c in candidates)
				{
					if (want.Count >= Info.MaxProps)
						break;

					if (want.Any(q => Math.Abs(q.X - c.X) <= 1 && Math.Abs(q.Y - c.Y) <= 1))
						continue;

					want.Add(c);
				}
			}

			var keep = new List<Actor>();
			var kill = new List<Actor>();
			foreach (var p in props)
			{
				if (p.Disposed)
					continue;

				if (want.Contains(p.Location))
				{
					keep.Add(p);
					want.Remove(p.Location);
				}
				else
					kill.Add(p);
			}

			props.Clear();
			props.AddRange(keep);
			var w = self.World;
			var owner = self.Owner;
			var add = want.ToArray();
			w.AddFrameEndTask(ww =>
			{
				foreach (var k in kill)
					if (!k.Disposed)
						k.Dispose();

				foreach (var c in add)
				{
					if (ww.ActorMap.AnyActorsAt(c))
						continue;

					props.Add(ww.CreateActor(Info.PropActor, [new LocationInit(c), new OwnerInit(owner)]));
				}
			});
		}

		void ITick.Tick(Actor self)
		{
			var w = self.World;
			if (CellList.Count == 0)
				return;

			// Stagger the capacity refresh across hubs by actor id.
			if ((w.WorldTick + (int)self.ActorID * 7) % 625 == 0)
				RefreshCapacity();

			// Output percent over the last full clock day (drives the emission).
			var day = Clock != null ? Clock.DayIndex : w.WorldTick / 2400;
			if (lastDay < 0)
				lastDay = day;
			else if (day != lastDay)
			{
				lastDay = day;
				OutputPercent = CapMilliPerDay > 0 ? (int)Math.Min(100L, producedTodayMilli * 100L / CapMilliPerDay) : 0;
				producedTodayMilli = 0;
			}

			if (Info.Kind == NaturalResourceKind.Forest && !ClearCut && --plantTimer <= 0)
			{
				plantTimer = Info.ReplantTicks;
				PlantOneTree();
			}
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			Areas?.RegisterHub(this);
			RefreshCapacity();
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			Areas?.UnregisterHub(this);
			var leftover = props.ToArray();
			props.Clear();
			if (leftover.Length > 0)
			{
				self.World.AddFrameEndTask(_ =>
				{
					foreach (var p in leftover)
						if (!p.Disposed)
							p.Dispose();
				});
			}
		}

		PollutionEmission IPollutionEmitter.Emission
		{
			get
			{
				if (CellList.Count == 0)
					return default;

				// Scale: Output. An idle hub stops polluting; without an economy it runs at the fallback percentage.
				var pct = everProduced || OutputPercent > 0 ? OutputPercent : Info.FallbackOutputPercent;
				return new PollutionEmission
				{
					Ground = Info.Ground * pct / 100,
					Air = Info.Air * pct / 100,
					Noise = Info.Noise * pct / 100,
					Water = Info.Water * pct / 100,
					Radius = Info.EmissionRadius,
				};
			}
		}

		/// <summary>True if the hub shows a crop selector (several products).</summary>
		public bool HasProductChoice => Info.Products.Length > 1;

		public bool Farm => IsFarm;
	}
}
