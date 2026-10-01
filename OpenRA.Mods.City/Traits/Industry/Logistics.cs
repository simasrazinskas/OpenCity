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
	[Desc("Freight logistics (design/06 3.5-3.8): shipments are plain data with a timing wheel, trucks are fleet slots,",
		"routes come from a cached BFS over the road network.",
		"A hash-sampled handful of shipments is drawn as real traffic trucks (ITrafficService trips, never camera based).")]
	public class LogisticsInfo : TraitInfo
	{
		[Desc("Ticks a truck needs per road cell (the truck sprite moves at about 14).")]
		public readonly int TruckTicksPerCell = 14;

		[Desc("Loading time before a truck leaves.")]
		public readonly int LoadTicks = 40;

		[Desc("Unloading and turnaround time at the end of the return trip.")]
		public readonly int TurnaroundTicks = 60;

		[Desc("A shipment that would have to wait longer than this for a free truck is not accepted (the seller's outbox backs up).")]
		public readonly int MaxQueueTicks = 2400;

		[Desc("Units per truck load by weight class 0..5 (class 0 = immaterial, no truck).")]
		public readonly int[] UnitsPerTruck = [0, 40, 30, 24, 16, 12];

		[Desc("Weight class by resource id (index = id, design/05 catalogue). Ids beyond the list use class 3.")]
		public readonly int[] ResourceWeights = IndustryResources.DefaultWeights;

		[Desc("Freight cost in milli-cents per unit, weight class and road cell.")]
		public readonly int FreightMilliCentsPerWeightCell = 100;

		[Desc("Trucks of a hub, base of a processor/company (+ level) and of a warehouse (+ lot cells / 4, max WarehouseMax).")]
		public readonly int HubTrucks = 4;
		public readonly int CompanyTrucks = 2;
		public readonly int WarehouseTrucks = 6;
		public readonly int WarehouseMax = 16;

		[Desc("Units per clock day one outside connection trades (imports and exports share it).")]
		public readonly int TradeUnitsPerDay = 600;

		[Desc("Route searches per tick; further requests wait (TryDispatch returns 0, the caller retries).")]
		public readonly int MaxBfsPerTick = 8;

		[Desc("Cached route fields (one BFS result per source road cell).")]
		public readonly int RouteCacheSize = 48;

		[Desc("One in this many shipments is drawn as a real truck (by hash of the shipment id).")]
		public readonly int SampleDiv = 4;

		[Desc("Visible truck budget: Base + population / PopPerVisible, capped.")]
		public readonly int MaxVisibleBase = 24;
		public readonly int PopPerVisible = 600;
		public readonly int MaxVisibleCap = 120;

		[Desc("VehicleType requested from the traffic service.")]
		public readonly string TruckVehicleType = "truck";

		[Desc("Congestion added per cell of a path per truck load (1/10 car equivalents; heavy trucks count 2.5 cars).")]
		public readonly int FreightPerLoad = 25;

		[Desc("Warehouse storage per lot cell: capacity per resource type = lot cells * this / weight class.")]
		public readonly int WarehouseUnitsPerCell = 60;

		[Desc("Resource types a warehouse stores.")]
		public readonly int WarehouseTypes = 3;

		[Desc("Warehouse stock balancing (design/06 3.8): a warehouse above High pushes to one below Low of the same type.")]
		public readonly int BalanceHighPercent = 80;
		public readonly int BalanceLowPercent = 30;

		[Desc("Percent of a donor's stock moved per day at most.")]
		public readonly int BalanceMaxMovePercent = 20;

		[Desc("Target fill percent a warehouse tries to reach through buy offers.")]
		public readonly int WarehouseTargetPercent = 50;

		[Desc("Warehouse buys at this percent of the base price and sells at SellMarkupPercent (ECO market offers).")]
		public readonly int BuyDiscountPercent = 90;
		public readonly int SellMarkupPercent = 105;

		[Desc("Trucks of a cargo terminal.")]
		public readonly int TerminalTrucks = 16;

		[Desc("Cost of the long-haul leg of a trade through a terminal, as a mode multiplier percent of a truck (design: rail 35, ship 25, air 300).")]
		public readonly int RailPercent = 35;
		public readonly int ShipPercent = 25;
		public readonly int AirPercent = 300;

		[Desc("Virtual long-haul distance (cells) charged for outside trade through a terminal / a highway connection. 0 = no extra freight",
			"cost, so terminals only add trade capacity. Raise both to make rail and ship cheaper than highway trucks.")]
		public readonly int TerminalLegCells = 0;
		public readonly int HighwayLegCells = 0;

		[Desc("City specialization bonus (design/05): up to this percent more efficiency at SpecializationThresholdUnits produced per month.")]
		public readonly int SpecializationMaxPercent = 15;
		public readonly int SpecializationThresholdUnits = 10000;

		public override object Create(ActorInitializer init) { return new Logistics(init.Self, this); }
	}

	public enum NodeKind : byte { Company, Storage, Terminal, Outside }

	public sealed class LogiNode
	{
		public int Id;
		public NodeKind Kind;
		public int PropertyId;
		public CPos Road;
		public int FleetOffset;
		public int FleetSize;
		public bool Alive = true;

		/// <summary>Outside connections and terminals: remaining trade capacity in milli-units.</summary>
		public long TradeTokensMilli;

		public int TradePerDay;
		public FreightMode Mode;

		/// <summary>Warehouses and terminals: stock and reserved inbound units per resource id (0..63), stored resource types.</summary>
		public int[] Stock;
		public int[] Inbound;
		public readonly int[] Types = new int[3];
		public int CapBase;
		public int StoragePerResource;
		public bool AnyType;

		public bool IsStorage => Kind == NodeKind.Storage || Kind == NodeKind.Terminal;
	}

	public struct Shipment
	{
		public int Id;
		public bool Active;
		public int FromNode, ToNode;
		public byte Resource;
		public ushort Units;
		public int DepartTick, ArriveTick, ReturnTick;
		public int UnitPriceCents, FreightCents;
		public int PathVersion;
		public int Next;
		public int Slot;
		public bool Sampled;
		public bool Redirected;
		public bool FromStorage;
		public bool ToStorage;
	}

	public partial class Logistics : ILogistics, ILogisticsEvents, ITripListener, ITick, IWorldLoaded, ISync, ICityAutoTestReporter
	{
		const int WheelSize = 8192;

		public readonly LogisticsInfo Info;
		readonly Actor self;
		readonly World world;

		readonly List<LogiNode> nodes = [];
		readonly Dictionary<int, LogiNode> nodeByProperty = [];
		readonly List<LogiNode> outsideNodes = [];
		readonly List<LogiNode> terminalNodes = [];
		readonly List<LogiNode> storageNodes = [];
		int outsideCount = -1;
		int[] fleetFreeAt = new int[64];
		int fleetUsed;

		Shipment[] ships = new Shipment[256];
		readonly Stack<int> freeShips = new();
		readonly int[] wheelHead = new int[WheelSize];
		readonly int[] wheelTail = new int[WheelSize];
		int nextShipmentId = 1;

		IPropertyRegistry registry;
		ITrafficService traffic;
		IFreightFlow freightFlow;
		CityClock clock;
		ICitizenPopulation population;
		bool subscribed;

		readonly List<SampledTrip> sampled = [];

		struct SampledTrip
		{
			public int TripId;
			public int ExpireTick;
		}

		// Statistics (units): totals and the previous clock month.
		public readonly long[] DispatchedUnits = new long[64];
		public readonly long[] DeliveredUnits = new long[64];
		public readonly long[] ImportedUnits = new long[64];
		public readonly long[] ExportedUnits = new long[64];
		public readonly int[] MonthlyDelivered = new int[64];
		public readonly int[] LastMonthDelivered = new int[64];
		public readonly int[] Stranded = new int[64];
		int lastDay = -1;
		long totalReturned;
		long totalDelivered;
		long totalDispatched;
		int rejectedBusy;
		long tradedViaTerminals;

		public Logistics(Actor self, LogisticsInfo info)
		{
			this.self = self;
			Info = info;
			world = self.World;
			Array.Fill(wheelHead, -1);
			Array.Fill(wheelTail, -1);
			SetupRoutes();
		}

		public event Action<ShipmentEvent> Delivered;

		public event Action<ShipmentEvent> Returned;

		public int ShipmentsInTransit { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; } = 17;

		/// <summary>Real trucks currently requested from the traffic service.</summary>
		public int VisibleTrucks => sampled.Count;

		public int NodeCount => nodes.Count;

		public int MaxVisible
		{
			get
			{
				population ??= self.Owner.PlayerActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault()
					?? world.WorldActor.TraitsImplementing<ICitizenPopulation>().FirstOrDefault();
				var pop = population?.Population ?? self.Owner.PlayerActor.TraitOrDefault<CityManager>()?.Population ?? 0;
				return Math.Min(Info.MaxVisibleCap, Info.MaxVisibleBase + pop / Math.Max(1, Info.PopPerVisible));
			}
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			Resolve();
		}

		void Resolve()
		{
			registry ??= world.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			traffic ??= world.WorldActor.TraitsImplementing<ITrafficService>().FirstOrDefault();
			freightFlow ??= world.WorldActor.TraitsImplementing<IFreightFlow>().FirstOrDefault();
			clock ??= world.WorldActor.TraitOrDefault<CityClock>();
			roads ??= world.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			if (registry != null && !subscribed)
			{
				subscribed = true;
				registry.Removed += OnPropertyRemoved;
				registry.Added += OnPropertyAdded;
				foreach (var p in registry.All.ToArray())
					OnPropertyAdded(p);
			}
		}

		// Warehouses and cargo terminals become nodes as soon as they exist, so ECO can see their offers.
		void OnPropertyAdded(Property p)
		{
			if (nodeByProperty.ContainsKey(p.Id))
				return;

			var isTerminal = p.Actor != null && p.Actor.TraitOrDefault<ICargoTerminal>() != null;
			if (p.Kind == PropertyKind.Warehouse || isTerminal)
				CreateNode(p);
		}

		void OnPropertyRemoved(Property p)
		{
			if (nodeByProperty.TryGetValue(p.Id, out var node))
			{
				node.Alive = false;
				nodeByProperty.Remove(p.Id);
			}
		}

		int WeightClass(int resource)
		{
			if (resource < 0)
				return 0;

			return resource < Info.ResourceWeights.Length ? Math.Clamp(Info.ResourceWeights[resource], 0, 5) : 3;
		}

		int UnitsPerTruck(int weightClass)
		{
			return Math.Max(1, Info.UnitsPerTruck[Math.Clamp(weightClass, 0, Info.UnitsPerTruck.Length - 1)]);
		}

		int TicksPerDay => clock?.TicksPerDay ?? 2400;

		int FleetSizeFor(Property p)
		{
			switch (p.Kind)
			{
				case PropertyKind.Extractor: return Info.HubTrucks;
				case PropertyKind.Warehouse: return Math.Min(Info.WarehouseMax, Info.WarehouseTrucks + Math.Max(1, p.Width * p.Depth) / 4);
				default:
					// Hubs are not always flagged as extractors by the registry baseline.
					if (p.Actor != null && !p.Actor.Disposed && p.Actor.TraitOrDefault<ExtractorHub>() != null)
						return Info.HubTrucks;

					return Info.CompanyTrucks + Math.Max(0, p.Level);
			}
		}

		LogiNode NodeOf(int propertyId)
		{
			Resolve();
			if (nodeByProperty.TryGetValue(propertyId, out var n))
			{
				// The registry re-derives access roads when the network changes.
				var current = registry?.Get(propertyId);
				if (current != null)
					n.Road = current.AccessRoad;

				return n;
			}

			var p = registry?.Get(propertyId);
			return p == null ? null : CreateNode(p);
		}

		LogiNode CreateNode(Property p)
		{
			var terminal = p.Actor != null && !p.Actor.Disposed ? p.Actor.TraitOrDefault<ICargoTerminal>() : null;
			var kind = terminal != null ? NodeKind.Terminal : p.Kind == PropertyKind.Warehouse ? NodeKind.Storage : NodeKind.Company;
			var size = terminal != null ? Info.TerminalTrucks : FleetSizeFor(p);
			var node = new LogiNode
			{
				Id = nodes.Count + 1,
				Kind = kind,
				PropertyId = p.Id,
				Road = p.AccessRoad,
				FleetOffset = AllocFleet(size),
				FleetSize = size,
			};

			if (kind != NodeKind.Company)
			{
				node.Stock = new int[64];
				node.Inbound = new int[64];
				node.CapBase = Math.Max(1, p.Width * p.Depth) * Info.WarehouseUnitsPerCell;
			}

			if (terminal != null)
			{
				node.Mode = terminal.Mode;
				node.TradePerDay = terminal.TradeUnitsPerDay;
				node.StoragePerResource = terminal.StorageUnitsPerResource;
				node.AnyType = true;
				node.TradeTokensMilli = (long)node.TradePerDay * 1000 / 2;
				terminalNodes.Add(node);
			}

			nodes.Add(node);
			nodeByProperty[p.Id] = node;
			if (kind != NodeKind.Company)
				storageNodes.Add(node);

			return node;
		}

		int AllocFleet(int size)
		{
			var offset = fleetUsed;
			fleetUsed += size;
			if (fleetUsed > fleetFreeAt.Length)
				Array.Resize(ref fleetFreeAt, Math.Max(fleetUsed, fleetFreeAt.Length * 2));

			return offset;
		}

		void RefreshOutsideNodes()
		{
			var count = 0;
			foreach (var _ in world.ActorsWithTrait<OutsideConnection>())
				count++;

			if (count == outsideCount)
				return;

			outsideCount = count;
			var known = new Dictionary<int, LogiNode>();
			foreach (var n in outsideNodes)
				known[n.PropertyId] = n;

			outsideNodes.Clear();
			foreach (var kv in world.ActorsWithTrait<OutsideConnection>().OrderBy(a => a.Actor.ActorID))
			{
				var key = (int)kv.Actor.ActorID;
				if (!known.TryGetValue(key, out var node))
				{
					node = new LogiNode
					{
						Id = nodes.Count + 1,
						Kind = NodeKind.Outside,
						PropertyId = key,
						TradePerDay = Info.TradeUnitsPerDay,
						TradeTokensMilli = (long)Info.TradeUnitsPerDay * 1000 / 2,
					};
					nodes.Add(node);
				}

				node.Road = kv.Actor.Location;
				outsideNodes.Add(node);
			}
		}

		int HaulPerUnitCents(int weightClass, int length)
		{
			return (int)Math.Max(1, (long)weightClass * length * Info.FreightMilliCentsPerWeightCell / 1000);
		}

		public int HaulCostCents(CPos fromRoad, CPos toRoad, int resourceId)
		{
			var wc = WeightClass(resourceId);
			if (wc == 0)
				return 0;

			var length = RouteLength(fromRoad, toRoad);
			if (length < 0)
				length = Math.Abs(fromRoad.X - toRoad.X) + Math.Abs(fromRoad.Y - toRoad.Y);

			return HaulPerUnitCents(wc, length);
		}

		/// <summary>Travel ticks one way between two road cells, -1 if unreachable (or no road network).</summary>
		public int TravelTicks(CPos fromRoad, CPos toRoad)
		{
			var length = RouteLength(fromRoad, toRoad);
			return length < 0 ? -1 : length * Info.TruckTicksPerCell;
		}

		int LegPerUnitCents(LogiNode link, int wc)
		{
			long cells = Info.HighwayLegCells;
			if (link.Kind == NodeKind.Terminal)
			{
				var percent = 100;
				if (link.Mode == FreightMode.Rail)
					percent = Info.RailPercent;
				else if (link.Mode == FreightMode.Ship)
					percent = Info.ShipPercent;
				else if (link.Mode == FreightMode.Air)
					percent = Info.AirPercent;

				cells = (long)Info.TerminalLegCells * percent / 100;
			}

			return (int)(wc * cells * Info.FreightMilliCentsPerWeightCell / 1000);
		}

		bool LinkUsable(LogiNode o)
		{
			if (!o.Alive || o.TradeTokensMilli < 1000)
				return false;

			return o.Kind != NodeKind.Terminal || (registry?.Get(o.PropertyId)?.Operational ?? true);
		}

		/// <summary>Cheapest outside link (highway or cargo terminal) with trade capacity left for a trade with `other`.</summary>
		LogiNode BestOutside(LogiNode other, bool importing, int wc)
		{
			RefreshOutsideNodes();
			LogiNode best = null;
			var bestCost = int.MaxValue;
			var bestLength = int.MaxValue;
			for (var k = 0; k < outsideNodes.Count + terminalNodes.Count; k++)
			{
				var o = k < outsideNodes.Count ? outsideNodes[k] : terminalNodes[k - outsideNodes.Count];
				if (!LinkUsable(o) || (o.Kind == NodeKind.Terminal && o.PropertyId == other.PropertyId))
					continue;

				var length = importing ? RouteLength(o.Road, other.Road) : RouteLength(other.Road, o.Road);
				if (length < 0)
					continue;

				var cost = HaulPerUnitCents(wc, length) + LegPerUnitCents(o, wc);
				if (cost < bestCost || (cost == bestCost && length < bestLength))
				{
					best = o;
					bestCost = cost;
					bestLength = length;
				}
			}

			return best;
		}

		public int TryDispatch(int fromProperty, int toProperty, int resourceId, int units, int unitPriceCents)
		{
			if (units <= 0 || resourceId <= 0 || (fromProperty == 0 && toProperty == 0))
				return 0;

			Resolve();
			LogiNode from = null;
			LogiNode to = null;
			if (fromProperty != 0 && (from = NodeOf(fromProperty)) == null)
				return 0;

			if (toProperty != 0 && (to = NodeOf(toProperty)) == null)
				return 0;

			var wc = WeightClass(resourceId);
			var fromOutside = from == null;
			var toOutside = false;
			if (fromOutside)
				from = BestOutside(to, true, wc);
			else if (to == null)
			{
				to = BestOutside(from, false, wc);
				toOutside = true;
			}

			if (from == null || to == null)
				return 0;

			var length = wc == 0 ? 0 : RouteLength(from.Road, to.Road);
			if (length < 0)
			{
				if (length == RouteBusy)
					rejectedBusy++;

				return 0;
			}

			return Dispatch(from, to, resourceId, units, unitPriceCents, wc, length, fromOutside, toOutside);
		}

		int Dispatch(LogiNode from, LogiNode to, int resource, int units, int priceCents, int wc, int length, bool fromOutside, bool toOutside)
		{
			var now = world.WorldTick;
			var outside = fromOutside ? from : toOutside ? to : null;
			if (outside != null)
				units = (int)Math.Min(units, outside.TradeTokensMilli / 1000);

			var fromStore = !fromOutside && from.IsStorage;
			var toStore = !toOutside && to.IsStorage;
			if (wc == 0 && (fromStore || toStore))
				return 0;

			if (fromStore)
				units = Math.Min(units, StockOf(from, resource));

			if (toStore)
				units = Math.Min(units, FreeSpace(to, resource));

			if (units <= 0)
				return 0;

			var perTruck = wc == 0 ? int.MaxValue : UnitsPerTruck(wc);
			var perCell = Info.TruckTicksPerCell;
			var legPerUnit = outside != null ? LegPerUnitCents(outside, wc) : 0;
			var dispatched = 0;
			var loads = 0;
			while (dispatched < units)
			{
				var load = Math.Min(perTruck, units - dispatched);
				var slot = -1;
				int depart;
				if (wc == 0)
					depart = now;
				else
				{
					depart = now + Info.LoadTicks;
					if (from.FleetSize > 0 && !fromOutside)
					{
						var best = int.MaxValue;
						for (var i = 0; i < from.FleetSize; i++)
							if (fleetFreeAt[from.FleetOffset + i] < best)
							{
								best = fleetFreeAt[from.FleetOffset + i];
								slot = from.FleetOffset + i;
							}

						depart = Math.Max(depart, best);
					}
				}

				if (depart - now > Info.MaxQueueTicks)
					break;

				var arrive = wc == 0 ? now + 1 : depart + length * perCell;
				var returnAt = wc == 0 ? arrive : arrive + length * perCell + Info.TurnaroundTicks;
				if (slot >= 0)
					fleetFreeAt[slot] = returnAt;

				var freight = (HaulPerUnitCents(wc, length) + legPerUnit) * load;
				var index = AddShipment(from, to, resource, load, depart, arrive, returnAt, priceCents, freight, slot);
				ships[index].FromStorage = fromStore;
				ships[index].ToStorage = toStore;
				if (wc != 0)
					TrySampleTruck(index, from, to, depart, returnAt);

				dispatched += load;
				loads++;
			}

			if (dispatched == 0)
			{
				rejectedBusy++;
				return 0;
			}

			if (outside != null)
			{
				outside.TradeTokensMilli -= dispatched * 1000L;
				if (outside.Kind == NodeKind.Terminal)
					tradedViaTerminals += dispatched;
			}

			var r = Math.Min(resource, 63);
			if (fromStore)
				from.Stock[r] -= dispatched;

			if (toStore)
				to.Inbound[r] += dispatched;

			DispatchedUnits[r] += dispatched;
			totalDispatched += dispatched;
			AddStat(IndustryStat.Dispatched, r, dispatched);
			if (fromOutside)
			{
				ImportedUnits[r] += dispatched;
				AddStat(IndustryStat.Imported, r, dispatched);
			}
			else if (toOutside)
			{
				ExportedUnits[r] += dispatched;
				AddStat(IndustryStat.Exported, r, dispatched);
			}

			if (wc != 0 && loads > 0)
				AddFreight(from.Road, to.Road, loads);

			return dispatched;
		}

		/// <summary>Returns the index into the shipment pool.</summary>
		int AddShipment(LogiNode from, LogiNode to, int resource, int units, int depart, int arrive, int returnAt, int priceCents, int freightCents, int slot)
		{
			int index;
			if (freeShips.Count > 0)
				index = freeShips.Pop();
			else
			{
				index = nextSlot++;
				if (index >= ships.Length)
					Array.Resize(ref ships, ships.Length * 2);
			}

			var id = nextShipmentId++;
			ships[index] = new Shipment
			{
				Id = id,
				Active = true,
				FromNode = from.Id,
				ToNode = to.Id,
				Resource = (byte)Math.Min(resource, 255),
				Units = (ushort)units,
				DepartTick = depart,
				ArriveTick = arrive,
				ReturnTick = returnAt,
				UnitPriceCents = priceCents,
				FreightCents = freightCents,
				PathVersion = roads?.NetworkVersion ?? 0,
				Next = -1,
				Slot = slot,
			};

			Enqueue(index, arrive);
			ShipmentsInTransit++;
			unchecked
			{
				StateHash = StateHash * 31 + id;
				StateHash = StateHash * 31 + from.Id * 7 + to.Id;
				StateHash = StateHash * 31 + resource * 1009 + units;
				StateHash = StateHash * 31 + arrive;
			}

			return index;
		}

		int nextSlot;

		void Enqueue(int index, int arriveTick)
		{
			var bucket = arriveTick & (WheelSize - 1);
			ships[index].Next = -1;
			if (wheelTail[bucket] < 0)
				wheelHead[bucket] = index;
			else
				ships[wheelTail[bucket]].Next = index;

			wheelTail[bucket] = index;
		}
	}
}
