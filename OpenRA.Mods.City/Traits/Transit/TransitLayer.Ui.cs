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
	// Read side for the UI WP: line overview (ITransitUiSource), the Transit info view (stop coverage) and depot/station inspection rows.
	// Everything here only reads simulation state; the cached lists are refreshed from the simulation tick.
	public sealed partial class TransitLayer : ITransitUiSource, ITransitUiSourceEx, IInfoViewSource, IInspectionContributor
	{
		readonly List<TransitLineEntry> uiLines = [];
		int uiVersion = -1;
		int coverageVersion = -1;
		CellLayer<byte> coverage;

		readonly List<TransitStopEntry> uiStops = [];
		readonly List<TransitDepotEntry> uiDepots = [];

		IReadOnlyList<TransitStopEntry> ITransitUiSourceEx.StopEntries => uiStops;

		IReadOnlyList<TransitDepotEntry> ITransitUiSourceEx.DepotEntries => uiDepots;

		string ITransitUiSourceEx.CheckStop(CPos cell, string mode)
		{
			return TryParseMode(mode, out var m) ? ValidateStop(cell, m) : ErrorNotSupported;
		}

		bool ITransitUiSourceEx.HasTramTrack(CPos cell) => IsTramTrack(cell);

		bool ITransitUiSourceEx.HasRail(CPos cell) => rail != null && rail.IsRail(cell);

		bool ITransitUiSourceEx.IsLevelCrossing(CPos cell) => rail != null && rail.IsCrossing(cell);

		bool ITransitUiSourceEx.IsModeUnlocked(string mode) => TryParseMode(mode, out var m) && ModeUnlocked(m);

		int ITransitUiSourceEx.StopCost => Info.StopCost;

		int ITransitUiSourceEx.TramTrackCostPerCell => Info.TramTrackCostPerCell;

		int ITransitUiSourceEx.RailCostPerCell => rail?.Info.CostPerCell ?? 0;

		int ITransitUiSourceEx.MetroTunnelCost(IList<int> stopIds, bool loop) => TunnelCells(stopIds, loop && stopIds.Count > 2) * Info.MetroTunnelCostPerCell;

		List<CPos> ITransitUiSourceEx.PreviewLine(string mode, IList<int> stopIds, bool loop, out bool broken, out int cycleTicks)
		{
			var m = TryParseMode(mode, out var parsed) ? parsed : TransitMode.Bus;
			return PreviewRoute(stopIds, loop, out broken, out cycleTicks, m);
		}

		void RefreshUiStops()
		{
			uiStops.Clear();
			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				uiStops.Add(new TransitStopEntry
				{
					Id = s.Id,
					Mode = ModeName(s.Mode),
					Cell = s.Cell,
					Side = s.Side,
					Waiting = s.WaitingCount,
					Lines = s.LineIds.Count,
					StationActorId = s.StationActorId,
				});
			}

			uiDepots.Clear();
			for (var i = 0; i < depots.Count; i++)
			{
				var d = depots[i];
				uiDepots.Add(new TransitDepotEntry { ActorId = d.ActorId, Mode = ModeName(d.Mode), Used = d.Used, Capacity = d.Capacity, Road = d.Road });
			}
		}

		int ITransitUiSource.StopAt(CPos cell)
		{
			// Prefer line stops (bus, metro) over taxi stands so the line tool picks the right one.
			TransitStop any = null;
			for (var i = 0; i < stops.Count; i++)
			{
				if (stops[i].Cell != cell)
					continue;

				if (stops[i].Mode != TransitMode.Taxi)
					return stops[i].Id;

				any ??= stops[i];
			}

			return any?.Id ?? 0;
		}

		IReadOnlyList<TransitLineEntry> ITransitUiSource.Lines => uiLines;

		/// <summary>Rebuilds the overview rows. Called from the simulation tick (every pulse and after structural changes).</summary>
		void RefreshUiLines()
		{
			uiVersion = Version;
			RefreshUiStops();
			uiLines.Clear();
			var showLast = clock != null && clock.TickOfDay < clock.TicksPerDay / 4;
			for (var i = 0; i < lines.Count; i++)
			{
				var line = lines[i];
				var rgb = LineColor(line.Color);
				var stop0 = line.StopIds.Count > 0 ? GetStop(line.StopIds[0]) : null;
				var stats = showLast ? line.LastMonth : line.ThisMonth;
				var usage = line.ThisMonth.LoadSamples > 0 ? line.ThisMonth.UsagePercent : line.LastMonth.UsagePercent;
				var waiting = 0;
				for (var s = 0; s < stops.Count; s++)
					for (var g = 0; g < stops[s].Waiting.Count; g++)
						if (stops[s].Waiting[g].LineId == line.Id)
							waiting += stops[s].Waiting[g].Count;

				uiLines.Add(new TransitLineEntry
				{
					Id = line.Id,
					Name = line.Name,
					Mode = ModeName(line.Mode),
					ArgbColor = unchecked((int)0xFF000000) | (rgb.R << 16) | (rgb.G << 8) | rgb.B,
					Stops = line.StopIds.Count,
					Vehicles = ActiveVehicles(line),
					Usage = Math.Clamp(usage, 0, 100),
					PassengersThisMonth = stats.Passengers,
					RevenueMonth = stats.FareCents / 100,
					CostMonth = stats.CostCents / 100,
					WaitingNow = waiting,
					TicketCents = Math.Max(line.TicketCents, stop0 != null ? Info.DefaultTicketCents * (Progression()?.GetPolicy("FareMinPct", stop0.Cell) ?? 0) / 100 : 0),
					MaxTicketCents = Info.MaxTicketCents,
					VehicleTarget = line.TargetVehicles,
				});
			}
		}

		// ---- info view: stop coverage ----
		bool IInfoViewSource.Supports(CityInfoView mode) => mode == CityInfoView.Transit;

		int IInfoViewSource.GetCell(CityInfoView mode, CPos cell)
		{
			if (mode != CityInfoView.Transit || stops.Count == 0)
				return -1;

			if (coverage == null || coverageVersion != Version)
				BuildCoverage();

			if (!coverage.Contains(cell))
				return -1;

			var v = coverage[cell];
			return v == 0 ? -1 : v;
		}

		void BuildCoverage()
		{
			coverageVersion = Version;
			coverage ??= new CellLayer<byte>(world.Map);
			coverage.Clear();
			var radius = Math.Max(1, Info.WalkRadius);
			for (var i = 0; i < stops.Count; i++)
			{
				var s = stops[i];
				if (s.Mode == TransitMode.Taxi || s.LineIds.Count == 0)
					continue;

				for (var dy = -radius; dy <= radius; dy++)
				{
					for (var dx = -radius; dx <= radius; dx++)
					{
						var d = Math.Abs(dx) + Math.Abs(dy);
						if (d > radius)
							continue;

						var c = new CPos(s.Cell.X + dx, s.Cell.Y + dy);
						if (!coverage.Contains(c))
							continue;

						var value = (byte)Math.Max(1, 100 - d * 90 / radius);
						if (value > coverage[c])
							coverage[c] = value;
					}
				}
			}
		}

		// ---- building panel rows ----
		void IInspectionContributor.Contribute(Actor building, Property property, List<InspectionRow> rows)
		{
			if (building.TraitOrDefault<TransitDepot>() != null)
			{
				for (var i = 0; i < depots.Count; i++)
				{
					if (depots[i].ActorId != building.ActorID)
						continue;

					var d = depots[i];
					rows.Add(new InspectionRow
					{
						Label = FluentProvider.GetMessage(Info.FleetLabel),
						Value = d.Used + " / " + d.Capacity,
						Tone = d.Used >= d.Capacity ? 2 : 0,
						BarPercent = d.Capacity > 0 ? d.Used * 100 / d.Capacity : -1,
					});
					if (d.Road == CPos.Zero)
						rows.Add(new InspectionRow { Label = FluentProvider.GetMessage(Info.NoRoadLabel), Value = "!", Tone = 3, BarPercent = -1 });

					return;
				}
			}

			if (building.TraitOrDefault<TransitStation>() != null)
			{
				for (var i = 0; i < stops.Count; i++)
				{
					if (stops[i].StationActorId != building.ActorID)
						continue;

					rows.Add(new InspectionRow
					{
						Label = FluentProvider.GetMessage(Info.WaitingLabel),
						Value = stops[i].WaitingCount.ToString(System.Globalization.CultureInfo.CurrentCulture),
						Tone = stops[i].WaitingCount > 30 ? 2 : 0,
						BarPercent = Math.Min(100, stops[i].WaitingCount * 100 / Math.Max(1, Info.MaxStopQueue)),
					});
					return;
				}
			}
		}
	}
}
