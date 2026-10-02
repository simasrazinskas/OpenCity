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
using OpenRA.Primitives;

namespace OpenRA.Mods.City.Traits
{
	public enum TileBuyResult : byte { Ok, NoPermit, AlreadyOwned, NotAdjacent, NoFunds, Invalid }

	// Map tiles: the playable bounds are split into a TileGrid x TileGrid grid (9x9). Only owned tiles are buildable.
	public partial class Progression
	{
		bool tilesInit;
		int tileLeft, tileTop, tileRight, tileBottom, tileW, tileH;
		bool[] tileOwned = [];
		bool[] tileHasConnection = [];
		int[] tilePaid = [];
		int[] tileBuildable = [];
		int startTiles;

		public int TileGrid => Info.TileGrid;

		public int TileCount => Info.TileGrid * Info.TileGrid;

		public int OwnedTileCount
		{
			get
			{
				var n = 0;
				for (var i = 0; i < tileOwned.Length; i++)
					if (tileOwned[i])
						n++;

				return n;
			}
		}

		public int TilesPurchased { get; private set; }

		void InitTiles()
		{
			var g = Info.TileGrid;
			var b = world.Map.Bounds;
			tileLeft = b.Left;
			tileTop = b.Top;
			tileRight = b.Right;
			tileBottom = b.Bottom;
			tileW = Math.Max(1, (tileRight - tileLeft) / g);
			tileH = Math.Max(1, (tileBottom - tileTop) / g);
			tileOwned = new bool[g * g];
			tileHasConnection = new bool[g * g];
			tilePaid = new int[g * g];
			tileBuildable = new int[g * g];

			// Buildable (non-water) cells per tile for the price formula.
			for (var y = tileTop; y < tileBottom; y++)
			{
				for (var x = tileLeft; x < tileRight; x++)
				{
					var cell = new CPos(x, y);
					if (!world.Map.Contains(cell))
						continue;

					var ti = TileIndexAt(cell);
					if (ti >= 0 && world.Map.GetTerrainInfo(cell).Type != "Water")
						tileBuildable[ti]++;
				}
			}

			// Highway entries and the tile block around the first one's inner end (lowest actor id) are owned from the start.
			Actor first = null;
			foreach (var a in world.ActorsHavingTrait<OutsideConnection>())
			{
				var ti = TileIndexAt(a.Location);
				if (ti >= 0)
					tileHasConnection[ti] = true;

				if (first == null || a.ActorID < first.ActorID)
					first = a;
			}

			int cx = g / 2, cy = g / 2;
			if (first != null)
			{
				var oc = first.Trait<OutsideConnection>();
				var end = new CPos(first.Location.X + oc.Info.Direction.X * (oc.Info.Length - 1), first.Location.Y + oc.Info.Direction.Y * (oc.Info.Length - 1));
				TileOf(end, out cx, out cy);
			}

			var block = Math.Clamp(Info.StartTileBlock, 1, g);
			var x0 = Math.Clamp(cx - block / 2, 0, g - block);
			var y0 = Math.Clamp(cy - block / 2, 0, g - block);
			for (var y = y0; y < y0 + block; y++)
				for (var x = x0; x < x0 + block; x++)
					tileOwned[y * g + x] = true;

			if (sandbox)
				Array.Fill(tileOwned, true);

			startTiles = sandbox ? g * g : block * block;
			tilesInit = true;
		}

		/// <summary>Tile coordinates of a cell, clamped into the grid (cells outside the bounds map to the nearest tile).</summary>
		void TileOf(CPos cell, out int tx, out int ty)
		{
			var g = Info.TileGrid;
			tx = Math.Clamp((cell.X - tileLeft) / tileW, 0, g - 1);
			ty = Math.Clamp((cell.Y - tileTop) / tileH, 0, g - 1);
		}

		/// <summary>Tile index (ty * grid + tx) of a cell, or -1 outside the playable bounds.</summary>
		public int TileIndexAt(CPos cell)
		{
			if (cell.X < tileLeft || cell.X >= tileRight || cell.Y < tileTop || cell.Y >= tileBottom)
				return -1;

			TileOf(cell, out var tx, out var ty);
			return ty * Info.TileGrid + tx;
		}

		/// <summary>Buildable-area gate: roads, zoning, placement and growth must reject cells of unowned tiles.</summary>
		public bool IsCellOwned(CPos cell)
		{
			if (!tilesInit)
				return true;

			var ti = TileIndexAt(cell);
			return ti >= 0 && tileOwned[ti];
		}

		public bool IsTileOwned(int tx, int ty)
		{
			return tilesInit && tx >= 0 && ty >= 0 && tx < Info.TileGrid && ty < Info.TileGrid && tileOwned[ty * Info.TileGrid + tx];
		}

