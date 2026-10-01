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

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Runtime view of a policy for the UI.</summary>
	public sealed class PolicyData
	{
		public int Index;
		public string Id;
		public PolicyScope Scope;
		public int Milestone;
		public int Upkeep, UpkeepPer100;
		public int SliderMin, SliderMax, SliderDefault;
		public string[] EffectKeys;
		public int[] EffectValues;
		public bool[] EffectSlider;

		public bool HasSlider => SliderMax > 0;
	}

	// Policies: yaml catalogue of integer effects, summed over the city policies and the policies of the cell's district.
	public partial class Progression
	{
		readonly List<PolicyData> policies = [];
		readonly Dictionary<string, int> policyIndex = [];
		readonly Dictionary<string, int> effectIndex = [];
		int[] policyValues = [];      // [district * policyCount + policy]: 0 = off, else 1 or the slider value (district 0 = city)
		int[] cityEffects = [];
		int[][] districtEffects = [];

		void BuildPolicies()
		{
			var effectKeys = new List<string>();
			foreach (var pi in self.Info.TraitInfos<ProgressionPolicyInfo>())
			{
				var id = pi.InstanceName;
				if (string.IsNullOrEmpty(id) || policyIndex.ContainsKey(id) || policies.Count >= 64)
				{
					CatalogueWarnings.Add("policy id missing, duplicated or over the limit: " + id);
					continue;
				}

				var p = new PolicyData
				{
					Index = policies.Count,
					Id = id,
					Scope = pi.Scope,
					Milestone = pi.Milestone,
					Upkeep = pi.Upkeep,
					UpkeepPer100 = pi.UpkeepPer100Residents,
					SliderMin = pi.SliderMin,
					SliderMax = pi.SliderMax,
					SliderDefault = pi.SliderDefault,
					EffectKeys = new string[pi.Effects.Length],
					EffectValues = new int[pi.Effects.Length],
					EffectSlider = new bool[pi.Effects.Length]
				};

				for (var e = 0; e < pi.Effects.Length; e++)
				{
					var kv = pi.Effects[e].Split('=');
					if (kv.Length != 2)
					{
						CatalogueWarnings.Add($"policy {id}: bad effect '{pi.Effects[e]}'");
						p.EffectKeys[e] = "";
						continue;
					}

					p.EffectKeys[e] = kv[0].Trim();
					var raw = kv[1].Trim();
					if (raw == "@slider")
						p.EffectSlider[e] = true;
					else if (!int.TryParse(raw, out p.EffectValues[e]))
						CatalogueWarnings.Add($"policy {id}: bad effect value '{raw}'");

					if (p.EffectKeys[e].Length > 0 && !effectIndex.ContainsKey(p.EffectKeys[e]))
					{
						effectIndex[p.EffectKeys[e]] = effectKeys.Count;
						effectKeys.Add(p.EffectKeys[e]);
					}
				}

				policyIndex[id] = policies.Count;
				policies.Add(p);
				RegisterMilestoneKey("policy:" + id, p.Milestone < 0 ? int.MaxValue : p.Milestone);
			}

			var districts = MaxDistricts + 1;
			policyValues = new int[districts * Math.Max(1, policies.Count)];
			cityEffects = new int[effectKeys.Count];
			districtEffects = new int[districts][];
			for (var d = 0; d < districts; d++)
				districtEffects[d] = new int[effectKeys.Count];
		}

		public int PolicyCount => policies.Count;

		public PolicyData GetPolicyData(int index) { return policies[index]; }

		public int FindPolicy(string id) { return id != null && policyIndex.TryGetValue(id, out var i) ? i : -1; }

		public bool IsPolicyAvailable(int index) { return IsUnlocked("policy:" + policies[index].Id); }

		/// <summary>0 = off, 1 = on, or the slider value. District 0 is the city scope.</summary>
		public int GetPolicyValue(int index, int district)
		{
			if (index < 0 || index >= policies.Count || district < 0 || district > MaxDistricts)
				return 0;

			return policyValues[district * policies.Count + index];
		}

		public bool IsPolicyActive(string id, int district) { return GetPolicyValue(FindPolicy(id), district) > 0; }

		public int ActivePolicyCount
		{
			get
			{
				var n = 0;
				for (var i = 0; i < policyValues.Length; i++)
					if (policyValues[i] > 0)
						n++;

				return n;
			}
		}

		/// <summary>Number of active city-scope policies.</summary>
		public int ActiveCityPolicyCount
		{
			get
			{
				var n = 0;
				for (var i = 0; i < policies.Count; i++)
					if (policyValues[i] > 0)
						n++;

				return n;
			}
		}

		/// <summary>Sum of an effect over the city policies and the policies of the district covering the cell (cells outside any district: city only).</summary>
		public int GetPolicy(string key, CPos cell)
		{
			return GetPolicyForDistrict(key, GetDistrict(cell));
		}

		/// <summary>City-scope policies only (use for city-wide systems such as demand or budget).</summary>
		public int GetCityPolicy(string key)
		{
			return GetPolicyForDistrict(key, 0);
		}

		public int GetPolicyForDistrict(string key, int district)
		{
			if (!effectIndex.TryGetValue(key, out var k))
				return 0;

			var v = cityEffects[k];
			if (district > 0 && district < districtEffects.Length)
				v += districtEffects[district][k];

			return v;
		}

		/// <summary>Enables, disables or sets the slider of a policy. Returns false (nothing changes) when not allowed.</summary>
		public bool SetPolicy(string id, int district, int value)
		{
			var pi = FindPolicy(id);
			if (pi < 0 || !IsPolicyAvailable(pi))
				return false;

			var p = policies[pi];
			if (p.Scope == PolicyScope.City ? district != 0 : (district <= 0 || district > MaxDistricts || !DistrictExists(district)))
				return false;

			if (value > 0)
				value = p.HasSlider ? Math.Clamp(value, p.SliderMin, p.SliderMax) : 1;

			policyValues[district * policies.Count + pi] = value;
			RebuildEffects();
			return true;
		}

		void RebuildEffects()
		{
			Array.Clear(cityEffects);
			for (var d = 0; d < districtEffects.Length; d++)
				Array.Clear(districtEffects[d]);

			var n = policies.Count;
			if (n == 0)
				return;

			for (var d = 0; d < districtEffects.Length; d++)
			{
				var target = d == 0 ? cityEffects : districtEffects[d];
				for (var pi = 0; pi < n; pi++)
				{
					var value = policyValues[d * n + pi];
					if (value <= 0)
						continue;

					var p = policies[pi];
					for (var e = 0; e < p.EffectKeys.Length; e++)
					{
						if (!effectIndex.TryGetValue(p.EffectKeys[e], out var k))
							continue;

						target[k] += p.EffectSlider[e] ? value : p.EffectValues[e];
					}
				}
			}
		}

		void ClearDistrictPolicies(int district)
		{
			var n = policies.Count;
			for (var pi = 0; pi < n; pi++)
				policyValues[district * n + pi] = 0;

			RebuildEffects();
		}

		/// <summary>Monthly upkeep of all active policies in dollars (flat + per 100 residents of the city / district).</summary>
		public int MonthlyPolicyUpkeep
		{
			get
			{
				long sum = 0;
				var n = policies.Count;
				for (var d = 0; d < districtEffects.Length; d++)
				{
					var pop = d == 0 ? CurrentPopulation : GetDistrictStats(d).Population;
					for (var pi = 0; pi < n; pi++)
					{
						if (policyValues[d * n + pi] <= 0)
							continue;

						var p = policies[pi];
						sum += p.Upkeep + (long)p.UpkeepPer100 * pop / 100;
					}
				}

				return (int)Math.Min(int.MaxValue, sum);
			}
		}

		int PolicyHash()
		{
			unchecked
			{
				var h = 0;
				for (var i = 0; i < policyValues.Length; i++)
					h = h * 31 + policyValues[i];

				return h;
			}
		}

		void ChargeUpkeep()
		{
			var tiles = MonthlyTileUpkeep;
			if (tiles > 0)
				Spend(tiles, "tile-upkeep");

			var pol = MonthlyPolicyUpkeep;
			if (pol > 0)
				Spend(pol, "policies");
		}
	}
}
