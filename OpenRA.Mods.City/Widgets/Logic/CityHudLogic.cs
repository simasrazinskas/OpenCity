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

using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>
	/// Root logic of the in-game HUD. Loads the panels from city-panels.yaml and centres the camera on
	/// the first highway connection when the game starts.
	/// </summary>
	public class CityHudLogic : ChromeLogic
	{
		static readonly string[] Panels =
		[
			"CITY_BUILDING_PANEL",
			"CITY_CITIZEN_PANEL",
			"CITY_BUDGET_PANEL",
			"CITY_INFOVIEWS_PANEL",
			"CITY_STATS_PANEL",
			"CITY_CHIRPER_PANEL",
			"CITY_PRODUCTION_PANEL",
			"CITY_POLICIES_PANEL",
			"CITY_PROGRESS_PANEL",
			"CITY_DISTRICTS_PANEL",
			"CITY_TRANSIT_PANEL"
		];

		readonly World world;
		readonly WorldRenderer worldRenderer;
		bool centered;

		[ObjectCreator.UseCtor]
		public CityHudLogic(Widget widget, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;

			var ctx = CityUiContext.For(world);
			ctx.CenterOn = cell => worldRenderer.Viewport.Center(world.Map.CenterOfCell(cell));

			foreach (var panel in Panels)
				Game.LoadWidget(world, panel, widget, []);

			widget.Get<LogicTickerWidget>("CITY_TICKER").OnTick = () =>
			{
				if (centered)
					return;

				centered = true;
				CenterOnHighway();
			};
		}

		void CenterOnHighway()
		{
			Actor first = null;
			OutsideConnection connection = null;
			foreach (var tp in world.ActorsWithTrait<OutsideConnection>())
			{
				if (first == null || tp.Actor.ActorID < first.ActorID)
				{
					first = tp.Actor;
					connection = tp.Trait;
				}
			}

			if (first == null)
				return;

			var length = connection.Info.Length - 1;
			var direction = connection.Info.Direction;
			var end = first.Location + new CVec(direction.X * length, direction.Y * length);
			worldRenderer.Viewport.Center(world.Map.CenterOfCell(end));
		}
	}
}