		/// <summary>Cell rectangle (x, y, width, height) of a tile.</summary>
		public Rectangle GetTileRect(int tx, int ty)
		{
			var g = Info.TileGrid;
			var x0 = tileLeft + tx * tileW;
			var y0 = tileTop + ty * tileH;
			var x1 = tx == g - 1 ? tileRight : x0 + tileW;
			var y1 = ty == g - 1 ? tileBottom : y0 + tileH;
			return new Rectangle(x0, y0, x1 - x0, y1 - y0);
		}

		public int GetTileBuildableCells(int tx, int ty) { return tilesInit ? tileBuildable[ty * Info.TileGrid + tx] : 0; }

		/// <summary>Price of the next purchase of this tile: (base + perCell * buildable) * (100 + step * purchased) / 100.</summary>
		public int GetTilePrice(int tx, int ty)
		{
			if (!tilesInit)
				return 0;

			return TilePriceFor(tileBuildable[ty * Info.TileGrid + tx], TilesPurchased);
		}

		/// <summary>Pure price formula (table-testable).</summary>
		public int TilePriceFor(int buildableCells, int purchased)
		{
			return (int)((long)(Info.TileBasePrice + Info.TilePricePerCell * buildableCells) * (100 + Info.TilePriceStepPercent * purchased) / 100);
		}

		bool TileAdjacentToOwned(int tx, int ty)
		{
			return IsTileOwned(tx - 1, ty) || IsTileOwned(tx + 1, ty) || IsTileOwned(tx, ty - 1) || IsTileOwned(tx, ty + 1);
		}

		public TileBuyResult CheckBuyTile(int tx, int ty)
		{
			var g = Info.TileGrid;
			if (!tilesInit || tx < 0 || ty < 0 || tx >= g || ty >= g)
				return TileBuyResult.Invalid;

			if (tileOwned[ty * g + tx])
				return TileBuyResult.AlreadyOwned;

			if (Permits <= 0)
				return TileBuyResult.NoPermit;

			if (!TileAdjacentToOwned(tx, ty) && !tileHasConnection[ty * g + tx])
				return TileBuyResult.NotAdjacent;

			if (manager != null && !manager.CanAfford(GetTilePrice(tx, ty)))
				return TileBuyResult.NoFunds;

			return TileBuyResult.Ok;
		}

		/// <summary>Order handler: buys a tile if allowed (permit, adjacency or highway, funds). Nothing changes otherwise.</summary>
		public TileBuyResult BuyTile(int tx, int ty)
		{
			var result = CheckBuyTile(tx, ty);
			if (result != TileBuyResult.Ok)
				return result;

			var g = Info.TileGrid;
			var price = GetTilePrice(tx, ty);
			if (!Spend(price, "tiles"))
				return TileBuyResult.NoFunds;

			Permits--;
			tileOwned[ty * g + tx] = true;
			tilePaid[ty * g + tx] = price;
			TilesPurchased++;
			Notify("notification-prg-tile", $"{tx + 1},{ty + 1}", "price", CityUtils.FormatMoney(price));
			return TileBuyResult.Ok;
		}

		/// <summary>Monthly upkeep of all purchased tiles: price paid * pct / 100 / divisor, pct rising from 5% to 25% with the number purchased.</summary>
		public int MonthlyTileUpkeep
		{
			get
			{
				var span = Math.Max(1, TileCount - startTiles);
				var pct = Info.TileUpkeepMinPct + Info.TileUpkeepRangePct * TilesPurchased / span;
				long sum = 0;
				for (var i = 0; i < tilePaid.Length; i++)
					sum += tilePaid[i];

				return (int)(sum * pct / 100 / Math.Max(1, Info.TileUpkeepDivisor));
			}
		}

		/// <summary>Bounding cell rectangle of all owned tiles as "left,top-right,bottom" (inclusive), for logs.</summary>
		public string OwnedBounds()
		{
			int l = int.MaxValue, t = int.MaxValue, r = int.MinValue, b = int.MinValue;
			var g = Info.TileGrid;
			for (var i = 0; i < tileOwned.Length; i++)
			{
				if (!tileOwned[i])
					continue;

				var rect = GetTileRect(i % g, i / g);
				l = Math.Min(l, rect.Left);
				t = Math.Min(t, rect.Top);
				r = Math.Max(r, rect.Right - 1);
				b = Math.Max(b, rect.Bottom - 1);
			}

			return tileOwned.Length == 0 || l == int.MaxValue ? "none" : $"{l},{t}-{r},{b}";
		}

		int TileHash()
		{
			unchecked
			{
				var h = TilesPurchased;
				for (var i = 0; i < tileOwned.Length; i++)
					if (tileOwned[i])
						h = h * 31 + i + 1;

				return h;
			}
		}
	}
}
