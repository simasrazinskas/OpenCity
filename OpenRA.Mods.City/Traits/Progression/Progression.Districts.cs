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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Per-district statistics (recomputed every few pulses while districts exist). Unknown values are -1.</summary>
	public sealed class DistrictStats
	{
		public int Cells, Buildings, Population, Households, JobSlots, Jobs, AverageHappiness = -1, AverageLandValue = -1;

		/// <summary>Job slots by workplace category: commercial, industrial, office.</summary>
		public int CommercialJobs, IndustrialJobs, OfficeJobs;

		/// <summary>Sampled residents by education level (uneducated..highly); only filled with a citizen sim.</summary>
		public readonly int[] Education = new int[5];

		/// <summary>Average of max(air, ground) pollution at the buildings, 0 without a pollution map.</summary>
		public int Pollution;

		public void Clear()
		{
			Cells = Buildings = Population = Households = JobSlots = Jobs = 0;
			CommercialJobs = IndustrialJobs = OfficeJobs = Pollution = 0;
			AverageHappiness = AverageLandValue = -1;
			Array.Clear(Education);
		}
	}

	// Districts: a byte per cell (0 = none, 1..MaxDistricts), names, paint/rename/delete orders and statistics.
	public partial class Progression
	{
		bool[] districtExists = [];
		string[] districtNames = [];
		int[] districtCells = [];
		DistrictStats[] districtStats = [];
		readonly int[] happinessSum = new int[64];
		readonly int[] happinessCount = new int[64];
		readonly long[] landValueSum = new long[64];
		readonly long[] pollutionSum = new long[64];
		int districtsCreated;

		static readonly string[] DistrictNameList =
		[
			"Downtown", "Old Town", "Harbor View", "Greenfield", "Riverside", "Hillcrest", "Midtown", "Eastgate",
			"Westend", "Northpoint", "Southbank", "Lakeside", "Market Square", "Station Quarter", "Oakwood", "Sunnyside"
		];

		/// <summary>Deterministic default name for a new district (the list cycles, with a number suffix after the first round).</summary>
		static string AutoDistrictName(int created)
		{
			var round = created / DistrictNameList.Length;
			var name = DistrictNameList[created % DistrictNameList.Length];
			return round == 0 ? name : name + " " + (round + 1);
		}

		/// <summary>Ids of the policies active in a district (0 = the city scope).</summary>
		public IReadOnlyList<string> GetDistrictPolicyIds(int district)
		{
			var list = new List<string>();
			for (var pi = 0; pi < policies.Count; pi++)
				if (GetPolicyValue(pi, district) > 0)
					list.Add(policies[pi].Id);

			return list;
		}

		public int MaxDistricts => Math.Clamp(Info.MaxDistricts, 1, 63);

		void EnsureDistricts()
		{
			if (DistrictLayer != null)
				return;

			DistrictLayer = new CellLayer<byte>(world.Map);
			var n = MaxDistricts + 1;
			districtExists = new bool[n];
			districtNames = new string[n];
			districtCells = new int[n];
			districtStats = new DistrictStats[n];
			for (var i = 0; i < n; i++)
				districtStats[i] = new DistrictStats();
		}

		/// <summary>District id at a cell (0 = none).</summary>
		public int GetDistrict(CPos cell)
		{
			return DistrictLayer != null && DistrictLayer.Contains(cell) ? DistrictLayer[cell] : 0;
		}

		public bool DistrictExists(int id) { return DistrictLayer != null && id > 0 && id <= MaxDistricts && districtExists[id]; }

		public int DistrictCount
		{
			get
			{
				var n = 0;
				for (var i = 1; i < districtExists.Length; i++)
					if (districtExists[i])
						n++;

				return n;
			}
		}

		/// <summary>Custom name, or null (UI shows the fluent 'district-default-name' with the id).</summary>
		public string GetDistrictName(int id) { return DistrictExists(id) ? districtNames[id] : null; }

		public int GetDistrictCellCount(int id) { return DistrictExists(id) ? districtCells[id] : 0; }

		public DistrictStats GetDistrictStats(int id)
		{
			return id > 0 && id < districtStats.Length ? districtStats[id] : EmptyStats;
		}

		static readonly DistrictStats EmptyStats = new();

		/// <summary>Raw cell layer for renderers (read-only use!). Null until the world ticked once.</summary>
		public CellLayer<byte> DistrictLayer { get; private set; }

		int FreeDistrictId()
		{
			for (var i = 1; i <= MaxDistricts; i++)
				if (!districtExists[i])
					return i;

			return 0;
		}

		void PaintDistrict(CPos from, CPos to, uint data)
		{
			if (!IsUnlocked("tool:districts"))
				return;

			EnsureDistricts();
			var x0 = Math.Min(from.X, to.X);
			var x1 = Math.Max(from.X, to.X);
			var y0 = Math.Min(from.Y, to.Y);
			var y1 = Math.Max(from.Y, to.Y);
			if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) > Info.MaxPaintCells)
				return;

			var id = 0;
			var created = false;
			if (data == ProgressionOrders.NewDistrict)
			{
				id = FreeDistrictId();
				if (id == 0)
					return;

				created = true;
			}
			else if (data != 0)
			{
				id = (int)Math.Min(data, 255u);
				if (!DistrictExists(id))
					return;
			}

			var painted = 0;
			for (var y = y0; y <= y1; y++)
			{
				for (var x = x0; x <= x1; x++)
				{
					var cell = new CPos(x, y);
					if (!DistrictLayer.Contains(cell))
						continue;

					var old = DistrictLayer[cell];
					if (old == id)
						continue;

					if (old != 0)
						districtCells[old]--;

					DistrictLayer[cell] = (byte)id;
					if (id != 0)
						districtCells[id]++;

					painted++;
				}
			}

			if (created && painted > 0)
			{
				districtExists[id] = true;
				districtNames[id] = AutoDistrictName(districtsCreated++);
			}

			// A district painted over completely is gone (policies included): no ghost entries.
			CleanupDistricts();
			if (painted > 0)
				UpdateDistrictStats();
		}

		void RenameDistrict(int id, string name)
		{
			if (!DistrictExists(id))
				return;

			var sb = new StringBuilder();
			foreach (var c in name ?? "")
				if (c >= ' ' && c != '\u007f' && sb.Length < 24)
					sb.Append(c);

			var s = sb.ToString().Trim();
			districtNames[id] = s.Length == 0 ? null : s;
		}

		void DeleteDistrict(int id)
		{
			if (!DistrictExists(id))
				return;

			foreach (var cell in world.Map.AllCells)
				if (DistrictLayer[cell] == id)
					DistrictLayer[cell] = 0;

			RemoveDistrict(id);
		}

		void RemoveDistrict(int id)
		{
			districtExists[id] = false;
			districtNames[id] = null;
			districtCells[id] = 0;
			districtStats[id].Clear();
			ClearDistrictPolicies(id);
		}

		// Districts that lost all cells (painted over) disappear at month end.
		void CleanupDistricts()
		{
			for (var i = 1; i < districtExists.Length; i++)
				if (districtExists[i] && districtCells[i] <= 0)
					RemoveDistrict(i);
		}

		int DistrictHash()
		{
			if (DistrictLayer == null)
				return 0;

			unchecked
			{
				var h = 0;
				for (var i = 1; i < districtExists.Length; i++)
					if (districtExists[i])
						h = h * 31 + i * 7 + districtCells[i] + StableHash(districtNames[i]);

				return h;
			}
		}

		// ---- statistics ----
		void UpdateDistrictStats()
		{
			if (DistrictLayer == null || DistrictCount == 0)
				return;

			for (var i = 1; i < districtStats.Length; i++)
				districtStats[i].Clear();

			Array.Clear(happinessSum);
			Array.Clear(happinessCount);
			Array.Clear(landValueSum);
			Array.Clear(pollutionSum);

			for (var i = 1; i < districtStats.Length; i++)
				districtStats[i].Cells = districtExists[i] ? districtCells[i] : 0;

			var registry = world.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			if (registry != null && citizens != null)
				StatsFromProperties(registry);
			else
				StatsFromBuildings();

			for (var i = 1; i < districtStats.Length; i++)
			{
				var s = districtStats[i];
				if (happinessCount[i] > 0)
					s.AverageHappiness = happinessSum[i] / happinessCount[i];

				if (s.Buildings > 0)
				{
					s.AverageLandValue = (int)(landValueSum[i] / s.Buildings);
					s.Pollution = (int)(pollutionSum[i] / s.Buildings);
				}
			}
		}

		void StatsFromProperties(IPropertyRegistry registry)
		{
			var all = registry.All;
			for (var n = 0; n < all.Count; n++)
			{
				var p = all[n];
				var d = GetDistrict(p.Origin);
				if (d <= 0 || !districtExists[d])
					continue;

				var s = districtStats[d];
				s.Buildings++;
				s.Population += p.Residents;
				s.Households += p.Households;
				s.JobSlots += p.TotalJobSlots;
				s.Jobs += p.TotalJobsFilled;
				AddCategoryJobs(s, p.Zone.Category(), p.TotalJobSlots);
				landValueSum[d] += p.LandValue;
				if (pollution != null)
					pollutionSum[d] += Math.Max(pollution.GetAir(p.AccessCell), pollution.GetGround(p.AccessCell));

				// Sample up to three residents per home for the happiness average.
				if (p.Residents > 0)
				{
					var sampled = 0;
					foreach (var id in citizens.ResidentsOf(p.Id))
					{
						if (citizens.TryGetCitizen(id, out var view))
						{
							happinessSum[d] += view.Happiness;
							happinessCount[d]++;
							s.Education[(int)view.Education]++;
						}

						if (++sampled >= 3)
							break;
					}
				}
			}
		}

		static void AddCategoryJobs(DistrictStats s, ZoneCategory cat, int slots)
		{
			switch (cat)
			{
				case ZoneCategory.Commercial: s.CommercialJobs += slots; break;
				case ZoneCategory.Industrial: s.IndustrialJobs += slots; break;
				case ZoneCategory.Office: s.OfficeJobs += slots; break;
			}
		}

		void StatsFromBuildings()
		{
			foreach (var a in world.ActorsHavingTrait<CityBuilding>())
			{
				var b = a.Trait<CityBuilding>();
				if (b.Manager != manager || b.Cells.Length == 0)
					continue;

				var d = GetDistrict(b.Cells[0]);
				if (d <= 0 || !districtExists[d])
					continue;

				var s = districtStats[d];
				s.Buildings++;
				landValueSum[d] += b.LandValue;
				if (b.Category == ZoneCategory.Residential)
				{
					s.Population += b.Residents;
					s.Households += (b.Residents + 2) / 3;
					happinessSum[d] += b.Happiness * b.Residents;
					happinessCount[d] += b.Residents;
				}
				else if (b.Info.MaxJobs > 0)
				{
					s.JobSlots += b.Info.MaxJobs;
					s.Jobs += b.Workers;
					AddCategoryJobs(s, b.Category, b.Info.MaxJobs);
				}
			}
		}
	}
}
