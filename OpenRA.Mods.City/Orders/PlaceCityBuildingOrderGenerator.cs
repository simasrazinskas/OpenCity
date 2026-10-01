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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Orders;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>
	/// Placement tool for CityPlaceable actors. Shows a translucent ghost of the actor (sequence "idle", frame 0) with its footprint
	/// in green / red. Left click issues <see cref="CityOrders.PlaceBuilding"/> and the tool stays active for repeated placement;
	/// right click or Escape cancels. The cursor cell is the footprint's centre (top-left for 1x1 and 2x2).
	/// Cursors: "city-place" / "city-blocked" (fall back to "default" / "generic-blocked").
	/// </summary>
	public class PlaceCityBuildingOrderGenerator : IOrderGenerator
	{
		const float GhostAlpha = 0.6f;

		readonly World world;
		readonly string actorType;
		readonly GameSettings gameSettings = Game.Settings.Game;
		readonly ActorInfo actorInfo;
		readonly BuildingInfo buildingInfo;
		readonly CityPlaceableInfo placeable;
		readonly RoadLayer roads;
		readonly CityManager cityManager;
		readonly CVec centreOffset;

		PlacementCheck cachedCheck;
		CPos cachedTopLeft;
		int cachedNetworkVersion = -1;
		int cachedTick = -1;
		CPos hoverCell;

		public PlaceCityBuildingOrderGenerator(World world, string actorType)
		{
			this.world = world;
			this.actorType = actorType;
			world.Map.Rules.Actors.TryGetValue(actorType, out actorInfo);
			buildingInfo = actorInfo?.TraitInfoOrDefault<BuildingInfo>();
			placeable = actorInfo?.TraitInfoOrDefault<CityPlaceableInfo>();
			roads = world.WorldActor.TraitOrDefault<RoadLayer>();
			cityManager = world.LocalPlayer?.PlayerActor.TraitOrDefault<CityManager>();
			if (buildingInfo != null)
				centreOffset = new CVec((buildingInfo.Dimensions.X - 1) / 2, (buildingInfo.Dimensions.Y - 1) / 2);

			if (gameSettings.MouseControlStyle == MouseControlStyle.Classic)
				world.Selection.Clear();
		}

		public MouseButton ActionButton => gameSettings.ResolveActionButton(MouseActionType.PlaceBuilding);
		public MouseButton CancelButton => gameSettings.ResolveCancelButton(MouseActionType.PlaceBuilding);

		bool Usable => buildingInfo != null && placeable != null;

		CPos TopLeftFor(CPos cell) { return cell - centreOffset; }

		PlacementCheck Check(CPos topLeft)
		{
			var version = roads?.NetworkVersion ?? 0;
			if (cachedCheck == null || cachedTopLeft != topLeft || cachedNetworkVersion != version || cachedTick != world.WorldTick)
			{
				cachedCheck = ConstructionUtils.CheckPlacement(world, actorInfo, topLeft, roads);
				cachedTopLeft = topLeft;
				cachedNetworkVersion = version;
				cachedTick = world.WorldTick;
			}

			return cachedCheck;
		}

		string ProblemKey(PlacementCheck check)
		{
			if (!check.Valid)
				return check.ErrorKey;

			if (cityManager != null)
			{
				if (!cityManager.IsUnlocked(actorType))
					return ConstructionUtils.ErrorLocked;

				if (!cityManager.CanAfford(placeable.Cost + check.ClearCost))
					return ConstructionUtils.ErrorMoney;
			}

			return null;
		}

		IEnumerable<Order> IOrderGenerator.Order(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			hoverCell = cell;

			if (mi.Button == CancelButton && mi.Event == MouseInputEvent.Up)
			{
				w.CancelInputMode();
				return [];
			}

			if (mi.Button == ActionButton && mi.Event == MouseInputEvent.Down && Usable && w.LocalPlayer != null && w.Map.Contains(cell))
				return [CityOrders.PlaceBuildingOrder(w.LocalPlayer, actorType, TopLeftFor(cell))];

			return [];
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Down && e.Key == Keycode.ESCAPE)
			{
				world.CancelInputMode();
				return true;
			}

			return false;
		}

		void IOrderGenerator.Tick(World w)
		{
			if (!Usable)
				w.CancelInputMode();
		}

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World w) { return []; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World w)
		{
			if (!Usable)
				yield break;

			hoverCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			if (!w.Map.Contains(hoverCell))
				yield break;

			var topLeft = TopLeftFor(hoverCell);
			var check = Check(topLeft);
			var ok = ProblemKey(check) == null;

			var tiles = check.Tiles.Count > 0 ? check.Tiles : [.. buildingInfo.Tiles(topLeft)];
			foreach (var t in tiles)
				if (w.Map.Contains(t))
					yield return new CityTileMarkerRenderable(t, ok ? CityDragOrderGenerator.ValidColor : CityDragOrderGenerator.InvalidColor);

			var ghost = Ghost(wr, w, topLeft);
			if (ghost != null)
				yield return ghost;
		}

		IRenderable Ghost(WorldRenderer wr, World w, CPos topLeft)
		{
			var rs = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>();
			if (rs == null)
				return null;

			var image = rs.GetImage(actorInfo, null);
			var sequences = w.Map.Sequences;
			if (!sequences.HasSequence(image, "idle"))
				return null;

			var seq = sequences.GetSequence(image, "idle");
			var palette = wr.Palette(rs.Palette ?? "city");
			var pos = w.Map.CenterOfCell(topLeft) + buildingInfo.CenterOffset(w);
			return new SpriteRenderable(seq.GetSprite(0), pos, WVec.Zero, 0, palette, seq.Scale, GhostAlpha, Vector3.One, TintModifiers.None, false);
		}

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World w)
		{
			if (!Usable || !w.Map.Contains(hoverCell))
				yield break;

			var check = Check(TopLeftFor(hoverCell));
			var problem = ProblemKey(check);
			var text = CityUtils.FormatMoney(placeable.Cost + check.ClearCost);
			if (problem != null)
				text += " (" + FluentProvider.GetMessage(problem) + ")";

			var font = Game.Renderer.Fonts["Bold"];

			// Just below the footprint (a fixed UI gap, so the label never covers the preview at any zoom).
			var below = hoverCell + new CVec(0, buildingInfo.Dimensions.Y);
			var pos = w.Map.CenterOfCell(w.Map.Clamp(below)) - new WVec(0, 512, 0);
			yield return new CityAnnotationText(font, pos, new int2(0, 4), problem != null ? Color.OrangeRed : Color.White, text);
		}

		string IOrderGenerator.GetCursor(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (!Usable || !w.Map.Contains(cell))
				return ConstructionUtils.Cursor("city-blocked", "generic-blocked");

			return ProblemKey(Check(TopLeftFor(cell))) == null
				? ConstructionUtils.Cursor("city-place", "default")
				: ConstructionUtils.Cursor("city-blocked", "generic-blocked");
		}

		void IOrderGenerator.Deactivate() { }

		void IOrderGenerator.SelectionChanged(World w, IEnumerable<Actor> selected) { }
	}
}
