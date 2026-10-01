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
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// The UI's single entry point for issuing other work packages' orders. Every method calls the factory of the owning WP
	/// (NetworkOrders, EconomyOrders, ServiceOrders, IndustryOrders, TransitOrders, ProgressionOrders), so the payloads can
	/// only ever match what the simulation resolves. UI code never builds an order by hand.
	/// </summary>
	public static class UiOrders
	{
		/// <summary>Fees the economy lets the player change, in panel order.</summary>
		public static readonly string[] FeeKeys = ["power", "water", "garbage"];

		public static Order Fee(Player p, string key, int percent)
		{
			var fee = key == "water" ? FeeKind.Water : key == "garbage" ? FeeKind.Garbage : FeeKind.Power;
			return EconomyOrders.SetFeeOrder(p, fee, percent);
		}

		public static Order CategoryTax(Player p, ZoneCategory category, int percent) => EconomyOrders.SetCategoryTaxOrder(p, category, percent);

		public static Order EducationTax(Player p, EducationLevel level, int percent) => EconomyOrders.SetEducationTaxOrder(p, level, percent);

		public static Order ResourceTax(Player p, int resourceId, int percent) => EconomyOrders.SetResourceTaxOrder(p, resourceId, percent);

		/// <summary>Health and education fees of the services WP (the power, water and garbage fees are the economy's).</summary>
		public static Order ServiceFeeOrder(Player p, ServiceFee fee, int percent) => ServiceOrdersWave2.SetServiceFeeOrder(p, fee, percent);

		public static Order ServiceDistricts(Player p, Actor building, int mask) => ServiceOrdersWave2.SetDistrictsOrder(p, building, mask);

		public static Order Loan(Player p, int principal) => EconomyOrders.SetLoanOrder(p, principal);

		public static Order ServiceBudget(Player p, ServiceKind kind, int percent) => ServiceOrders.SetBudgetOrder(p, kind, percent);

		public static Order Upgrade(Player p, Actor building, int index) => ServiceOrders.BuyUpgradeOrder(p, building, index);

		public static Order HubArea(Player p, Actor hub, CPos from, CPos to, bool add) => IndustryOrders.AreaOrder(p, hub, from, to, add);

		public static Order HubProduct(Player p, Actor hub, string product) => IndustryOrders.SetProductOrder(p, hub, product);

		public static Order PlaceStop(Player p, CPos cell, TransitMode mode) => TransitOrders.PlaceStopOrder(p, cell, mode);

		public static Order CreateLine(Player p, TransitMode mode, bool loop, IEnumerable<int> stopIds, int color, int vehicles = 1) =>
			TransitOrders.CreateLineOrder(p, mode, loop, stopIds, color, "", vehicles);

		public static Order SetLine(Player p, int lineId, int ticketCents, int vehicles)
		{
			// The panel edits cents; the order carries a percent of the default fare.
			var defaultCents = p.World.WorldActor.TraitOrDefault<TransitLayer>()?.Info.DefaultTicketCents ?? 100;
			var percent = ticketCents < 0 ? -1 : ticketCents * 100 / System.Math.Max(1, defaultCents);
			return TransitOrders.SetLineOrder(p, lineId, percent, vehicles);
		}

		public static Order DeleteLine(Player p, int lineId) => TransitOrders.DeleteLineOrder(p, lineId);

		public static Order Policy(Player p, string policyId, int districtId, int value)
		{
			// The UI identifies policies by id; the order carries the catalogue index.
			var index = p.PlayerActor.TraitOrDefault<Progression>()?.FindPolicy(policyId) ?? -1;
			return ProgressionOrders.SetPolicyOrder(p, index, districtId, value);
		}

		public static Order Node(Player p, string nodeId) => ProgressionOrders.UnlockNodeOrder(p, nodeId);

		public static Order Tile(Player p, int tileX, int tileY) => ProgressionOrders.BuyTileOrder(p, tileX, tileY);

		/// <summary>districtId 0 erases, a negative id creates a new district for the painted area.</summary>
		public static Order DistrictPaint(Player p, CPos from, CPos to, int districtId) =>
			districtId < 0 ? ProgressionOrders.NewDistrictPaintOrder(p, from, to) : ProgressionOrders.DistrictPaintOrder(p, from, to, districtId);

		public static Order DistrictRename(Player p, int districtId, string name) => ProgressionOrders.DistrictNameOrder(p, districtId, name);

		public static Order DistrictRemove(Player p, int districtId) => ProgressionOrders.DistrictDeleteOrder(p, districtId);
	}
}
