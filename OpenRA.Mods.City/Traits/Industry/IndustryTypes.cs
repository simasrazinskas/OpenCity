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
	/// <summary>Natural resource layers (design/06 3.1). Stone has no stock: it is a yield factor of the terrain.</summary>
	public enum NaturalResourceKind : byte { Fertile, Forest, Ore, Oil, Stone, Fish }

	/// <summary>Resource ids of the default catalogue (design/05 3.1). ECO's economy.yaml may reorder them: names are resolved through ICityEconomy.ResourceName first.</summary>
	public static class IndustryResources
	{
		public const int Grain = 1, Vegetables = 2, Livestock = 3, Cotton = 4, Wood = 5, Ore = 6, Oil = 7, Stone = 8;

		public static readonly string[] DefaultNames =
		[
			null, "Grain", "Vegetables", "Livestock", "Cotton", "Wood", "Ore", "Oil", "Stone", "Timber", "Petrochemicals", "Metals",
			"Concrete", "Food", "Beverages", "Textiles", "Plastics", "Furniture", "Electronics", "Vehicles", "Machinery",
			"Software", "Financial", "Media", "Meals", "Entertainment",
		];

		/// <summary>Weight class 0..5 per default resource id (0 = immaterial, no truck).</summary>
		public static readonly int[] DefaultWeights =
		[
			0, 3, 2, 3, 2, 4, 5, 4, 5, 4, 3, 4, 5, 2, 3, 1, 2, 3, 1, 4, 4, 0, 0, 0, 0, 0,
		];

		/// <summary>Default id for a product name (case-insensitive), 0 if unknown.</summary>
		public static int DefaultId(string name)
		{
			if (string.IsNullOrEmpty(name))
				return 0;

			for (var i = 1; i < DefaultNames.Length; i++)
				if (string.Equals(DefaultNames[i], name, StringComparison.OrdinalIgnoreCase))
					return i;

			return 0;
		}
	}

	/// <summary>Order strings and factories of the industry WP (design/06 3.2). Resolved by the ExtractorTool player trait.</summary>
	public static class IndustryOrders
	{
		/// <summary>Target = end cell, ExtraLocation = start cell, ExtraData = hub ActorID, TargetString = "add" or "remove".</summary>
		public const string ExtractorArea = "CityExtractorArea";

		/// <summary>ExtraData = hub ActorID, TargetString = product name (farm hubs: Grain, Vegetables, Livestock, Cotton).</summary>
		public const string ExtractorSetProduct = "CityExtractorSetProduct";

		/// <summary>ExtraData = hub ActorID, TargetString = "clearcut", ExtraLocation.X = 1 (on) / 0 (off).</summary>
		public const string ExtractorSetPolicy = "CityExtractorSetPolicy";

		public static Order AreaOrder(Player p, Actor hub, CPos from, CPos to, bool add) =>
			new(ExtractorArea, p.PlayerActor, Target.FromCell(p.World, to), false)
			{
				ExtraLocation = from,
				ExtraData = hub.ActorID,
				TargetString = add ? "add" : "remove",
			};

		public static Order SetProductOrder(Player p, Actor hub, string product) =>
			new(ExtractorSetProduct, p.PlayerActor, false) { ExtraData = hub.ActorID, TargetString = product };

		public static Order SetPolicyOrder(Player p, Actor hub, string policy, bool on) =>
			new(ExtractorSetPolicy, p.PlayerActor, false) { ExtraData = hub.ActorID, TargetString = policy, ExtraLocation = new CPos(on ? 1 : 0, 0) };
	}

	/// <summary>Integer hashing helpers for deterministic noise and sampling (no System.Random, no floats).</summary>
	public static class IndustryHash
	{
		public static int Mix(int a, int b = 0, int c = 0)
		{
			unchecked
			{
				var h = (uint)(a * 73856093 ^ b * 19349663 ^ c * 83492791);
				h ^= h >> 13;
				h *= 0x5bd1e995;
				h ^= h >> 15;
				return (int)(h & 0x7fffffff);
			}
		}

		public static int Mix(CPos cell, int salt) => Mix(cell.X, cell.Y, salt);

		public static int HashString(string s)
		{
			unchecked
			{
				var h = 17;
				if (s != null)
					for (var i = 0; i < s.Length; i++)
						h = h * 31 + s[i];

				return h & 0x7fffffff;
			}
		}
	}

	/// <summary>
	/// Freight events for the economy (design/06 3.5). Implemented by the Logistics player trait; ECO subscribes to settle stock and money.
	/// Proposed for CityInterfaces.cs (see the WP report).
	/// </summary>
	public interface ILogisticsEvents
	{
		/// <summary>A shipment reached its buyer: ECO adds `Units` to the buyer's input stock.</summary>
		event Action<ShipmentEvent> Delivered;

		/// <summary>A shipment could not be delivered and went back: ECO returns the units to the seller's outbox and refunds the buyer.</summary>
		event Action<ShipmentEvent> Returned;
	}

	public struct ShipmentEvent
	{
		public int ShipmentId;
		public int FromProperty;      // 0 = outside connection
		public int ToProperty;        // 0 = outside connection
		public int Resource;
		public int Units;
		public int UnitPriceCents;
		public int FreightCents;      // total for the load

		/// <summary>The buyer is a warehouse or cargo terminal: Logistics already put the goods into its stock.</summary>
		public bool ToStorage;

		/// <summary>The seller was a warehouse or cargo terminal (its stock is restored on a return).</summary>
		public bool FromStorage;
	}

	public enum FreightMode : byte { Truck, Rail, Ship, Air }

	/// <summary>
	/// Actor trait of a cargo terminal building (rail yard, cargo harbor, airport). Owner of the interface: IND (Logistics discovers terminals
	/// through it); implemented by PT's buildings. The building must also be a CityBuilding so it has a property id and an access road.
	/// A terminal is a warehouse (StorageUnitsPerResource per resource type) with its own outside link (TradeUnitsPerDay).
	/// </summary>
	public interface ICargoTerminal
	{
		FreightMode Mode { get; }

		/// <summary>Units per clock day the terminal imports and exports (rail 400, harbor 900, air 80).</summary>
		int TradeUnitsPerDay { get; }

		/// <summary>Storage per resource type (design: 600).</summary>
		int StorageUnitsPerResource { get; }
	}

	/// <summary>Monthly statistic series kept by Logistics (units), for the production panel.</summary>
	public enum IndustryStat : byte { Produced, Dispatched, Delivered, Imported, Exported }

	/// <summary>Per-cell freight congestion hook. TRF's traffic service may implement it to add freight to GetTrafficLoad (proposed).</summary>
	public interface IFreightFlow
	{
		/// <summary>Adds `vehicleEquivalents` (heavy trucks count 2.5 cars) of load to every road cell of a path.</summary>
		void AddFlow(System.Collections.Generic.IReadOnlyList<CPos> pathCells, int vehicleEquivalents);
	}
}
