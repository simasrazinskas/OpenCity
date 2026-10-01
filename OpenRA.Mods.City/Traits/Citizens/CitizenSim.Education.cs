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
	// Schooling: enrolment (ICityServices.TryEnroll or a fallback over schools with seats), study, graduation.
	public partial class CitizenSim
	{
		int graduates, enrollments;

		void UpdateSchooling(int ci, int age, int today)
		{
			var c = cits[ci];
			if ((c.Flags & CitFlags.Student) != 0)
			{
				StudyDay(ci, today);
				return;
			}

			if (c.Education >= 4 || (c.Flags & (CitFlags.Sick | CitFlags.Retired | CitFlags.Jailed)) != 0 || c.Household < 0)
				return;

			var level = c.Education + 1;
			var chance = 0;
			switch (level)
			{
				case 1:
					if (age > 5 && age <= info.ChildMaxAge)
						chance = 100;
					break;
				case 2:
					if (age > info.ChildMaxAge && age <= info.TeenMaxAge)
						chance = info.TeenApplyPercent;
					else if (age > info.TeenMaxAge && age < 25)
						chance = info.TeenApplyPercent / 3;
					break;
				case 3:
					if (age >= 16 && age <= 40)
						chance = info.CollegeApplyPercent * (UnderEmployed(ci) ? 3 : 1);
					break;
				default:
					if (age >= 18 && age <= 45)
						chance = info.UniversityApplyPercent * (UnderEmployed(ci) ? 3 : 1);
					break;
			}

			if (chance > 0 && chance < 100 && progression != null)
			{
				var home = registry.Get(hhs[c.Household].Home);
				if (home != null)
					chance = chance * (100 + progression.GetPolicy("EduBoostPct", home.AccessCell)) / 100;
			}

			if (chance > 0 && Hash(ci, today, 20 + level) % 100 < chance)
				Enroll(ci, level);
		}

		bool UnderEmployed(int ci)
		{
			var c = cits[ci];
			return (c.Flags & CitFlags.Worker) == 0 || c.JobLevel < c.Education;
		}

		void Enroll(int ci, int level)
		{
			var hh = cits[ci].Household;
			var home = registry.Get(hhs[hh].Home);
			if (home == null)
				return;

			int school;
			if (services != null)
				school = services.TryEnroll(ci + 1, level, home.AccessRoad);
			else
			{
				var list = schoolCands[level];
				Property best = null;
				var bestD = int.MaxValue;
				for (var s = 0; s < info.SearchSamples && list.Count > 0; s++)
				{
					var p = list[NextRandom(list.Count)];
					if (p.Students >= SeatsOf(p) || !Alive(p))
						continue;

					var d = ManhattanRoad(home, p);
					if (d < bestD)
					{
						bestD = d;
						best = p;
					}
				}

				school = best?.Id ?? 0;
			}

			if (school == 0)
				return;

			var sp = registry.Get(school);
			if (sp == null)
				return;

			ReleaseWork(ci);
			sp.Students++;
			cits[ci].Work = school;
			cits[ci].StudyDays = 0;
			cits[ci].Flags |= CitFlags.Student;
			enrollments++;
		}

		void StudyDay(int ci, int today)
		{
			var c = cits[ci];
			var school = registry.Get(c.Work);
			if (school == null)
			{
				// School demolished.
				cits[ci].Work = 0;
				cits[ci].Flags &= ~CitFlags.Student;
				return;
			}

			if ((c.Flags & CitFlags.Sick) != 0 || !school.Operational)
				return;

			var level = Math.Min(4, c.Education + 1);
			var need = info.StudyDaysByLevel[level];
			var days = c.StudyDays + 1;
			if (days < need)
			{
				cits[ci].StudyDays = (ushort)days;
				return;
			}

			var passBonus = progression != null ? progression.GetPolicy("EduBoostPct", school.AccessCell) / 5 : 0;
			if (Hash(ci, today, 30) % 100 < Math.Min(100, info.StudyPassPercent + passBonus))
			{
				cits[ci].Education = (byte)level;
				graduates++;
				Announce("chirp-citizen-graduated", ci, 2, 35);
				ReleaseWork(ci);
			}
			else
				cits[ci].StudyDays = (ushort)Math.Max(0, days - Math.Max(1, need / 4));
		}
	}
}
