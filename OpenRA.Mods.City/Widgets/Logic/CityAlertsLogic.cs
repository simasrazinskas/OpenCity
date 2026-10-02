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

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// The alert row of the status bar (design: hud-*.png, under the news ticker): the worst city-wide problems
	/// (ICityProblems.Summary, tier Problem and worse) plus wildfires and road accidents, each as its status icon and the
	/// number of affected buildings. Hover shows the name, clicking selects one affected building and centres on it.
	/// </summary>
	public class CityAlertsLogic : ChromeLogic
	{
		[FluentReference("name", "count")]
		const string ChipText = "label-alert-chip";

		const int MaxChips = 8;

		readonly World world;
		readonly CityUiContext ctx;
		readonly Widget strip;
		readonly List<Chip> shown = [];

		struct Chip
		{
			public string Name;
			public string Icon;
			public int Count;
			public CPos Cell;
			public uint ActorId;
		}

		int shownVersion = -1;
		int shownWildfires = -1;
		int shownAccidents = -1;
		int shownWidth = -1;

		[ObjectCreator.UseCtor]
		public CityAlertsLogic(Widget widget, World world)
		{
			this.world = world;
			ctx = CityUiContext.For(world);
			strip = widget;
		}

		/// <summary>The status icon of a problem: `st_` + the snake_case problem name (st_no_power, st_air_pollution ...).</summary>
		public static string ProblemIcon(CityProblem problem)
		{
			var name = problem.ToString();
			var sb = new StringBuilder("st_");
			for (var i = 0; i < name.Length; i++)
			{
				if (i > 0 && char.IsUpper(name[i]))
					sb.Append('_');

				sb.Append(char.ToLowerInvariant(name[i]));
			}

			return sb.ToString();
		}

		public override void Tick()
		{
			var problems = ctx.Problems;
			if (problems == null || world.LocalPlayer == null)
			{
				if (shown.Count > 0)
				{
					shown.Clear();
					Rebuild();
				}

				return;
			}

			// Wildfires on tree cells and road accidents are not building problems: they get chips of their own.
			var wildfires = ctx.Get<ServiceSimulation>()?.WildfireCount ?? 0;
			var incidents = ctx.Get<ITrafficIncidents>()?.Incidents;
			var accidents = incidents?.Count ?? 0;
			if (problems.Version == shownVersion && wildfires == shownWildfires && accidents == shownAccidents && strip.Bounds.Width == shownWidth)
				return;

			shownVersion = problems.Version;
			shownWildfires = wildfires;
			shownAccidents = accidents;
			shownWidth = strip.Bounds.Width;
			shown.Clear();
			if (wildfires > 0)
				shown.Add(new Chip { Name = CityUi.Message("label-alert-wildfire"), Icon = "st_wildfire", Count = wildfires });

			if (accidents > 0)
				shown.Add(new Chip { Name = CityUi.Message("label-alert-accident"), Icon = "st_accident", Count = accidents, Cell = incidents[0].Cell });

			foreach (var entry in problems.Summary)
			{
				// Problems and worse; minor notes and "good news" stay on the building icons.
				if (entry.Tier < ProblemTier.Problem || entry.Tier == ProblemTier.Good)
					continue;

				shown.Add(new Chip
				{
					Name = CityUi.Message(ProblemCatalog.NameKey(entry.Problem)),
					Icon = ProblemIcon(entry.Problem),
					Count = entry.Count,
					Cell = entry.Cell,
					ActorId = entry.ActorId
				});

				if (shown.Count >= MaxChips)
					break;
			}

			Rebuild();
		}

		void Rebuild()
		{
			strip.RemoveChildren();
			var font = Game.Renderer.Fonts["Tiny"];
			var x = 0;
			foreach (var entry in shown)
			{
				var count = entry.Count.ToString(CultureInfo.CurrentCulture);
				var width = 16 + 3 + font.Measure(count).X + 6;
				if (x + width > strip.Bounds.Width)
					break;

				var chip = (ButtonWidget)Game.LoadWidget(world, "CITY_ALERT_CHIP", strip, []);
				chip.Bounds = new WidgetBounds(x, 0, width, strip.Bounds.Height);
				chip.Get<CityIconWidget>("ICON").Icon = entry.Icon;
				var label = chip.Get<LabelWidget>("COUNT");
				label.Bounds.Width = width - 19;
				label.GetText = () => count;
				var text = FluentProvider.GetMessage(ChipText, "name", entry.Name, "count", entry.Count);
				chip.GetTooltipText = () => text;
				var e = entry;
				chip.OnClick = () =>
				{
					if (e.Cell != CPos.Zero)
						ctx.Locate?.Invoke(e.ActorId, e.Cell);
				};

				x += width + 2;
			}
		}
	}
}
