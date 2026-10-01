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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Shared blackboard of city buildings as lots with slots (homes, jobs by education, seats). Owned by the zoning WP.",
		"Growables get capacity from their lot (cells x density x level), other buildings from CityBuilding numbers.",
		"Property ids are stable: levels are state on the actor, so the id survives level changes.")]
	public class PropertyRegistryInfo : TraitInfo
	{
		[Desc("Average residents per household, used to derive household slots of non-growable buildings from CityBuilding.MaxResidents.")]
		public readonly int ResidentsPerHousehold = 3;

		[Desc("Default job mix by education (percent, edu 0..4) per property kind, for non-growable buildings.")]
		public readonly int[] CommercialJobMix = [40, 40, 20, 0, 0];
		public readonly int[] IndustrialJobMix = [45, 40, 15, 0, 0];
		public readonly int[] OfficeJobMix = [0, 20, 50, 30, 0];
		public readonly int[] ServiceJobMix = [10, 30, 40, 20, 0];

		[Desc("Rent scale in percent of the design formula",
			"rent = ((landValue + zoneFactor * level) * cells * space) / 10. Tune so median rent is 25-30% of median household income.")]
		public readonly int RentScalePercent = 250;

		[Desc("Rent payments (ReportRentPaid) older than this many months no longer count as 'tenants are paying'; the fallback loop takes over.")]
		public readonly int PaymentsWindowMonths = 2;

		[Desc("Ticks between pulses that refresh flags, land value and rent (a quarter of the records each pulse).")]
		public readonly int Pulse = 25;

		public override object Create(ActorInitializer init) { return new PropertyRegistry(init.Self, this); }
	}

	public partial class PropertyRegistry : IPropertyRegistry, ITick, IWorldLoaded, ICityAutoTestReporter, INotifyActorDisposing
	{
		sealed class Record
		{
			public Property P;
			public CityBuilding City;
			public GrowableBuilding Growable;
			public Action<GrowableBuilding> OnChanged;
		}

		public readonly PropertyRegistryInfo Info;
		readonly World world;
		readonly List<Property> all = [];
		readonly List<Record> records = [];
		readonly Dictionary<Actor, Record> byActor = [];
		readonly Dictionary<int, Record> byId = [];
		CellLayer<int> cellToId;
		IRoadNetwork roads;
		ZoneLayer zoneLayer;
		ILandValueSource landValue;
		IProgression progression;
		CityClock clock;
		int nextId = 1;
		int roadVersion = -1;
		int lastPaymentTick = -1;
		bool disposed;

		public PropertyRegistry(Actor self, PropertyRegistryInfo info)
		{
			Info = info;
			world = self.World;
			world.ActorAdded += OnActorAdded;
			world.ActorRemoved += OnActorRemoved;
		}

		public int Version { get; private set; }

		public IReadOnlyList<Property> All => all;

		public event Action<Property> Added;
		public event Action<Property> Removed;
		public event Action<Property> Changed;

		/// <summary>Game ticks in one rent month (the CityClock day), 750 without a clock.</summary>
		internal int TicksPerMonth => clock != null ? clock.TicksPerMonth : 750;

		/// <summary>Progression milestone index for the level caps, or -1 when there is no IProgression (no cap).</summary>
		internal int MilestoneIndex => progression?.MilestoneIndex ?? -1;

		/// <summary>True while tenants (CIT/ECO) report rent: the condition loop then uses real payments.</summary>
		internal bool PaymentsActive => lastPaymentTick >= 0 && world.WorldTick - lastPaymentTick <= Info.PaymentsWindowMonths * TicksPerMonth;

		public Property Get(int id) { return byId.TryGetValue(id, out var r) ? r.P : null; }

		public Property GetAt(CPos cell)
		{
			if (cellToId == null || !cellToId.Contains(cell))
				return null;

			var id = cellToId[cell];
			return id > 0 ? Get(id) : null;
		}

		public Property GetByActor(Actor actor) { return actor != null && byActor.TryGetValue(actor, out var r) ? r.P : null; }

		/// <summary>The lot building of a property (condition, level progress), or null for services.</summary>
		public GrowableBuilding GetGrowable(int id) { return byId.TryGetValue(id, out var r) ? r.Growable : null; }

		public void ReportRentPaid(int propertyId, int dollars)
		{
			if (dollars <= 0 || !byId.TryGetValue(propertyId, out var r))
				return;

			lastPaymentTick = world.WorldTick;
			r.Growable?.AddRentPaid(dollars);
		}

		/// <summary>
		/// CIT reports how many households of the property cannot afford their rent. Above 30% the building shows the high rent
		/// flag and its condition stops rising (design 3.7). Call monthly; 0 clears the flag.
		/// </summary>
		public void ReportHighRent(int propertyId, int unaffordableHouseholds)
		{
			if (!byId.TryGetValue(propertyId, out var r) || r.Growable == null)
				return;

			var total = Math.Max(1, r.P.Households);
			r.Growable.HighRent = unaffordableHouseholds > 0 && unaffordableHouseholds * 100 > total * 30;
		}

		void IWorldLoaded.WorldLoaded(World w, OpenRA.Graphics.WorldRenderer wr)
		{
			cellToId ??= new CellLayer<int>(w.Map);
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			landValue = w.WorldActor.TraitsImplementing<ILandValueSource>().FirstOrDefault();
			clock = w.WorldActor.TraitOrDefault<CityClock>();
			progression = ZoningLookup.Find<IProgression>(w);
			foreach (var pl in w.Players)
			{
				// Prefer the city player's own progression over the first one found.
				if (pl.Playable && pl.PlayerActor.TraitOrDefault<CityManager>() != null)
				{
					progression = pl.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault() ?? progression;
					break;
				}
			}

			foreach (var p in all)
				Stamp(p, p.Id);
		}

		void OnActorAdded(Actor a)
		{
			var cb = a.TraitOrDefault<CityBuilding>();
			if (cb == null || byActor.ContainsKey(a))
				return;

			cellToId ??= new CellLayer<int>(world.Map);
			var growable = cb.Growable ?? a.TraitOrDefault<GrowableBuilding>();
			var dims = a.Info.TraitInfoOrDefault<BuildingInfo>()?.Dimensions ?? new CVec(1, 1);
			var p = new Property
			{
				Id = nextId++,
				Actor = a,
				Origin = a.Location,
				Width = dims.X,
				Depth = dims.Y,
				Kind = KindOf(a, cb),
				Zone = cb.Info.Zone,
				Level = Math.Max(1, cb.Info.Level),
			};

			var r = new Record { P = p, City = cb, Growable = growable };
			SetDefaultSlots(r, true);
			all.Add(p);
			records.Add(r);
			byId[p.Id] = r;
			byActor[a] = r;
			Stamp(p, p.Id);
			RefreshAccess(r);
			p.Operational = cb.IsOperational;
			p.HasRoadAccess = RoadAccess(r);
			RefreshMoney(r);
			if (growable != null)
			{
				r.OnChanged = g => OnGrowableChanged(r);
				growable.CapacityChanged += r.OnChanged;
			}

			Version++;
			Added?.Invoke(p);
		}

		// Lots are connected when their access road (middle of the frontage) reaches the outside; other buildings use the
		// legacy flag of CityManager.
		bool RoadAccess(Record r)
		{
			if (r.Growable == null || roads == null)
				return r.City.HasRoadAccess;

			return r.P.AccessRoad != CPos.Zero && roads.IsConnectedToOutside(r.P.AccessRoad);
		}

		void OnGrowableChanged(Record r)
		{
			r.P.Level = r.Growable.Level;
			r.P.Operational = r.City.IsOperational;
			SetDefaultSlots(r, false);
			RefreshMoney(r);
			Version++;
			Changed?.Invoke(r.P);
		}

		void OnActorRemoved(Actor a)
		{
			if (!byActor.TryGetValue(a, out var r))
				return;

			byActor.Remove(a);
			byId.Remove(r.P.Id);
			if (r.Growable != null && r.OnChanged != null)
				r.Growable.CapacityChanged -= r.OnChanged;

			// Ids grow monotonically, so both lists stay sorted by id.
			var index = IndexOf(r.P.Id);
			if (index >= 0)
			{
				all.RemoveAt(index);
				records.RemoveAt(index);
			}

			Stamp(r.P, 0);
			Version++;
			Removed?.Invoke(r.P);
		}

		int IndexOf(int id)
		{
			int lo = 0, hi = records.Count - 1;
			while (lo <= hi)
			{
				var mid = (lo + hi) >> 1;
				var v = records[mid].P.Id;
				if (v == id)
					return mid;

				if (v < id)
					lo = mid + 1;
				else
					hi = mid - 1;
			}

			return -1;
		}

		void Stamp(Property p, int id)
		{
			if (cellToId == null)
				return;

			for (var y = 0; y < p.Depth; y++)
				for (var x = 0; x < p.Width; x++)
				{
					var c = p.Origin + new CVec(x, y);
					if (cellToId.Contains(c) && (id != 0 || cellToId[c] == p.Id))
						cellToId[c] = id;
				}
		}

		static PropertyKind KindOf(Actor a, CityBuilding cb)
		{
			switch (cb.Info.Zone.Category())
			{
				case ZoneCategory.Residential: return PropertyKind.Residential;
				case ZoneCategory.Commercial: return PropertyKind.Commercial;
				case ZoneCategory.Industrial: return cb.Info.Zone == ZoneType.Warehouse ? PropertyKind.Warehouse : PropertyKind.Industrial;
				case ZoneCategory.Office: return PropertyKind.Office;
			}

			if (a.Info.HasTraitInfo<ExtractorHubInfo>())
				return PropertyKind.Extractor;

			return a.Info.HasTraitInfo<ServiceBuildingInfo>() || a.Info.HasTraitInfo<UtilityProducerInfo>() ? PropertyKind.Service : PropertyKind.None;
		}

		/// <summary>Capacity defaults. Job slots of a property with a company or a service are left alone (ECO / SVC own them).</summary>
		void SetDefaultSlots(Record r, bool initial)
		{
			var p = r.P;
			if (r.Growable != null)
			{
				p.HouseholdSlots = r.Growable.HouseholdSlots;
				if (initial || p.CompanyId == 0)
					r.Growable.FillJobSlots(p.JobSlots);

				return;
			}

			if (!initial)
				return;

			var cb = r.City;
			if (p.Kind == PropertyKind.Residential)
				p.HouseholdSlots = Math.Max(1, cb.Info.MaxResidents / Math.Max(1, Info.ResidentsPerHousehold));

			var mix = p.Kind switch
			{
				PropertyKind.Commercial => Info.CommercialJobMix,
				PropertyKind.Industrial or PropertyKind.Warehouse => Info.IndustrialJobMix,
				PropertyKind.Office => Info.OfficeJobMix,
				_ => Info.ServiceJobMix,
			};

			Distribute(cb.Info.MaxJobs, mix, p.JobSlots);
		}

		/// <summary>Splits `total` into slots by percent, remainder to the largest share (deterministic).</summary>
		public static void Distribute(int total, ReadOnlySpan<int> percent, int[] slots)
		{
			Array.Clear(slots);
			if (total <= 0)
				return;

			var assigned = 0;
			var best = 0;
			for (var i = 0; i < slots.Length && i < percent.Length; i++)
			{
				slots[i] = total * percent[i] / 100;
				assigned += slots[i];
				if (percent[i] > percent[best])
					best = i;
			}

			slots[best] += total - assigned;
		}

		/// <summary>Land value and rent of a record (growables only; other buildings have no rent).</summary>
		void RefreshMoney(Record r)
		{
			var p = r.P;
			var cells = p.Width * p.Depth;
			var lv = SampleLandValue(r);
			p.LandValue = lv;
			if (landValue != null)
				r.City.SetZoningLandValue(lv);

			if (r.Growable != null)
				p.RentPerMonth = LotMath.Rent(r.Growable.Info, cells, r.Growable.Level, lv, Info.RentScalePercent);
		}

		int SampleLandValue(Record r)
		{
			if (landValue == null)
				return Math.Clamp(r.City.LandValue, 0, 100);

			var p = r.P;
			var sum = 0;
			var n = 0;
			for (var y = 0; y < p.Depth; y++)
				for (var x = 0; x < p.Width; x++)
				{
					sum += landValue.GetLandValue(p.Origin + new CVec(x, y));
					n++;
				}

			return n > 0 ? sum / n : 0;
		}

		void ITick.Tick(Actor self)
		{
			var pulse = Math.Max(1, Info.Pulse);
			if (world.WorldTick % pulse != 0)
				return;

			roads ??= world.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			landValue ??= world.WorldActor.TraitsImplementing<ILandValueSource>().FirstOrDefault();

			var roadsChanged = roads != null && roads.NetworkVersion != roadVersion;
			if (roadsChanged)
				roadVersion = roads.NetworkVersion;

			// Flags every pulse; land value and rent for a quarter of the records per pulse (every 100 ticks each).
			var phase = world.WorldTick / pulse % 4;
			for (var i = 0; i < records.Count; i++)
			{
				var r = records[i];
				var p = r.P;
				if (roadsChanged)
					RefreshAccess(r);

				if (i % 4 == phase)
					RefreshMoney(r);

				var operational = r.City.IsOperational;
				var access = RoadAccess(r);
				if (operational != p.Operational || access != p.HasRoadAccess)
				{
					p.Operational = operational;
					p.HasRoadAccess = access;
					Version++;
					Changed?.Invoke(p);
				}
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			world.ActorAdded -= OnActorAdded;
			world.ActorRemoved -= OnActorRemoved;
			disposed = true;
		}
	}
}
