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
using System.Linq;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>The action column of the building window (go to, info view, upgrades, districts, hub areas).</summary>
	public partial class CityBuildingInfoLogic
	{
		[FluentReference]
		const string AreaPaint = "label-tool-area-paint";

		[FluentReference]
		const string AreaClear = "label-tool-area-clear";

		[FluentReference]
		const string AreaPaintDesc = "label-tool-area-paint-desc";

		[FluentReference]
		const string AreaClearDesc = "label-tool-area-clear-desc";

		[FluentReference("count")]
		const string AreaCells = "label-tool-cells-count";

		const int ActionStep = 28;

		readonly Widget actions;
		int actionsHeight;

		ExtractorHub Hub => actor?.TraitOrDefault<ExtractorHub>();

		static CityInfoView InfoViewOf(ServiceKind kind)
		{
			switch (kind)
			{
				case ServiceKind.Power: return CityInfoView.Power;
				case ServiceKind.Water: return CityInfoView.Water;
				case ServiceKind.Sewage: return CityInfoView.Sewage;
				case ServiceKind.Garbage: return CityInfoView.Garbage;
				case ServiceKind.Health: return CityInfoView.Health;
				case ServiceKind.Deathcare: return CityInfoView.Deathcare;
				case ServiceKind.Education: return CityInfoView.Education;
				case ServiceKind.Police: return CityInfoView.Police;
				case ServiceKind.Fire: return CityInfoView.Fire;
				case ServiceKind.Parks: return CityInfoView.Parks;
				case ServiceKind.Telecom: return CityInfoView.Telecom;
				case ServiceKind.Post: return CityInfoView.Post;
				default: return CityInfoView.None;
			}
		}

		ButtonWidget AddAction(string icon, string title, string desc, Action click, Func<bool> highlighted = null)
		{
			var button = (ButtonWidget)Game.LoadWidget(world, "CITY_BUILDING_ACTION", actions, []);
			button.Bounds.Y = actionsHeight;
			actionsHeight += ActionStep;
			button.Get<CityIconWidget>("ICON").Icon = icon;
			var text = string.IsNullOrEmpty(desc) ? title : title + "\n" + desc;
			button.GetTooltipText = () => text;
			button.OnClick = click;
			if (highlighted != null)
				button.IsHighlighted = highlighted;

			return button;
		}

		void BuildActions()
		{
			AddAction("ui_locate", Msg("label-inspect-goto"), Msg("label-inspect-goto-desc"), Locate);

			var service = actor.Info.TraitInfoOrDefault<ServiceBuildingInfo>();
			var view = service != null ? InfoViewOf(service.Kind) : CityInfoView.None;
			var layer = InfoViewLayer.Get(world);
			var def = view != CityInfoView.None ? InfoViews.Get(view) : null;
			if (layer != null && def != null)
			{
				AddAction("info_" + def.Id, Msg("label-inspect-infoview"), FluentProvider.GetMessage("label-inspect-infoview-desc", "name", def.Name),
					() => layer.Mode = layer.Mode == view ? CityInfoView.None : view, () => layer.Mode == view);
			}

			if (world.LocalPlayer == null)
				return;

			if (ctx.Upgrades?.UpgradesOf(actor).Count > 0)
				AddAction("tool_upgrade", Msg("label-inspect-upgrades"), Msg("label-inspect-upgrades-desc"), () => ShowPage(Page.Upkeep));

			if (HasDistricts)
				AddAction("cat_districts", Msg("label-inspect-districts"), Msg("label-inspect-districts-desc"), () => ShowPage(Page.Upkeep));

			var hub = Hub;
			if (hub != null)
			{
				var hubActor = actor;
				AddAction("tool_area_paint", FluentProvider.GetMessage(AreaPaint), FluentProvider.GetMessage(AreaPaintDesc), () => StartArea(hubActor, true));
				AddAction("tool_area_clear", FluentProvider.GetMessage(AreaClear), FluentProvider.GetMessage(AreaClearDesc), () => StartArea(hubActor, false));
			}
		}

		bool HasDistricts
		{
			get
			{
				var districts = ctx.ProgressionUi?.Districts;
				return districts != null && districts.Count > 0 && ctx.Get<ServiceSimulation>() != null && actor.Info.TraitInfoOrDefault<ServiceBuildingInfo>() != null;
			}
		}

		void StartArea(Actor hub, bool add)
		{
			ctx.ActivateTool(add ? "area-paint" : "area-clear", new UiAreaToolGenerator(world,
				(p, a, b) => UiOrders.HubArea(p, hub, a, b, add),
				add ? Color.FromArgb(110, 120, 220, 90) : Color.FromArgb(120, 255, 150, 30),
				(a, b) => FluentProvider.GetMessage(AreaCells, "count", CityUtils.Rect(a, b).Count())));
		}
	}
}
