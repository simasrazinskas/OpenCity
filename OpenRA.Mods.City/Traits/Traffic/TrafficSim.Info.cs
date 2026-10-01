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
	// Data for the traffic info view (ITrafficInfo): volume vs flow per cell, 24 hour flow history.
	public sealed partial class TrafficSim
	{
		int[] volume;                       // decayed traversals per cell (about the last 100 ticks)
		readonly long[] flowHourSum = new long[24];
		readonly int[] flowHourCount = new int[24];
		int lastFlowHour = -1;

		void CountTraversal(int cell)
		{
			volume ??= new int[cellCount];
			volume[cell]++;
		}

		void DecayVolume()
		{
			if (volume == null)
				return;

			for (var k = 0; k < roadListCount; k++)
			{
				var c = roadList[k];
				volume[c] -= volume[c] / 4;
			}
		}

		void SampleFlowHistory()
		{
			if (clock == null)
				return;

			var hour = clock.Hour % 24;
			if (hour != lastFlowHour)
			{
				lastFlowHour = hour;
				flowHourSum[hour] = 0;
				flowHourCount[hour] = 0;
			}

			flowHourSum[hour] += CityTrafficFlow;
			flowHourCount[hour]++;
		}

		int ITrafficInfo.GetTrafficVolumePercent(CPos cell)
		{
			if (volume == null || !InMap(cell))
				return 0;

			var c = Cell(cell);
			if (roadFlag[c] == 0)
				return 0;

			// Free-flow capacity of the cell's lanes per ~100 ticks, both directions (Greenshields maximum S / (4 * freeFlow)).
			var reference = Math.Max(1, Storage(c) * 2 * 25 * U / Math.Max(1, ffU[c]));
			return Math.Min(100, volume[c] * 100 / reference);
		}

		int ITrafficInfo.GetTrafficFlowPercent(CPos cell)
		{
			return ((ITrafficService)this).GetTrafficFlow(cell);
		}

		int ITrafficInfo.GetFlowHistory(int hour)
		{
			hour = (hour % 24 + 24) % 24;
			return flowHourCount[hour] > 0 ? (int)(flowHourSum[hour] / flowHourCount[hour]) : -1;
		}

		int ITrafficInfo.AverageTicksPer100Cells => tripCellSum > 0 ? (int)(tripTickSum * 100 / tripCellSum) : 0;

		int ITrafficInfo.OpenIncidents => incidents.Count;
	}
}
