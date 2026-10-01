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

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>Achievements and tile purchase details of the progression WP for the UI panels.</summary>
	public sealed class ProgressionExtrasAdapter : IAchievementSource, ITileInfoSource
	{
		readonly Progression progression;
		readonly World world;
		readonly List<AchievementEntry> entries = [];
		int entriesTick = -1;

		public ProgressionExtrasAdapter(World world, Progression progression)
		{
			this.world = world;
			this.progression = progression;
		}

		public IReadOnlyList<AchievementEntry> Entries
		{
			get
			{
				if (entriesTick == world.WorldTick)
					return entries;

				entriesTick = world.WorldTick;
				entries.Clear();
				foreach (var a in progression.Achievements)
				{
					entries.Add(new AchievementEntry
					{
						Id = a.Id,
						NameKey = a.NameKey,
						DescKey = a.DescKey,
						Unlocked = a.Unlocked,
						Progress = a.Progress,
						Streak = a.Streak,
						Months = a.Months,
						Xp = a.Xp
					});
				}

				return entries;
			}
		}

		public int Unlocked => progression.UnlockedAchievementCount;

		public TileDetail Detail(int tileX, int tileY)
		{
			var info = progression.GetTileInfo(tileX, tileY);
			string blocked = null;
			switch (info.Status)
			{
				case TileBuyResult.NoPermit: blocked = "label-tiles-no-permit"; break;
				case TileBuyResult.NotAdjacent: blocked = "label-tiles-unavailable"; break;
				case TileBuyResult.NoFunds: blocked = "label-tiles-no-funds"; break;
				case TileBuyResult.Invalid: blocked = "label-tiles-invalid"; break;
			}

			return new TileDetail
			{
				Valid = info.Status != TileBuyResult.Invalid,
				Owned = info.Owned,
				Adjacent = info.Adjacent,
				HasOutsideConnection = info.HasOutsideConnection,
				Price = info.Price,
				BlockedKey = blocked,
				TotalCells = info.TotalCells,
				BuildableCells = info.BuildableCells,
				UpkeepIncrease = info.UpkeepIncrease,
				Resources = info.Resources
			};
		}

		public int OwnedTiles => progression.OwnedTileCount;
		public int TotalTiles => progression.TileCount;
		public int MonthlyUpkeep => progression.MonthlyTileUpkeep;
	}
}
