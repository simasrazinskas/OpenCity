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
	// Life events for the chirper, commute effects and weather-aware leisure.
	public partial class CitizenSim
	{
		int chirpsToday;
		int commuteQuits;

		/// <summary>Posts a life-event chirp (key + "-1".."-variants", argument = the citizen's name), within the daily budget.</summary>
		void Announce(string key, int ci, int variants, int percent = 100)
		{
			if (stats == null || chirpsToday >= info.ChirpsPerDay || (percent < 100 && Hash(ci, Today, 120) % 100 >= percent))
				return;

			chirpsToday++;
			var name = NameOf(ci, cits[ci].BirthDay, (cits[ci].Flags & CitFlags.Male) != 0);
			var v = 1 + Hash(ci, Today, 121) % Math.Max(1, variants);
			stats.Chirp(key + "-" + v, ci + 1, name);
		}

		/// <summary>Happiness penalty (0..10) of the worker's commute.</summary>
		int CommutePenalty(int ci)
		{
			var commute = cits[ci].Commute;
			if (commute <= info.CommuteOkTicks)
				return 0;

			return Math.Min(10, (commute - info.CommuteOkTicks) / Math.Max(1, info.CommutePenaltyDivisor));
		}

		void RecordCommute(int ci, int ticks)
		{
			var c = cits[ci].Commute;
			var v = c == 0 ? ticks : (c * 3 + ticks) / 4;
			cits[ci].Commute = (ushort)Math.Clamp(v, 1, ushort.MaxValue);
		}

		/// <summary>Daily check of a worker: a commute that stays too long makes them look for a nearer job (and a nearer home).</summary>
		void CheckCommute(int ci, int hh)
		{
			if (cits[ci].Commute <= info.CommuteQuitTicks)
			{
				cits[ci].CommuteStrikes = 0;
				return;
			}

			cits[ci].CommuteStrikes++;
			if (cits[ci].CommuteStrikes < info.CommuteStrikesToQuit)
				return;

			cits[ci].CommuteStrikes = 0;
			var home = registry.Get(hhs[hh].Home);
			var old = cits[ci].Work;
			var level = cits[ci].JobLevel;
			var p = home != null ? FindJob(level, home) : null;
			if (p != null && p.Id != old && ManhattanRoad(home, p) < ManhattanRoad(home, registry.Get(old) ?? p))
			{
				ReleaseWork(ci);
				TakeJob(ci, p, level);
				cits[ci].Commute = 0;
				commuteQuits++;
			}
			else
				hhs[hh].Flags |= HhFlags.WantsMove;
		}

		/// <summary>
		/// Leisure destination: park or commercial lot. In rain, snow or storms and in the cold, outdoor parks are avoided and indoor
		/// venues preferred; storms keep everybody at home.
		/// </summary>
		Property PickLeisureDestination(Property home)
		{
			var n = leisureCands.Count;
			if (n == 0)
				return null;

			var bad = false;
			if (climate != null)
			{
				if (climate.Weather == CityWeather.Storm)
					return null;

				bad = climate.Weather == CityWeather.Rain || climate.Weather == CityWeather.Snow || climate.TemperatureX10 < 20;
			}

			Property best = null;
			var bestScore = int.MinValue;
			for (var s = 0; s < 4; s++)
			{
				var p = leisureCands[NextRandom(n)];
				var park = p.Kind != PropertyKind.Commercial;
				var score = (park ? (bad ? -60 : 30) : 10) - (home != null ? ManhattanRoad(home, p) : 0);
				if (score > bestScore)
				{
					bestScore = score;
					best = p;
				}
			}

			return best;
		}

		/// <summary>A resident pays for a leisure visit at a commercial property (via the economy's shop sales, else free).</summary>
		void SpendLeisure(int hh, int shopId)
		{
			if (info.LeisureSpendCents <= 0 || hhs[hh].Cash <= info.LeisureSpendCents)
				return;

			var shop = registry.Get(shopId);
			if (shop == null || shop.Kind != PropertyKind.Commercial)
				return;

			var charged = info.LeisureSpendCents;
			if (economy != null && economy.ConsumerResources.Count > 0)
			{
				var res = economy.ConsumerResources[Hash(hh, Today, 122) % economy.ConsumerResources.Count];
				economy.SellToHousehold(shopId, res, 1000, info.LeisureSpendCents, out charged);
			}

			hhs[hh].Cash -= charged;
			spentCents += charged;
		}
	}
}
