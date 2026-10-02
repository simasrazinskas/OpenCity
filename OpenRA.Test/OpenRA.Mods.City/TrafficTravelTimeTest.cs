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
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class TrafficTravelTimeTest
	{
		[TestCase(40, 36000)]
		[TestCase(50, 28800)]
		[TestCase(100, 14400)]
		[TestCase(5, 288000)]
		public void PhysicalSpeedUsesDistanceAndBaseTick(int speedKph, int milliTicks)
		{
			Assert.That(TravelTimeCalibration.MilliTicksPerCell(16, speedKph, 40), Is.EqualTo(milliTicks));
		}

		[Test]
		public void TruckAndEmergencySpeedMultipliersUseTheSamePhysicalScale()
		{
			Assert.That(TravelTimeCalibration.ScaleSpeed(36000, 80), Is.EqualTo(45000));
			Assert.That(TravelTimeCalibration.ScaleSpeed(36000, 150), Is.EqualTo(24000));
		}

		[Test]
		public void SignalQuoteAccountsForActualRedPhaseAndClearance()
		{
			Assert.That(TravelTimeCalibration.SignalWaitTicks(1500, 50), Is.EqualTo(214));
		}

		[Test]
		public void PhysicalCalibrationRejectsInvalidInputs()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => TravelTimeCalibration.MilliTicksPerCell(16, 0, 40));
		}

		[Test]
		public void SidewalkDetourUsesActualRouteLength()
		{
			// Origin/destination are Manhattan distance 2 apart; the missing middle requires 4 sidewalk steps.
			var router = new WalkingRouter(3, 2);
			byte[] edges = [4, 0, 4, 3, 10, 9];
			bool[] sidewalk = [true, false, true, true, true, true];
			var path = router.FindRoute(edges, sidewalk, 0, 2, 100, out var deferred);
			Assert.That(deferred, Is.False);
			Assert.That(path, Is.EqualTo(new byte[] { 2, 1, 1, 0 }));
			Assert.That(path.Length * 288, Is.EqualTo(1152));
		}

		[Test]
		public void SidewalksAllowReverseTravelOnOneWayStreets()
		{
			var router = new WalkingRouter(3, 1);
			byte[] edges = [2, 10, 8];
			bool[] sidewalk = [true, true, true];
			Assert.That(router.FindRoute(edges, sidewalk, 2, 0, 100, out _), Is.EqualTo(new byte[] { 3, 3 }));
		}

		[Test]
		public void DisconnectedSidewalkNeverCompletesAnImaginaryWalk()
		{
			var router = new WalkingRouter(3, 1);
			byte[] edges = [0, 0, 0];
			bool[] sidewalk = [true, false, true];
			Assert.That(router.FindRoute(edges, sidewalk, 0, 2, 100, out var deferred), Is.Null);
			Assert.That(deferred, Is.False);
		}

		[Test]
		public void CancelledLongWalkReleasesLivePendingCapacityImmediately()
		{
			var sim = (TrafficSim)RuntimeHelpers.GetUninitializedObject(typeof(TrafficSim));
			var tripsField = typeof(TrafficSim).GetField("trips", BindingFlags.Instance | BindingFlags.NonPublic);
			var trips = (IDictionary)Activator.CreateInstance(tripsField.FieldType);
			tripsField.SetValue(sim, trips);
			var tripType = typeof(TrafficSim).GetNestedType("TripRec", BindingFlags.NonPublic);
			var trip = Activator.CreateInstance(tripType, true);
			tripType.GetField("WalkRoute").SetValue(trip, new byte[100]);
			tripType.GetField("WalkEnd").SetValue(trip, 28800);
			trips.Add(7, trip);
			((ITrafficService)sim).CancelTrip(7);
			Assert.That(trips.Count, Is.Zero);
			Assert.That(typeof(TrafficSim).GetProperty("PendingCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sim), Is.EqualTo(0));
		}

		[Test]
		public void DeferredRouteAllowanceEventuallyCoversTheFiniteMap()
		{
			var sim = (TrafficSim)RuntimeHelpers.GetUninitializedObject(typeof(TrafficSim));
			var info = new TrafficSimInfo();
			typeof(TrafficSimInfo).GetField(nameof(TrafficSimInfo.MaxRouteNodes)).SetValue(info, 2);
			typeof(TrafficSim).GetField(nameof(TrafficSim.Info)).SetValue(sim, info);
			typeof(TrafficSim).GetField("linkCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sim, 20);
			var method = typeof(TrafficSim).GetMethod("NextSearchNodes", BindingFlags.Instance | BindingFlags.NonPublic);
			var allowance = 2;
			int[] expectedAllowances = [4, 8, 16, 20, 20];
			foreach (var expected in expectedAllowances)
			{
				allowance = (int)method.Invoke(sim, [allowance]);
				Assert.That(allowance, Is.EqualTo(expected));
			}
		}

		[Test]
		public void SearchBudgetIsDeferredAndCanBeRetried()
		{
			var router = new WalkingRouter(3, 1);
			byte[] edges = [2, 10, 8];
			bool[] sidewalk = [true, true, true];
			Assert.That(router.FindRoute(edges, sidewalk, 0, 2, 1, out var deferred), Is.Null);
			Assert.That(deferred, Is.True);
			Assert.That(router.FindRoute(edges, sidewalk, 0, 2, 100, out deferred), Has.Length.EqualTo(2));
			Assert.That(deferred, Is.False);
		}
	}
}
