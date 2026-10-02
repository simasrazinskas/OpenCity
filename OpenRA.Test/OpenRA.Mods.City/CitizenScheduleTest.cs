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
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class CitizenScheduleTest
	{
		const int Hour = CityTime.DefaultTicksPerHour;
		const int Day = Hour * 24;

		const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		sealed class TestTraffic : ITrafficService
		{
			public readonly List<TripRequest> Requests = [];
			public int CarTicks = 400;
			public int WalkTicks = 1000;
			public bool Refuse;
			public int ActiveVehicles => 0;
			public int CityTrafficFlow => 100;
			public int StateHash => 0;
			public int RequestTrip(in TripRequest request, ITripListener listener)
			{
				Requests.Add(request);
				return Refuse ? 0 : Requests.Count;
			}

			public void CancelTrip(int tripId) { }
			public int EstimateTravelTicks(CPos fromRoad, CPos toRoad, TravelMode mode) => mode == TravelMode.Walk ? WalkTicks : CarTicks;
			public int GetTrafficLoad(CPos cell) => 0;
			public int GetTrafficFlow(CPos cell) => 100;
			public int GetNoise(CPos cell) => 0;
		}

		sealed class TestRegistry : IPropertyRegistry
		{
			public readonly List<Property> Properties = [];
			public int Version => 1;
			public IReadOnlyList<Property> All => Properties;
			public Property Get(int id) => Properties.Find(p => p.Id == id);
			public Property GetAt(CPos cell) => Properties.Find(p => p.Origin == cell);
			public Property GetByActor(Actor actor) => null;
			public event Action<Property> Added { add { } remove { } }
			public event Action<Property> Removed { add { } remove { } }
			public event Action<Property> Changed { add { } remove { } }
			public void ReportRentPaid(int propertyId, int dollars) { }
		}

		// Exercise the real citizen planner without loading a map, renderer, or the unrelated economy/service traits.
		sealed class Simulation
		{
			public readonly CitizenSim Sim;
			public readonly CitizenSimInfo Info = new();
			public readonly TestTraffic Traffic = new();
			public readonly TestRegistry Registry = new();
			readonly World world = (World)RuntimeHelpers.GetUninitializedObject(typeof(World));

			public Simulation()
			{
				var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
				Set(actor, "World", world);
				Sim = new CitizenSim(actor, Info);
				Set(Sim, "registry", Registry);
				Set(Sim, "traffic", Traffic);
				Set(Info, "DepartJitterTicks", 0);
				Set(Info, "LeisureTripPercent", 0);
				Registry.Properties.Add(new Property
				{
					Id = 1,
					Kind = PropertyKind.Residential,
					AccessRoad = new CPos(1, 1),
					Operational = true,
					HasRoadAccess = true
				});
				Registry.Properties.Add(new Property
				{
					Id = 2,
					Kind = PropertyKind.Office,
					AccessRoad = new CPos(21, 1),
					Operational = true,
					HasRoadAccess = true
				});
				Registry.Properties.Add(new Property
				{
					Id = 3,
					Kind = PropertyKind.Commercial,
					AccessRoad = new CPos(4, 1),
					Operational = true,
					HasRoadAccess = true
				});
				Call("AllocHousehold");
				Call("AllocCitizen");
				Call("AddMember", 0, 0);
				SetArrayMember("hhs", "Home", 1);
				SetArrayMember("hhs", "Cash", 10000);
				SetArrayMember("cits", "BirthDay", -30);
			}

			public void SetCitizen(string field, object value) => SetArrayMember("cits", field, value);
			public int CitizenValue(string field) => Convert.ToInt32(Get(((Array)Get(Sim, "cits")).GetValue(0), field), CultureInfo.InvariantCulture);

			void SetArrayMember(string arrayName, string field, object value)
			{
				var array = (Array)Get(Sim, arrayName);
				var citizen = array.GetValue(0);
				Set(citizen, field, value);
				array.SetValue(citizen, 0);
			}

			public void Worker()
			{
				SetCitizen("Flags", "Alive, Worker");
				SetCitizen("Work", 2);
			}

			public void Tick(int tick)
			{
				Set(world, "<WorldTick>k__BackingField", tick);
				Call("RunScheduledTrips", tick % Day);
			}

			public object Call(string name, params object[] args)
			{
				var method = typeof(CitizenSim).GetMethod(name, Fields);
				var parameters = method.GetParameters();
				var supplied = new object[parameters.Length];
				for (var i = 0; i < supplied.Length; i++)
					supplied[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;

				return method.Invoke(Sim, supplied);
			}

			public static object Leg(string kind) => Enum.Parse(typeof(CitizenSim).GetNestedType("Leg", BindingFlags.NonPublic), kind);
			public void Schedule(string kind, int due, int dest, int deadline, int hold = 0, int origin = 0)
			{
				Call("AddTrip", 0, Leg(kind), due, dest, deadline, hold, origin);
			}

			public void Arrive(int now)
			{
				Tick(now);
				((ITripListener)Sim).OnTripArrived(new TripResult
				{
					OwnerId = 1,
					TripId = CitizenValue("TripId"),
					DepartTick = now - 100,
					ArriveTick = now
				});
			}
		}

		static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);

		static void Set(object target, string field, object value)
		{
			var member = target.GetType().GetField(field, Fields);
			member.SetValue(target, value is string text && member.FieldType.IsEnum ? Enum.Parse(member.FieldType, text) : value);
		}

		[Test]
		public void CommuteDepartsUsingTravelEstimateAndPreparesTomorrowOnlyOnce()
		{
			var s = new Simulation();
			s.Worker();
			s.Call("PlanDay", 0, 30, 0);
			s.Call("PlanDay", 0, 30, 0);
			Assert.That(Get(s.Sim, "tripPoolCount"), Is.EqualTo(4));
			s.Tick(8 * Hour - 401);
			Assert.That(s.Traffic.Requests, Is.Empty);
			s.Tick(8 * Hour - 400); // Leave early enough to arrive for the 08:00 shift.
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			Assert.That(s.Traffic.Requests[0].Purpose, Is.EqualTo(TripPurpose.Work));
			s.Arrive(8 * Hour);
			Assert.That(s.CitizenValue("BusyUntil"), Is.EqualTo(16 * Hour));
		}

		[Test]
		public void FutureDepartureDoesNotRunOnTodaysMatchingRingSlot()
		{
			var s = new Simulation();
			s.Schedule("ToLeisure", Day + 100, 3, Day + 10000);
			s.Tick(100);
			Assert.That(s.Traffic.Requests, Is.Empty);
			s.Tick(Day + 100);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
		}

		[Test]
		public void OverlappingErrandWaitsForWorkAndFinishesBeforeReturningHome()
		{
			var s = new Simulation();
			s.Worker();
			s.SetCitizen("Loc", 2);
			s.SetCitizen("BusyUntil", 16 * Hour);
			s.Schedule("ToShop", 16 * Hour - 1000, 3, 22 * Hour);
			s.Schedule("ToHome", 16 * Hour, 0, 22 * Hour, origin: 2);
			s.Tick(16 * Hour - 1000);
			Assert.That(s.Traffic.Requests, Is.Empty);
			s.Tick(16 * Hour);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			s.Tick(16 * Hour + 1);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			Assert.That(s.Traffic.Requests[0].Purpose, Is.EqualTo(TripPurpose.Shopping));
			s.Arrive(16 * Hour + 2);
			s.Tick(16 * Hour + 3);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			Assert.That(s.CitizenValue("Loc"), Is.EqualTo(3));
			s.Tick(16 * Hour + 2 + 30 * Hour / 60);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(2));
			Assert.That(s.Traffic.Requests[1].Purpose, Is.EqualTo(TripPurpose.GoingHome));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void FailedOrRefusedTravelDoesNotTeleportOrDeliverBenefits(bool refuse)
		{
			var s = new Simulation();
			s.Traffic.Refuse = refuse;
			s.SetCitizen("Wellbeing", (byte)50);
			s.SetCitizen("Leisure", (byte)100);
			s.Schedule("ToLeisure", 100, 3, 10000);
			s.Tick(100);
			if (!refuse)
				((ITripListener)s.Sim).OnTripFailed(s.CitizenValue("TripId"), 1, TripFailure.NoRoute);

			Assert.That(s.CitizenValue("Loc"), Is.Zero);
			Assert.That(s.CitizenValue("Leisure"), Is.EqualTo(100));
			Assert.That(s.CitizenValue("Wellbeing"), Is.EqualTo(50));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void DemolishedVisitKeepsReachedRoadAndReturnsWithoutBenefits(bool beforeArrival)
		{
			var s = new Simulation();
			s.SetCitizen("Wellbeing", (byte)50);
			s.SetCitizen("Leisure", (byte)100);
			s.Schedule("ToLeisure", 100, 3, 10000);
			s.Tick(100);
			if (beforeArrival)
				s.Registry.Properties.RemoveAll(p => p.Id == 3);

			s.Arrive(200);
			if (!beforeArrival)
				s.Registry.Properties.RemoveAll(p => p.Id == 3);

			var returnAt = beforeArrival ? 201 : 200 + 90 * Hour / 60;
			s.Tick(returnAt);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(2));
			Assert.That(s.Traffic.Requests[1].OriginRoad, Is.EqualTo(new CPos(4, 1)));
			Assert.That(s.Traffic.Requests[1].Purpose, Is.EqualTo(TripPurpose.GoingHome));
			Assert.That(s.CitizenValue("Loc"), Is.EqualTo(3));
			if (beforeArrival)
			{
				Assert.That(s.CitizenValue("Leisure"), Is.EqualTo(100));
				Assert.That(s.CitizenValue("Wellbeing"), Is.EqualTo(50));
			}
		}

		[Test]
		public void DeferredQuoteRetriesNextTickWithoutConsumingTheActivity()
		{
			var s = new Simulation();
			s.Traffic.WalkTicks = -2;
			s.Schedule("ToLeisure", 100, 3, 10000);
			s.Tick(100);
			Assert.That(s.Traffic.Requests, Is.Empty);
			s.Traffic.WalkTicks = 1000;
			s.Tick(101);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			Assert.That(s.Traffic.Requests[0].AllowedModes, Is.EqualTo(TravelModes.Walk));
		}

		[Test]
		public void VisitFinishesAndReturnsHomeAcrossMidnight()
		{
			var s = new Simulation();
			s.Tick(Day - 1000);
			s.Call("ApplyArrival", 0, Simulation.Leg("ToLeisure"), 3);
			Assert.That(s.CitizenValue("BusyUntil"), Is.EqualTo(Day - 1000 + 90 * Hour / 60)); // 90 game minutes.
			s.Tick(90 * Hour / 60 - 1000); // same slot on an earlier day must not execute tomorrow's return.
			Assert.That(s.Traffic.Requests, Is.Empty);
			s.Tick(Day - 1000 + 90 * Hour / 60);
			Assert.That(s.Traffic.Requests.Count, Is.EqualTo(1));
			Assert.That(s.Traffic.Requests[0].Purpose, Is.EqualTo(TripPurpose.GoingHome));
		}

		[Test]
		public void ReusedCitizenSlotDoesNotInheritOldDepartures()
		{
			var s = new Simulation();
			s.Schedule("ToLeisure", 100, 3, 10000);
			s.Call("FreeCitizen", 0);
			s.Call("AllocCitizen");
			s.Call("AddMember", 0, 0);
			s.SetCitizen("BirthDay", -30); // even an identical birth date must not revive stale plans.
			s.Tick(100);
			Assert.That(s.Traffic.Requests, Is.Empty);
		}

		[Test]
		public void LowImmigrationRateCarriesRemaindersAcrossTheLongDay()
		{
			var s = new Simulation();
			Set(s.Info, "ImmigrationFillPercent", 100);
			Set(s.Info, "ImmigrationBase", 1);
			Set(s.Sim, "freeHomeSlots", 1);
			for (var tick = 0; tick < Day; tick += 25)
			{
				s.Tick(tick);
				s.Call("Immigrate");
			}

			// Empty home candidate list records the one attempted immigrant without constructing a household.
			Assert.That(Get(s.Sim, "refused"), Is.EqualTo(1));
			Assert.That(Get(s.Sim, "immigAccumMilli"), Is.Zero);
		}

		[Test]
		public void OneVisitorGroupPerDayDoesNotRoundDownToZero()
		{
			var s = new Simulation();
			Set(s.Sim, "fallbackGroupsPerDay", 1);
			Set(s.Sim, "<Population>k__BackingField", 10);
			for (var tick = 0; tick < Day; tick += 25)
			{
				s.Tick(tick);
				s.Call("TickTourists");
			}

			Assert.That(Get(s.Sim, "nextGroupId"), Is.EqualTo(2));
			Assert.That(Get(s.Sim, "touristAccumMilli"), Is.Zero);
		}

		[Test]
		public void NearbyReachableShopIsPreferredOverUnreachableCandidates()
		{
			var s = new Simulation();
			s.Registry.Get(2).HasRoadAccess = false;
			var candidates = new List<Property> { s.Registry.Get(2), s.Registry.Get(3) };
			Assert.That(s.Call("PickLocalDestination", 0, s.Registry.Get(1), candidates, false, 0), Is.EqualTo(s.Registry.Get(3)));
		}
	}
}
