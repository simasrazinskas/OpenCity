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

// Read-only provider interfaces that the UI work package needs but that CityInterfaces.cs does not (yet) contain.
// A simulation trait (world or player trait) implements them, the UI finds them with
// TraitsImplementing<IFoo>().FirstOrDefault() and hides the matching panel when nothing implements them.
// They are the "interface requests" of the UI WP: the lead either keeps this file or moves the interfaces into
// CityInterfaces.cs (names stay the same), and the owning WP adds the interface to its existing trait.
namespace OpenRA.Mods.City.Traits
{
	/// <summary>Read side of the chirper feed. Owner: ENV. Newest entry last.</summary>
	public interface IChirperSource
	{
		/// <summary>Bumped on every new entry (the panel redraws only then).</summary>
		int Version { get; }
		IReadOnlyList<ChirpEntry> Entries { get; }
	}

	/// <summary>Production and trade numbers of one resource for the production panel.</summary>
	public struct ResourceStat
	{
		public int Produced, Consumed, Imported, Exported;

		/// <summary>Units in stock across the city.</summary>
		public int Stock;

		/// <summary>Imported units are paid with this many cents per unit (0 = unknown).</summary>
		public int PriceCents;
	}

	/// <summary>Read side of fees, loan, per-detail taxes and trade statistics. Owner: ECO (CityEconomy).</summary>
	public interface IEconomyUiSource
	{
		/// <summary>Percent of the default fee (50..200). Keys: "power", "water", "garbage", "health", "education", "parking".</summary>
		int GetFee(string key);

		/// <summary>Detailed tax percent (-10..30). kind 0 = zone category (ZoneCategory as index), 1 = education level, 2 = resource id.</summary>
		int GetTaxDetail(int kind, int index);

		int LoanPrincipal { get; }

		/// <summary>Maximum loan the current milestone allows.</summary>
		int LoanLimit { get; }

		/// <summary>Interest in tenths of a percent per month.</summary>
		int LoanInterestTenthsPercent { get; }

		/// <summary>Monthly statistics of a resource (id 1..ICityEconomy.ResourceCount).</summary>
		ResourceStat GetResourceStat(int resourceId);
	}

	public struct PolicyEntry
	{
		/// <summary>Policy id used in the CitySetPolicy order (TargetString).</summary>
		public string Id;
		public string NameKey;
		public string DescKey;

		/// <summary>True: a city policy, false: applies per district.</summary>
		public bool CityScope;
		public bool Unlocked;
		public bool Active;

		/// <summary>Slider range, SliderMax = 0 when the policy is a plain toggle.</summary>
		public int SliderMin, SliderMax, SliderValue;
		public int UpkeepPerMonth;
	}

	public struct DevNode
	{
		/// <summary>Node id used in the CityUnlockNode order (TargetString).</summary>
		public string Id;
		public string NameKey;
		public string DescKey;
		public string Tree;
		public int Tier;
		public int Cost;
		public bool Owned;

		/// <summary>All requirements fulfilled (the node can be bought when DevPoints allow it).</summary>
		public bool Available;

		/// <summary>Ids of the nodes this one requires.</summary>
		public string[] Requires;
	}

	public struct MilestoneEntry
	{
		public string Name;
		public int Xp;
		public bool Reached;
		public string Reward;
	}

	public struct DistrictEntry
	{
		public int Id;
		public string Name;
		public int Population, Households, Jobs, Happiness, LandValue;
		public int ArgbColor;
	}

	/// <summary>Read side of policies, development tree, milestones, districts and purchasable map tiles. Owner: PRG.</summary>
	public interface IProgressionUiSource
	{
		/// <summary>Policies of the city (districtId 0) or of a district.</summary>
		IReadOnlyList<PolicyEntry> Policies(int districtId);

		IReadOnlyList<DevNode> DevNodes { get; }
		IReadOnlyList<MilestoneEntry> Milestones { get; }

		/// <summary>Unspent map tile permits.</summary>
		int Permits { get; }

		/// <summary>Tiles per side of the purchasable-area grid (9).</summary>
		int TileGridSize { get; }

		/// <summary>Price of a tile, -1 when it cannot be bought now (owned or not adjacent).</summary>
		int TilePrice(int tileX, int tileY);

		bool IsTileOwned(int tileX, int tileY);

		/// <summary>The tile a cell belongs to (false outside the playable grid).</summary>
		bool TileAt(CPos cell, out int tileX, out int tileY);

		/// <summary>Cells of a tile as a rectangle (top-left inclusive, bottom-right inclusive).</summary>
		void TileBounds(int tileX, int tileY, out CPos topLeft, out CPos bottomRight);

		IReadOnlyList<DistrictEntry> Districts { get; }
	}

	public struct TransitLineEntry
	{
		public int Id;
		public string Name;
		public string Mode;
		public int ArgbColor;
		public int Stops, Vehicles;

