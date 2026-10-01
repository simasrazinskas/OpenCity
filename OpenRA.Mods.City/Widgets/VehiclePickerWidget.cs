#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option)
 * any later version. For more information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// A transparent widget over the map that turns a left click on a vehicle (IVehicleInspector) into a vehicle selection. Vehicles
	/// are not actors, so the normal selection never sees them. Clicks that miss a vehicle, or happen while a tool is active, pass
	/// through to the world.
	/// </summary>
	public class VehiclePickerWidget : Widget
	{
		const int PickRadius = 640;

		readonly World world;
		readonly WorldRenderer worldRenderer;

		[ObjectCreator.UseCtor]
		public VehiclePickerWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
		}

		protected VehiclePickerWidget(VehiclePickerWidget other)
			: base(other)
		{
			world = other.world;
			worldRenderer = other.worldRenderer;
		}

		public override VehiclePickerWidget Clone() { return new VehiclePickerWidget(this); }

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Event != MouseInputEvent.Down || mi.Button != MouseButton.Left)
				return false;

			var ctx = CityUiContext.For(world);
			var inspector = ctx.Get<IVehicleInspector>();
			if (inspector == null || ctx.AnyToolActive())
				return false;

			var position = worldRenderer.ProjectedPosition(worldRenderer.Viewport.ViewToWorldPx(mi.Location));
			if (!inspector.TryGetVehicleAt(position, PickRadius, out var view))
				return false;

			ctx.SelectedVehicle = view.TripId;
			return true;
		}

		public override void Draw() { }
	}
}
