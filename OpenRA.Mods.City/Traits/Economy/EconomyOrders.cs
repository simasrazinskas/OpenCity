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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Kind of tax a CitySetTaxDetail order changes (ExtraLocation.X).</summary>
	public enum TaxDetailKind { Category = 0, Education = 1, Resource = 2 }

	/// <summary>Fees whose price the player sets (percent of the default).</summary>
	public enum FeeKind { Power = 0, Water = 1, Garbage = 2 }

	/// <summary>
	/// Economy orders (resolved by the CityEconomy player trait). Every order's subject is the issuing player's PlayerActor.
	/// UI code only calls the factories below.
	/// </summary>
	public static class EconomyOrders
	{
		/// <summary>ExtraLocation = (TaxDetailKind, index), ExtraData = rate percent + 10 (so -10..30 maps to 0..40).</summary>
		public const string SetTaxDetail = "CitySetTaxDetail";

		/// <summary>ExtraLocation.X = FeeKind, ExtraData = percent of the default price (50..200).</summary>
		public const string SetFee = "CitySetFee";

		/// <summary>ExtraLocation.X = ServiceKind, ExtraData = budget percent (50..150).</summary>
		public const string SetServiceBudget = "CitySetServiceBudget";

		/// <summary>ExtraData = wanted loan principal in dollars (clamped to the milestone limit; lower values repay).</summary>
		public const string SetLoan = "CitySetLoan";

		/// <summary>Marks a tax rate that follows its zone category rate (no override).</summary>
		public const int UnsetRate = -100;

		/// <summary>Sets the income tax rate of one education level. Pass percent -10..30.</summary>
		public static Order SetEducationTaxOrder(Player p, EducationLevel edu, int percent) =>
			new(SetTaxDetail, p.PlayerActor, false)
			{
				ExtraLocation = new CPos((int)TaxDetailKind.Education, (int)edu),
				ExtraData = (uint)(EconomyMath.ClampTax(percent) + 10),
			};

		/// <summary>Sets the profit tax rate of one resource (companies producing it). Pass percent -10..30.</summary>
		public static Order SetResourceTaxOrder(Player p, int resourceId, int percent) =>
			new(SetTaxDetail, p.PlayerActor, false)
			{
				ExtraLocation = new CPos((int)TaxDetailKind.Resource, resourceId),
				ExtraData = (uint)(EconomyMath.ClampTax(percent) + 10),
			};

		/// <summary>Sets the base rate of a zone category (-10..30); resets per-education / per-resource overrides of that category.</summary>
		public static Order SetCategoryTaxOrder(Player p, ZoneCategory category, int percent) =>
			new(SetTaxDetail, p.PlayerActor, false)
			{
				ExtraLocation = new CPos((int)TaxDetailKind.Category, (int)category),
				ExtraData = (uint)(EconomyMath.ClampTax(percent) + 10),
			};

		public static Order SetFeeOrder(Player p, FeeKind fee, int percent) =>
			new(SetFee, p.PlayerActor, false) { ExtraLocation = new CPos((int)fee, 0), ExtraData = (uint)percent };

		public static Order SetServiceBudgetOrder(Player p, ServiceKind kind, int percent) =>
			new(SetServiceBudget, p.PlayerActor, false) { ExtraLocation = new CPos((int)kind, 0), ExtraData = (uint)percent };

		public static Order SetLoanOrder(Player p, int principalDollars) =>
			new(SetLoan, p.PlayerActor, false) { ExtraData = (uint)System.Math.Max(0, principalDollars) };
	}
}
