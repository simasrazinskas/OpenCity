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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Orders of the services work package. Every order's Subject is the player actor; resolved by ServiceSimulation.</summary>
	public static class ServiceOrders
	{
		/// <summary>ExtraLocation.X = (int)ServiceKind, ExtraData = budget percent (50..150).</summary>
		public const string SetBudget = "CitySetBudget";

		/// <summary>Target = the service building actor, ExtraData = index into its ServiceUpgrade traits (declaration order).</summary>
		public const string BuyUpgrade = "CityBuyUpgrade";

		public static Order SetBudgetOrder(Player p, ServiceKind kind, int percent) =>
			new(SetBudget, p.PlayerActor, false) { ExtraLocation = new CPos((int)kind, 0), ExtraData = (uint)percent };

		public static Order BuyUpgradeOrder(Player p, Actor building, int upgradeIndex) =>
			new(BuyUpgrade, p.PlayerActor, Target.FromActor(building), false) { ExtraData = (uint)upgradeIndex };
	}

	/// <summary>Read-only snapshot of one service building for the building panel / info views.</summary>
	public struct ServiceBuildingStatus
	{
		public ServiceKind Kind;
		public bool Active;

		/// <summary>0..125, budget x staff x utilities.</summary>
		public int Efficiency;

		/// <summary>0..100 provider satisfaction (capacity / load).</summary>
		public int Satisfaction;

		public int Fleet, FleetInUse;

		/// <summary>Kind-specific capacity (beds, seats, kg, cells...) after upgrades and efficiency.</summary>
		public int Capacity;

		/// <summary>Current use of Capacity: patients, enrolled students, stored kg, bodies, inmates, ...</summary>
		public int Used;

		/// <summary>Coverage load assigned to this provider (kind-specific units).</summary>
		public int Load;

		public int UpgradeMask;
	}

	[Desc("An optional in-place upgrade of a service building (declare several as ServiceUpgrade@name). Bought with the CityBuyUpgrade order.")]
	public class ServiceUpgradeInfo : TraitInfo, Requires<ServiceBuildingInfo>
	{
		[FieldLoader.Require]
		[Desc("Fluent key of the upgrade name.")]
		public readonly string Name = null;

		[Desc("Fluent key of the description.")]
		public readonly string Description = null;

		public readonly int Cost = 1000;

		[Desc("Extra monthly upkeep.")]
		public readonly int Upkeep = 0;

		public readonly int AddFleet = 0;
		public readonly int AddCapacity = 0;
		public readonly int AddThroughput = 0;

		[Desc("Percent added to the efficiency of the building.")]
		public readonly int AddEfficiency = 0;

		[Desc("Percent by which the pollution emitted by the building is reduced.")]
		public readonly int PollutionReduction = 0;

		[Desc("Power produced in addition (incinerator furnace, extra turbine).")]
		public readonly int AddPower = 0;

		public override object Create(ActorInitializer init) { return new ServiceUpgrade(this); }
	}

	public class ServiceUpgrade
	{
		public readonly ServiceUpgradeInfo Info;
		public ServiceUpgrade(ServiceUpgradeInfo info) { Info = info; }
	}

	/// <summary>Group of providers sharing one road catchment (education has one group per school level).</summary>
	enum CatchmentGroup
	{
		Garbage = 0, Health, Deathcare, Edu1, Edu2, Edu3, Edu4, Police, Fire, Parks, Post, Telecom, Admin,
		Count
	}

	static class ServiceMath
	{
		public const int GroupCount = (int)CatchmentGroup.Count;
		public const int KindCount = 13;

		public static CatchmentGroup GroupOf(ServiceBuildingInfo i)
		{
			switch (i.Kind)
			{
				case ServiceKind.Garbage: return CatchmentGroup.Garbage;
				case ServiceKind.Health: return CatchmentGroup.Health;
				case ServiceKind.Deathcare: return CatchmentGroup.Deathcare;
				case ServiceKind.Education: return CatchmentGroup.Edu1 + Math.Clamp(i.SchoolLevel, 1, 4) - 1;
				case ServiceKind.Police: return CatchmentGroup.Police;
				case ServiceKind.Fire: return CatchmentGroup.Fire;
				case ServiceKind.Parks: return CatchmentGroup.Parks;
				case ServiceKind.Post: return CatchmentGroup.Post;
				case ServiceKind.Telecom: return CatchmentGroup.Telecom;
				default: return CatchmentGroup.Admin;
			}
		}

		public static ServiceKind KindOf(CatchmentGroup g)
		{
			switch (g)
			{
				case CatchmentGroup.Garbage: return ServiceKind.Garbage;
				case CatchmentGroup.Health: return ServiceKind.Health;
				case CatchmentGroup.Deathcare: return ServiceKind.Deathcare;
				case CatchmentGroup.Edu1:
				case CatchmentGroup.Edu2:
				case CatchmentGroup.Edu3:
				case CatchmentGroup.Edu4: return ServiceKind.Education;
				case CatchmentGroup.Police: return ServiceKind.Police;
				case CatchmentGroup.Fire: return ServiceKind.Fire;
				case CatchmentGroup.Parks: return ServiceKind.Parks;
				case CatchmentGroup.Post: return ServiceKind.Post;
				case CatchmentGroup.Telecom: return ServiceKind.Telecom;
				default: return ServiceKind.Admin;
			}
		}

		/// <summary>CS2 budget curve: 50% gives 25 efficiency, 100% gives 100, 150% gives 125.</summary>
		public static int BudgetEfficiency(int budget)
		{
			return budget <= 100 ? 25 + 3 * (budget - 50) / 2 : 100 + (budget - 100) / 2;
		}

		public static int Hash(int a, int b, int salt)
		{
			unchecked
			{
				var h = (uint)a * 2654435761u ^ (uint)b * 2246822519u ^ (uint)salt * 3266489917u;
				h ^= h >> 15;
				h *= 2654435761u;
				h ^= h >> 13;
				h *= 2246822519u;
				h ^= h >> 16;
				return (int)(h & 0x7fffffff);
			}
		}
	}
}
