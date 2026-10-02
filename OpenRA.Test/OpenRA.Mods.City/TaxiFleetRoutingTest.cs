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

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.City;
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class TaxiFleetRoutingTest
	{
		sealed class TestRoads : IRoadNetwork
		{
			public readonly HashSet<CPos> Cells = [];
			public readonly HashSet<(CPos Cell, int Direction)> Blocked = [];
			public readonly Dictionary<CPos, int> Speeds = [];

			public int NetworkVersion => 1;
			public IEnumerable<CPos> RoadCells => Cells;
			public bool IsRoad(CPos cell) => Cells.Contains(cell);
			public bool CanEnter(CPos from, int dir) => !Blocked.Contains((from, dir)) && IsRoad(from) && IsRoad(from + CityUtils.Neighbours4[dir]);
			public RoadClass GetClass(CPos cell) => RoadClass.Local;
			public int GetLanes(CPos cell) => 1;
			public int GetSpeedPercent(CPos cell) => Speeds.TryGetValue(cell, out var speed) ? speed : 100;
			public JunctionControl GetControl(CPos cell) => JunctionControl.None;
			public int GetParkingSlots(CPos cell) => 0;
			public bool IsConnectedToOutside(CPos roadCell) => false;
			public CPos GetAccessRoad(IEnumerable<CPos> footprint) => CPos.Zero;
		}

		static void SetField(object instance, string name, object value)
		{
			instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(instance, value);
		}

		static void Invoke(TransitLayer layer, string method, params object[] args)
		{
			typeof(TransitLayer).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layer, args);
		}

		static (TransitLayer Layer, TestRoads Roads, TransitDepotRecord Depot) CreateFleet()
		{
			// Provide only map geometry and world ownership: dispatch still uses the real router, StartMove and return logic.
			var map = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
			SetField(map, nameof(Map.Grid), RuntimeHelpers.GetUninitializedObject(typeof(MapGrid)));
			typeof(Map).GetProperty(nameof(Map.MapSize)).SetValue(map, new Size(40, 5));
			map.Bounds = new Rectangle(0, 0, 40, 5);
			var world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));
			SetField(world, nameof(World.Map), map);
			var layer = (TransitLayer)RuntimeHelpers.GetUninitializedObject(typeof(TransitLayer));
			SetField(layer, nameof(TransitLayer.Info), new TransitLayerInfo());
			SetField(layer, "world", world);
			SetField(layer, "stops", new List<TransitStop>());
			SetField(layer, "lines", new List<TransitLine>());
			SetField(layer, "vehicles", new List<TransitVehicle>());
			SetField(layer, "nextVehicleId", 1);
			var depot = new TransitDepotRecord { ActorId = 1, Mode = TransitMode.Taxi, Capacity = 4, Road = new CPos(1, 1) };
			SetField(layer, "depots", new List<TransitDepotRecord> { depot });
			var roads = new TestRoads();
			SetField(layer, "roads", roads);
			SetField(layer, "router", new TransitRouter(map, roads, c => TravelTimeCalibration.ScaleSpeed(36, roads.GetSpeedPercent(c))));
			SetField(layer, "previewRouter", new TransitRouter(map, roads));
			SetField(layer, "walkRouter", new TransitRouter(map, _ => false, (_, _) => false));
			return (layer, roads, depot);
		}

		static void AddRoad(TestRoads roads, int first, int last)
		{
			for (var x = first; x <= last; x++)
				roads.Cells.Add(new CPos(x, 1));
		}

		static void AddStand(TransitLayer layer, int id, CPos cell)
		{
			((List<TransitStop>)layer.Stops).Add(new TransitStop { Id = id, Mode = TransitMode.Taxi, Cell = cell });
		}

		static void DispatchOrReturn(TransitLayer layer, TransitDepotRecord depot, bool returning)
		{
			if (!returning)
			{
				Invoke(layer, "ManageTaxiFleet", 100);
				return;
			}

			var taxi = new TransitVehicle { Id = 1, Mode = TransitMode.Taxi, DepotId = depot.ActorId, Cell = depot.Road, State = TransitVehicleState.Carrying };
			((List<TransitVehicle>)layer.Vehicles).Add(taxi);
			depot.Used++;
			Invoke(layer, "HandleTaxiArrivalCarrying", taxi, 100);
		}

		static void AssertTarget(TransitLayer layer, TransitDepotRecord depot, int standId, CPos target)
		{
			Assert.That(layer.Vehicles, Has.Count.EqualTo(1));
			var taxi = layer.Vehicles[0];
			Assert.That(taxi.State, Is.EqualTo(TransitVehicleState.ToPickup));
			Assert.That(taxi.StopId, Is.EqualTo(standId));
			Assert.That(taxi.LegCells[0], Is.EqualTo(depot.Road));
			Assert.That(taxi.LegCells[^1], Is.EqualTo(target));
			Assert.That(depot.Used, Is.EqualTo(1));
		}

		static void AssertNoActiveVehicles(TransitLayer layer, bool returning)
		{
			if (!returning)
			{
				Assert.That(layer.Vehicles, Is.Empty);
				return;
			}

			Assert.That(layer.Vehicles, Has.Count.EqualTo(1));
			Assert.That(layer.Vehicles[0].State, Is.EqualTo(TransitVehicleState.Gone));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void AbsentStandDoesNotDispatchOrRetainATaxi(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			roads.Cells.Add(depot.Road);
			Assert.DoesNotThrow(() => DispatchOrReturn(layer, depot, returning));
			AssertNoActiveVehicles(layer, returning);
			Assert.That(depot.Used, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void NoReachableStandDoesNotDispatchOrRetainATaxi(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			roads.Cells.UnionWith([depot.Road, new CPos(10, 1)]);
			AddStand(layer, 1, new CPos(10, 1));
			Assert.DoesNotThrow(() => DispatchOrReturn(layer, depot, returning));
			AssertNoActiveVehicles(layer, returning);
			Assert.That(depot.Used, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void AbsentRouterDoesNotDispatchOrRetainATaxi(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			AddRoad(roads, 1, 2);
			AddStand(layer, 1, new CPos(2, 1));
			SetField(layer, "router", null);
			Assert.DoesNotThrow(() => DispatchOrReturn(layer, depot, returning));
			AssertNoActiveVehicles(layer, returning);
			Assert.That(depot.Used, Is.Zero);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void DrivingStandMayBeBeyondPassengerWalkingRadius(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			AddRoad(roads, 1, 20);
			var target = new CPos(20, 1);
			AddStand(layer, 1, target);
			DispatchOrReturn(layer, depot, returning);
			AssertTarget(layer, depot, 1, target);
			Assert.That(layer.Vehicles[0].LegCells, Has.Length.EqualTo(20));
			SetField(layer, "walkRouter", typeof(TransitLayer).GetField("router", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer));
			layer.Vehicles[0].State = TransitVehicleState.Idle;
			Assert.That(layer.QuoteTaxi(depot.Road, target, AgeGroup.Adult).Available, Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void SidewalkGapDoesNotPreventFleetDrivingButStillPreventsPassengerBooking(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			AddRoad(roads, 1, 3);
			var target = new CPos(3, 1);
			AddStand(layer, 1, target);
			DispatchOrReturn(layer, depot, returning);
			AssertTarget(layer, depot, 1, target);
			layer.Vehicles[0].State = TransitVehicleState.Idle;
			Assert.That(layer.QuoteTaxi(depot.Road, target, AgeGroup.Adult).Available, Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void UnreachableClosestStandDoesNotHideFartherReachableStand(bool returning)
		{
			var (layer, roads, depot) = CreateFleet();
			AddRoad(roads, 1, 6);
			var closest = new CPos(1, 2);
			roads.Cells.Add(closest);
			roads.Blocked.Add((depot.Road, 2));
			var target = new CPos(6, 1);
			AddStand(layer, 1, closest);
			AddStand(layer, 2, target);
			DispatchOrReturn(layer, depot, returning);
			AssertTarget(layer, depot, 2, target);
		}

		[Test]
		public void ReturningTaxiDrivesHomeWhenNoStandIsReachable()
		{
			var (layer, roads, depot) = CreateFleet();
			AddRoad(roads, 1, 5);
			roads.Cells.Add(new CPos(10, 1));
			AddStand(layer, 1, new CPos(10, 1));
			var taxi = new TransitVehicle { Id = 1, Mode = TransitMode.Taxi, DepotId = depot.ActorId, Cell = new CPos(5, 1), State = TransitVehicleState.Carrying };
			((List<TransitVehicle>)layer.Vehicles).Add(taxi);
			depot.Used = 1;
			Invoke(layer, "HandleTaxiArrivalCarrying", taxi, 100);
			Assert.That(taxi.State, Is.EqualTo(TransitVehicleState.ToDepot));
			Assert.That(taxi.Recall, Is.True);
			Assert.That(taxi.LegCells, Has.Length.EqualTo(5));
			Assert.That(taxi.LegCells[^1], Is.EqualTo(depot.Road));
			Assert.That(depot.Used, Is.EqualTo(1));
		}

		[Test]
		public void FleetChoosesFastestStandAndOwnsSelectedRouteAcrossLaterSearches()
		{
			var (layer, roads, depot) = CreateFleet();
			depot.Road = new CPos(10, 1);
			AddRoad(roads, 8, 14);
			for (var x = 11; x <= 14; x++)
				roads.Speeds[new CPos(x, 1)] = 800;

			var target = new CPos(14, 1);
			AddStand(layer, 2, target);
			AddStand(layer, 1, new CPos(8, 1));
			DispatchOrReturn(layer, depot, false);
			AssertTarget(layer, depot, 2, target);
			var expected = new CPos[] { new(10, 1), new(11, 1), new(12, 1), new(13, 1), new(14, 1) };
			Assert.That(layer.Vehicles[0].LegCells, Is.EqualTo(expected));
		}

		[Test]
		public void EqualDrivingTimesChooseStableStandId()
		{
			var (layer, roads, depot) = CreateFleet();
			depot.Road = new CPos(10, 1);
			AddRoad(roads, 9, 11);
			AddStand(layer, 2, new CPos(9, 1));
			var target = new CPos(11, 1);
			AddStand(layer, 1, target);
			DispatchOrReturn(layer, depot, false);
			AssertTarget(layer, depot, 1, target);
		}
	}
}
