#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Alert strip along the top of the screen: the worst city-wide problems (ICityProblems.Summary) as tier coloured chips with
	/// the number of affected buildings; clicking a chip selects one affected building and centres the camera on it.
	/// </summary>
	public class CityAlertsLogic : ChromeLogic
	{
		[FluentReference("name", "count")]
		const string ChipText = "label-alert-chip";

		const int MaxChips = 4;
		const int ChipWidth = 184;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget strip;
		readonly List<Chip> shown = [];

		struct Chip
		{
			public string Name;
			public int Count;
			public ProblemTier Tier;
			public CPos Cell;
			public uint ActorId;
		}

		int shownVersion = -1;
		int shownWildfires = -1;
		int shownAccidents = -1;

		[ObjectCreator.UseCtor]
		public CityAlertsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			strip = widget;

			// The strip only takes mouse input over its chips.
			widget.IsVisible = () =>
			{
				Refresh();
				return shown.Count > 0;
			};
		}

		static Color TierColor(ProblemTier tier)
		{
			switch (tier)
			{
				case ProblemTier.Info: return Color.FromArgb(0x6B, 0xB8, 0xF0);
				case ProblemTier.Problem: return Color.FromArgb(0xF2, 0xD0, 0x4C);
				case ProblemTier.Warning: return Color.FromArgb(0xF2, 0x8C, 0x30);
				case ProblemTier.Major: return Color.FromArgb(0xE5, 0x4B, 0x3C);
				case ProblemTier.Error: return Color.FromArgb(0xA8, 0x20, 0x20);
				case ProblemTier.Fatal: return Color.FromArgb(0x30, 0x10, 0x10);
				case ProblemTier.Good: return CityUi.Good;
				default: return Color.FromArgb(0x90, 0x90, 0x90);
			}
		}

		void Refresh()
		{
			var problems = ctx.Problems;
			if (problems == null || world.LocalPlayer == null)
			{
				shown.Clear();
				return;
			}

			// Wildfires on tree cells and road accidents are not building problems: they get chips of their own.
			var wildfires = ctx.Get<ServiceSimulation>()?.WildfireCount ?? 0;
			var incidents = ctx.Get<ITrafficIncidents>()?.Incidents;
			var accidents = incidents?.Count ?? 0;
			if (problems.Version == shownVersion && wildfires == shownWildfires && accidents == shownAccidents)
				return;

			shownVersion = problems.Version;
			shownWildfires = wildfires;
			shownAccidents = accidents;
			shown.Clear();
			if (wildfires > 0)
				shown.Add(new Chip { Name = CityUi.Message("label-alert-wildfire"), Count = wildfires, Tier = ProblemTier.Major });

			if (accidents > 0)
				shown.Add(new Chip { Name = CityUi.Message("label-alert-accident"), Count = accidents, Tier = ProblemTier.Warning, Cell = incidents[0].Cell });

			var count = 0;
			foreach (var entry in problems.Summary)
			{
				// Problems and worse; minor notes and "good news" stay on the building icons.
				if (entry.Tier < ProblemTier.Problem || entry.Tier == ProblemTier.Good)
					continue;

				shown.Add(new Chip
				{
					Name = CityUi.Message(ProblemCatalog.NameKey(entry.Problem)),
					Count = entry.Count,
					Tier = entry.Tier,
					Cell = entry.Cell,
					ActorId = entry.ActorId
				});

				if (++count >= MaxChips)
					break;
			}

			Rebuild();
		}

		void Rebuild()
		{
			strip.RemoveChildren();
			var width = shown.Count * (ChipWidth + 6) - 6;
			strip.Bounds.Width = width;
			strip.Bounds.X = (Game.Renderer.Resolution.Width - 226 - width) / 2;

			for (var i = 0; i < shown.Count; i++)
			{
				var entry = shown[i];
				var chip = Game.LoadWidget(world, "CITY_ALERT_CHIP", strip, []) as ButtonWidget;
				chip.Bounds.X = i * (ChipWidth + 6);
				chip.Bounds.Width = ChipWidth;
				var color = TierColor(entry.Tier);
				chip.Get<ColorBlockWidget>("TIER").GetColor = () => color;
				var text = FluentProvider.GetMessage(ChipText, "name", entry.Name, "count", entry.Count);
				chip.GetText = () => text;
				chip.Bounds.Y = 0;
				chip.OnClick = () =>
				{
					if (entry.Cell != CPos.Zero)
						ctx.Locate?.Invoke(entry.ActorId, entry.Cell);
				};
			}
		}
	}
}
