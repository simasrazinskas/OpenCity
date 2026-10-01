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
	// Economy inputs to demand (design 05 section 3.8) and the budget projection. All results are integers
	// (bar points / dollars). The demand model (PRG) may read the same getters.
	public partial class CityEconomy
	{
		readonly int[] vacant = new int[5];

		void CountVacancy()
		{
			Array.Clear(vacant);
			var all = registry.All;
			for (var i = 0; i < all.Count; i++)
			{
				var p = all[i];
				if (p.CompanyId != 0 || !p.Operational || !Mine(p))
					continue;

				switch (p.Kind)
				{
					case PropertyKind.Commercial: vacant[(int)ZoneCategory.Commercial]++; break;
					case PropertyKind.Industrial: vacant[(int)ZoneCategory.Industrial]++; break;
					case PropertyKind.Office: vacant[(int)ZoneCategory.Office]++; break;
				}
			}
		}

		/// <summary>Operational buildings of a category that have no company (yet).</summary>
		public int VacantSlots(ZoneCategory category) { return vacant[(int)category]; }

		/// <summary>-30..50: share of household needs for retail goods and services that nobody served.</summary>
		public int RetailPressure()
		{
			long need = 0;
			long unmet = 0;
			for (var k = 0; k < saleList.Count; k++)
			{
				var r = Tables.Resources[saleList[k]];
				if (r.Recipe >= 0 && Tables.Recipes[r.Recipe].Zone == ZoneCategory.Office)
					continue;

				need += (long)emaNeed[r.Id] * r.RetailCents;
				unmet += (long)emaUnmet[r.Id] * r.RetailCents;
			}

			if (need <= 0)
				return 0;

			var pct = (int)(unmet * 100 / (need + 1));
			return Math.Clamp((pct - 8) * 2, -30, 50);
		}

		/// <summary>-30..50: how much of the industrial goods the city uses is imported.</summary>
		public int IndustrialPressure() { return ImportShare(ZoneCategory.Industrial, false); }

		/// <summary>-30..50: unmet office services (finance, media) plus imported software.</summary>
		public int OfficePressure() { return ImportShare(ZoneCategory.Office, true); }

		int ImportShare(ZoneCategory zone, bool includeUnmet)
		{
			long imported = 0;
			long produced = 0;
			for (var r = 1; r <= Tables.ResourceCount; r++)
			{
				var def = Tables.Resources[r];
				if (def.Recipe < 0 || Tables.Recipes[def.Recipe].Zone != zone)
					continue;

				imported += (long)(emaImported[r] + (includeUnmet ? emaUnmet[r] : 0)) * def.PriceCents;
				produced += (long)emaProduced[r] * def.PriceCents;
			}

			var share = (int)(imported * 100 / (imported + produced + 1));
			return Math.Clamp(share - 15, -30, 50);
		}

		/// <summary>Monthly city income in dollars: this month's run rate (last month's total early in the month).</summary>
		public int ProjectedMonthlyIncome()
		{
			long now = 0;
			long last = 0;
			for (var c = LFeePower; c <= LTaxOffice; c++)
			{
				now += flowMonth[c];
				last += flowLast[c];
			}

			now += flowMonth[LFeeParking] + flowMonth[LTourismTax];
			last += flowLast[LFeeParking] + flowLast[LTourismTax];
			var elapsed = clock != null ? clock.TickOfDay : ticksPerMonth / 2;
			var projected = elapsed >= ticksPerMonth / 6 ? now * ticksPerMonth / Math.Max(1, elapsed) : Math.Max(last, now);
			return (int)Math.Min(int.MaxValue / 2, projected / 100);
		}
	}
}