		/// <summary>0..100 passenger load.</summary>
		public int Usage;
		public int PassengersThisMonth, RevenueMonth, CostMonth, WaitingNow;

		/// <summary>Ticket price in cents, the highest allowed price and the wanted number of vehicles (the TransitSetLine order).</summary>
		public int TicketCents, MaxTicketCents, VehicleTarget;
	}

	/// <summary>Read side of the transit overview. Owner: PT.</summary>
	public interface ITransitUiSource
	{
		IReadOnlyList<TransitLineEntry> Lines { get; }

		/// <summary>Id of the stop on this road cell, 0 = none (the line tool picks stops by clicking them).</summary>
		int StopAt(CPos cell);
	}

	public struct UpgradeEntry
	{
		/// <summary>Fluent key of the upgrade name; the CityBuyUpgrade order carries the entry's index in the list.</summary>
		public string NameKey;
		public int Cost;
		public int UpkeepPerMonth;
		public bool Owned;
	}

	/// <summary>Read side of in-place building upgrades (ambulance bay, wing, filter ...). Owner: SVC.</summary>
	public interface IUpgradeSource
	{
		/// <summary>The upgrades of a building in the order of the CityBuyUpgrade index; empty when it has none.</summary>
		IReadOnlyList<UpgradeEntry> UpgradesOf(Actor building);
	}

	/// <summary>One row an inspection contributor adds to the building panel.</summary>
	public struct InspectionRow
	{
		public string Label;
		public string Value;

		/// <summary>0 neutral, 1 good (green), 2 warning (yellow), 3 bad (red).</summary>
		public int Tone;

		/// <summary>0..100 draws a bar under the row, -1 none.</summary>
		public int BarPercent;
	}

	/// <summary>
	/// Any simulation trait (world or player trait) may add rows to the building inspection panel, e.g. ECO company
	/// details, IND hub data, SVC capacity and fleet, ZON condition. Called from UI code: read only, no allocations per call beyond the rows.
	/// </summary>
	public interface IInspectionContributor
	{
		void Contribute(Actor building, Property property, List<InspectionRow> rows);
	}

	/// <summary>
	/// Cell values for info views the UI cannot compute from CityInterfaces.cs alone (NaturalResources, Production, Freight,
	/// Transit, Tourism ...). Owner: the WP of that view. The info view panel lists a view only when some source Supports it.
	/// </summary>
	public interface IInfoViewSource
	{
		bool Supports(CityInfoView mode);

		/// <summary>Bumped when values change (the layer refreshes only then or on a timer).</summary>
		int Version { get; }

		/// <summary>0..100 (or a category index for categorical views) at a cell, -1 = draw nothing there.</summary>
		int GetCell(CityInfoView mode, CPos cell);
	}

	public struct AchievementEntry
	{
		public string Id;
		public string NameKey;
		public string DescKey;
		public bool Unlocked;

		/// <summary>0..100 progress towards the first unmet condition (100 once unlocked).</summary>
		public int Progress;

		/// <summary>Months in a row the conditions held, and how many are required (0 = instant).</summary>
		public int Streak, Months;
		public int Xp;
	}

	/// <summary>Read side of achievements. Owner: PRG (UI adapter over Progression.Achievements).</summary>
	public interface IAchievementSource
	{
		IReadOnlyList<AchievementEntry> Entries { get; }
		int Unlocked { get; }
	}

	/// <summary>Everything the map tile panel shows about one tile.</summary>
	public struct TileDetail
	{
		public bool Valid, Owned, Adjacent, HasOutsideConnection;

		/// <summary>Price now, -1 when owned.</summary>
		public int Price;

		/// <summary>Fluent key of why the tile cannot be bought now, null when it can.</summary>
		public string BlockedKey;
		public int TotalCells, BuildableCells;

		/// <summary>Monthly upkeep added by buying it.</summary>
		public int UpkeepIncrease;

		/// <summary>Remaining resource amount per NaturalResourceKind index.</summary>
		public int[] Resources;
	}

	/// <summary>Read side of the purchasable map tiles beyond IProgressionUiSource. Owner: PRG (UI adapter over Progression.GetTileInfo).</summary>
	public interface ITileInfoSource
	{
		TileDetail Detail(int tileX, int tileY);

		int OwnedTiles { get; }
		int TotalTiles { get; }
		int MonthlyUpkeep { get; }
	}

	/// <summary>
	/// Camera follow: a sim trait that knows where a citizen or vehicle currently is (TRF/CIT) implements it; the UI then keeps the
	/// camera on the followed citizen. Until then following falls back to the citizen's home or workplace.
	/// </summary>
	public interface IFollowSource
	{
		bool TryGetCitizenPosition(int citizenId, out WPos position);

		bool TryGetVehiclePosition(int vehicleId, out WPos position);
	}
}
