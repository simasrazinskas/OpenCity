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
	// Job matching by education (any job at your level or lower), wages, income tax and shopping.
	public partial class CitizenSim
	{
		long taxPoolCents;
		long wagesPaidCents, benefitsPaidCents, rentPaidCents, spentCents;
		int rentToReport;
		int jobsFound, jobsLost, unmetShopping;

		/// <summary>Releases the job or school seat of a citizen (counters on the property and at the services provider).</summary>
		void ReleaseWork(int ci)
		{
			var work = cits[ci].Work;
			if (work != 0)
			{
				var p = registry.Get(work);
				if (p != null)
				{
					if ((cits[ci].Flags & CitFlags.Student) != 0)
					{
						if (p.Students > 0)
							p.Students--;

						services?.Unenroll(ci + 1, work);
					}
					else if ((cits[ci].Flags & CitFlags.Worker) != 0 && p.JobsFilled[cits[ci].JobLevel] > 0)
						p.JobsFilled[cits[ci].JobLevel]--;
				}
			}

			cits[ci].Work = 0;
			cits[ci].StudyDays = 0;
			cits[ci].Flags &= ~(CitFlags.Student | CitFlags.Worker);
			CancelActiveTrip(ci);
		}

		/// <summary>Drops a job whose building is gone or whose slot was removed by the employer.</summary>
		void CheckWork(int ci)
		{
			var c = cits[ci];
			if ((c.Flags & CitFlags.Worker) == 0 || c.Work == 0)
				return;

			var p = registry.Get(c.Work);
			if (p == null)
			{
				cits[ci].Work = 0;
				cits[ci].Flags &= ~CitFlags.Worker;
				jobsLost++;
			}
			else if (p.JobsFilled[c.JobLevel] - CommutersAt(p, c.JobLevel) > p.JobSlots[c.JobLevel])
			{
				ReleaseWork(ci);
				jobsLost++;
			}
		}

		Property FindJob(int level, Property home)
		{
			var list = jobCands[level];
			if (list.Count == 0)
				return null;

			Property best = null;
			var bestD = int.MaxValue;
			for (var s = 0; s < info.SearchSamples; s++)
			{
				var p = list[NextRandom(list.Count)];
				if (!Alive(p) || p.JobsFilled[level] - CommutersAt(p, level) >= p.JobSlots[level])
					continue;

				var d = home != null ? ManhattanRoad(home, p) : 0;
				if (d < bestD)
				{
					bestD = d;
					best = p;
				}
			}

			return best;
		}

		void UpdateWork(int ci, int age, int today)
		{
			var c = cits[ci];
			if ((c.Flags & (CitFlags.Student | CitFlags.Retired | CitFlags.Jailed)) != 0 || age < info.WorkAge || age > info.AdultMaxAge || c.Household < 0)
				return;

			var hh = c.Household;
			if ((c.Flags & CitFlags.Worker) != 0)
			{
				PayWage(ci, hh);
				CheckCommute(ci, hh);

				// Over-qualified workers sometimes look for a job at their own level.
				if (c.JobLevel < c.Education && Hash(ci, today, 40) % 100 < 20)
					TryBetterJob(ci, hh);

				return;
			}

			SeekJob(ci, age);
		}

		/// <summary>Job search of an unemployed adult: own education level first, lower levels after a few failed tries.</summary>
		bool SeekJob(int ci, int age)
		{
			var c = cits[ci];
			if ((c.Flags & (CitFlags.Student | CitFlags.Retired | CitFlags.Worker | CitFlags.Sick | CitFlags.Jailed)) != 0
				|| age < info.WorkAge || age > info.AdultMaxAge || c.Household < 0)
				return false;

			var home = registry.Get(hhs[c.Household].Home);
			if (home == null)
				return false;

			var lowest = c.UnemployedDays >= info.TriesBeforeLowerJobs ? Math.Max(0, c.Education - 2) : c.Education;
			for (int level = c.Education; level >= lowest; level--)
			{
				var p = FindJob(level, home);
				if (p != null)
				{
					TakeJob(ci, p, level);
					return true;
				}
			}

			if (cits[ci].UnemployedDays < 250)
				cits[ci].UnemployedDays++;

			return false;
		}

		int seekCursor, homeCursor;

		/// <summary>Round-robin scan (SearchScansPerDay full passes per day): unemployed citizens search for jobs and homeless households for homes.</summary>
		void ScanSeekers()
		{
			var pulsesPerDay = Math.Max(1, TicksPerDay / 25);
			var scans = Math.Max(1, info.SearchScansPerDay);
			var cn = citCount * scans / pulsesPerDay + 1;
			for (var k = 0; k < cn && citCount > 0; k++)
			{
				if (seekCursor >= citCount)
					seekCursor = 0;

				var ci = seekCursor++;
				var f = cits[ci].Flags;
				if ((f & (CitFlags.Alive | CitFlags.Worker | CitFlags.Student | CitFlags.Retired | CitFlags.Sick | CitFlags.Jailed)) == CitFlags.Alive)
					SeekJob(ci, AgeOf(ci));
			}

			var hn = hhCount * scans / pulsesPerDay + 1;
			for (var k = 0; k < hn && hhCount > 0; k++)
			{
				if (homeCursor >= hhCount)
					homeCursor = 0;

				var hh = homeCursor++;
				if ((hhs[hh].Flags & HhFlags.Alive) != 0 && hhs[hh].Home == 0 && hhs[hh].Size > 0)
				{
					var p = ChooseHome(hh, -1);
					if (p != null)
						MoveIn(hh, p);
				}
			}
		}

		/// <summary>Employers (ECO) may shrink job slots: the excess workers are laid off, newest citizen index first.</summary>
		void FireExcessWorkers()
		{
			if (overfull.Count == 0)
				return;

			for (var ci = citCount - 1; ci >= 0; ci--)
			{
				if ((cits[ci].Flags & (CitFlags.Alive | CitFlags.Worker)) != (CitFlags.Alive | CitFlags.Worker))
					continue;

				var p = registry.Get(cits[ci].Work);
				if (p != null && p.JobsFilled[cits[ci].JobLevel] - CommutersAt(p, cits[ci].JobLevel) > p.JobSlots[cits[ci].JobLevel])
				{
					ReleaseWork(ci);
					jobsLost++;
				}
			}
		}

		int CommutersAt(Property p, int level)
		{
			return commuterAt.TryGetValue(p.Id, out var comm) ? comm[level] : 0;
		}

		void TakeJob(int ci, Property p, int level)
		{
			DisplaceCommuter(p, level);
			p.JobsFilled[level]++;
			cits[ci].Work = p.Id;
			cits[ci].JobLevel = (byte)level;
			cits[ci].Flags |= CitFlags.Worker;
			cits[ci].UnemployedDays = 0;
			cits[ci].Shift = 0;
			jobsFound++;
		}

		void TryBetterJob(int ci, int hh)
		{
			var home = registry.Get(hhs[hh].Home);
			if (home == null)
				return;

			var level = cits[ci].Education;
			var p = FindJob(level, home);
			if (p == null)
				return;

			ReleaseWork(ci);
			TakeJob(ci, p, level);
		}

		int IncomeTaxPercent => cm != null ? cm.GetTaxRate(ZoneCategory.Residential) : 10;

		int IncomeTaxFor(int level) { return economyConcrete != null ? economyConcrete.IncomeTaxRate((EducationLevel)level) : IncomeTaxPercent; }

		void PayWage(int ci, int hh)
		{
			var wage = WageOf(cits[ci].JobLevel);
			var tax = wage * IncomeTaxFor(cits[ci].JobLevel) / 100;
			hhs[hh].Cash += wage - tax;
			wagesPaidCents += wage;
			if (economy != null)
			{
				economy.ChargeWages(cits[ci].Work, wage);
				economy.BookIncomeTax((EducationLevel)cits[ci].JobLevel, tax);
			}
			else
				taxPoolCents += tax;
		}

		/// <summary>Books collected income tax (fallback without an economy) as city income once whole dollars accumulate.</summary>
		void FlushMoney()
		{
			if (taxPoolCents >= 100 && cm != null)
			{
				var dollars = (int)(taxPoolCents / 100);
				cm.AddFunds(dollars, "tax-residential");
				taxPoolCents -= dollars * 100L;
			}
		}

		/// <summary>Household shopping: either a real shopping trip (purchase on arrival) or an immediate purchase.</summary>
		void Shop(int hh)
		{
			if (hhs[hh].Home != 0 && traffic != null && Hash(hh, Today, 50) % 100 < info.ShopTripPercent && PlanShopTrip(hh))
				return;

			BuyNow(hh);
		}

		void BuyNow(int hh)
		{
			ref var h = ref hhs[hh];
			var budget = h.Size * info.ShoppingCentsPerPerson;
			var spend = Math.Min(budget, Math.Max(0, h.Cash));
			if (spend <= 0)
			{
				if (budget > 0)
					unmetShopping++;

				return;
			}

			var charged = spend;
			if (economy != null && economy.ConsumerResources.Count > 0)
			{
				var home = registry.Get(h.Home);
				var res = economy.ConsumerResources[Hash(hh, Today, 51) % economy.ConsumerResources.Count];
				var shop = home != null ? economy.FindShop(res, home.AccessRoad) : 0;
				charged = 0;
				if (shop != 0)
					economy.SellToHousehold(shop, res, 2000 * h.Size, spend, out charged);

				if (charged <= 0)
					unmetShopping++;
			}

			h.Cash -= charged;
			spentCents += charged;
			h.Spent = charged;
		}
	}
}
