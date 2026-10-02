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
using System.Runtime.CompilerServices;
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Numbers of the active info view for the legends: average, highest and lowest value of the cells the view
	/// colours, and a histogram of the values (category / resource legends). Read-only UI state computed from
	/// InfoViewLayer.ValueAt a couple of times per second; never touches the simulation.
	/// </summary>
	public sealed class InfoViewStats
	{
		[FluentReference]
		const string AverageLabel = "label-infoview-stat-average";

		[FluentReference]
		const string HighestLabel = "label-infoview-stat-highest";

		[FluentReference]
		const string LowestLabel = "label-infoview-stat-lowest";

		[FluentReference("name", "value")]
		const string DistrictLabel = "label-infoview-stat-district";

		[FluentReference("value")]
		const string Percent = "label-city-percent";

		const int RefreshTicks = 20;

		/// <summary>Largest value of any ramp: continuous 0..100, resources kind * 16 + step.</summary>
		const int Values = 112;

		static readonly ConditionalWeakTable<World, InfoViewStats> Cache = [];

		readonly World world;
		readonly CityUiContext ctx;
		readonly int[] counts = new int[Values];
		CityInfoView mode;
		int lastRefresh = -1000;

		/// <summary>Number of cells with a value (continuous views: the cells the overlay draws).</summary>
		public int Count;

		public int Average;
		public int Highest;
		public int Lowest;
		public CPos HighestCell;
		public CPos LowestCell;

		InfoViewStats(World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
		}

		public static InfoViewStats For(World world)
		{
			if (!Cache.TryGetValue(world, out var stats))
			{
				stats = new InfoViewStats(world);
				Cache.Add(world, stats);
			}

			return stats;
		}

		public bool Valid => Count > 0;

		/// <summary>Number of cells whose value is exactly v (category index, resource value).</summary>
		public int CellsWith(int value) { return value >= 0 && value < Values ? counts[value] : 0; }

		/// <summary>Average richness 0..100 of one resource kind (value = kind * 16 + step 0..10), -1 when the kind is absent.</summary>
		public int KindRichness(int kind)
		{
			long cells = 0, sum = 0;
			for (var step = 0; step <= 10; step++)
			{
				var n = CellsWith(kind * 16 + step);
				cells += n;
				sum += (long)n * step * 10;
			}

			return cells == 0 ? -1 : (int)(sum / cells);
		}

		/// <summary>Recomputes after a short delay or when the view changed.</summary>
		public void Update(InfoViewLayer layer)
		{
			if (layer == null)
				return;

			var now = Game.RenderFrame;
			if (layer.Mode == mode && now - lastRefresh < RefreshTicks)
				return;

			// The layer computes its cells while rendering, a moment after the view changed: look again soon.
			lastRefresh = layer.Mode == mode ? now : now - RefreshTicks + 3;
			mode = layer.Mode;
			Array.Clear(counts);
			Count = 0;
			if (mode == CityInfoView.None)
				return;

			long sum = 0;
			Highest = int.MinValue;
			Lowest = int.MaxValue;
			foreach (var cell in world.Map.AllCells)
			{
				var v = layer.ValueAt(cell);
				if (v < 0)
					continue;

				if (v < Values)
					counts[v]++;

				Count++;
				sum += v;
				if (v > Highest)
				{
					Highest = v;
					HighestCell = cell;
				}

				if (v < Lowest)
				{
					Lowest = v;
					LowestCell = cell;
				}
			}

			Average = Count > 0 ? (int)(sum / Count) : 0;
		}

		/// <summary>Name of the district a cell lies in, or null.</summary>
		public string DistrictNameAt(CPos cell)
		{
			var id = ctx.Progression?.GetDistrict(cell) ?? 0;
			return id == 0 ? null : DistrictName(id);
		}

		public string DistrictName(int id)
		{
			var districts = ctx.ProgressionUi?.Districts;
			if (districts != null)
				foreach (var d in districts)
					if (d.Id == id && !string.IsNullOrEmpty(d.Name))
						return d.Name;

			return "#" + id;
		}

		/// <summary>A value and, when the cell lies in a named district, "District value".</summary>
		public string Describe(CPos cell, int value)
		{
			var text = FluentProvider.GetMessage(Percent, "value", value);
			var district = DistrictNameAt(cell);
			return district == null ? text : FluentProvider.GetMessage(DistrictLabel, "name", district, "value", text);
		}

		/// <summary>The (label, value) lines "Average / Highest / Lowest" of the active continuous view; empty when no data.</summary>
		public System.Collections.Generic.IEnumerable<(string Label, string Value)> Lines()
		{
			if (!Valid)
				yield break;

			yield return (FluentProvider.GetMessage(AverageLabel), FluentProvider.GetMessage(Percent, "value", Average));
			yield return (FluentProvider.GetMessage(HighestLabel), Describe(HighestCell, Highest));
			yield return (FluentProvider.GetMessage(LowestLabel), Describe(LowestCell, Lowest));
		}
	}
}
