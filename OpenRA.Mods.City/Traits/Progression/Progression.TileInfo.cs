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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Everything the tile purchase UI needs about one tile (read-only snapshot).</summary>
	public struct TileInfoData
	{
		public int X, Y;
		public bool Owned;

		/// <summary>Adjacent to an owned tile, or the tile holds an outside connection.</summary>
		public bool Adjacent;

		public bool HasOutsideConnection;

		/// <summary>Price of buying it now (-1 when owned).</summary>
		public int Price;

		/// <summary>Why it cannot be bought now (Ok = it can).</summary>
		public TileBuyResult Status;

		public int TotalCells, BuildableCells;

		/// <summary>Monthly tile upkeep after buying this tile minus the current upkeep.</summary>
		public int UpkeepIncrease;

		/// <summary>Remaining natural resource amount per NaturalResourceKind index (Fertile, Forest, Ore, Oil, Stone, Fish), 0 without a resource layer.</summary>
		public int[] Resources;
	}

	// Tile purchase UX data: prices, adjacency, upkeep preview and natural resources per tile.
	public partial class Progression
	{
		NaturalResourceLayer naturalResources;
		bool naturalResourcesResolved;

		/// <summary>Number of natural resource kinds in TileInfoData.Resources.</summary>
		public static int ResourceKindCount => Enum.GetValues<NaturalResourceKind>().Length;

		public TileInfoData GetTileInfo(int tx, int ty)
		{
			var g = Info.TileGrid;
			var data = new TileInfoData { X = tx, Y = ty, Price = -1, Status = TileBuyResult.Invalid, Resources = new int[ResourceKindCount] };
			if (!tilesInit || tx < 0 || ty < 0 || tx >= g || ty >= g)
				return data;

			var ti = ty * g + tx;
			var rect = GetTileRect(tx, ty);
			data.Owned = tileOwned[ti];
			data.HasOutsideConnection = tileHasConnection[ti];
			data.Adjacent = TileAdjacentToOwned(tx, ty) || data.HasOutsideConnection;
			data.TotalCells = rect.Width * rect.Height;
			data.BuildableCells = tileBuildable[ti];
			data.Status = CheckBuyTile(tx, ty);
			if (data.Owned)
				return data;

			data.Price = GetTilePrice(tx, ty);
			data.UpkeepIncrease = UpkeepFor(TilesPurchased + 1, PaidTotal() + data.Price) - MonthlyTileUpkeep;
			FillResources(rect, data.Resources);
			return data;
		}

		long PaidTotal()
		{
			long sum = 0;
			for (var i = 0; i < tilePaid.Length; i++)
				sum += tilePaid[i];

			return sum;
		}

		int UpkeepFor(int purchased, long paid)
		{
			var span = Math.Max(1, TileCount - startTiles);
			var pct = Info.TileUpkeepMinPct + Info.TileUpkeepRangePct * purchased / span;
			return (int)(paid * pct / 100 / Math.Max(1, Info.TileUpkeepDivisor));
		}

		void FillResources(OpenRA.Primitives.Rectangle rect, int[] into)
		{
			if (!naturalResourcesResolved)
			{
				naturalResourcesResolved = true;
				naturalResources = world.WorldActor.TraitOrDefault<NaturalResourceLayer>();
			}

			if (naturalResources == null)
				return;

			var kinds = Enum.GetValues<NaturalResourceKind>();
			for (var y = rect.Top; y < rect.Bottom; y++)
			{
				for (var x = rect.Left; x < rect.Right; x++)
				{
					var cell = new CPos(x, y);
					if (!world.Map.Contains(cell))
						continue;

					for (var k = 0; k < kinds.Length; k++)
						into[k] += naturalResources.GetAmount(kinds[k], cell);
				}
			}
		}
	}
}
