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
using OpenRA.Graphics;
using OpenRA.Mods.City.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.City
{
	/// <summary>Rectangle zone paint tool. ZoneType.None dezones.</summary>
	public class ZoneOrderGenerator : CityDragOrderGenerator
	{
		static readonly Color[] ZoneColors =
		[
			Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF), // None (dezone)
			Color.FromArgb(0x90, 0x7E, 0xD9, 0x57), // ResidentialLow
			Color.FromArgb(0x90, 0x2E, 0x9E, 0x3E), // ResidentialHigh
			Color.FromArgb(0x90, 0x5A, 0xB4, 0xF0), // CommercialLow
			Color.FromArgb(0x90, 0x2C, 0x6F, 0xD1), // CommercialHigh
			Color.FromArgb(0x90, 0xF2, 0xC9, 0x4C), // Industrial
			Color.FromArgb(0x90, 0xB0, 0x7C, 0xE8), // Office
			Color.FromArgb(0x90, 0xA8, 0xE0, 0x63), // ResidentialRow
			Color.FromArgb(0x90, 0x4F, 0xBF, 0x4F), // ResidentialMedium
			Color.FromArgb(0x90, 0x8F, 0xD3, 0xC8), // ResidentialMixed
			Color.FromArgb(0x90, 0x2F, 0x6F, 0x3F), // ResidentialLowRent
			Color.FromArgb(0x90, 0x7A, 0x4F, 0xC4), // OfficeHigh
			Color.FromArgb(0x90, 0xC9, 0xA2, 0x27), // Warehouse
		];

		static readonly Color ZoneInvalidColor = Color.FromArgb(0x50, 0xE0, 0x30, 0x30);

		public readonly ZoneType Zone;
		readonly ZoneLayer zoneLayer;

		public ZoneOrderGenerator(World world, ZoneType zone)
			: base(world)
		{
			Zone = zone;
			zoneLayer = world.WorldActor.TraitOrDefault<ZoneLayer>();
		}

		protected override IEnumerable<Order> OnDragComplete(World w, CPos start, CPos end)
		{
			if (w.LocalPlayer == null)
				yield break;

			yield return CityOrders.ZoneOrder(w.LocalPlayer, start, end, Zone);
		}

		protected override IEnumerable<IRenderable> RenderPreviewAnnotations(WorldRenderer wr, World w)
		{
			if (zoneLayer == null || !w.Map.Contains(HoverCell))
				yield break;

			var color = ZoneColors[Math.Min((int)Zone, ZoneColors.Length - 1)];
			foreach (var cell in CityUtils.Rect(PreviewStart, PreviewEnd))
			{
				if (!w.Map.Contains(cell))
					continue;

				var ok = Zone == ZoneType.None ? zoneLayer.GetZone(cell) != ZoneType.None : zoneLayer.CanZone(cell);
				yield return new MarkerTileRenderable(cell, ok ? color : ZoneInvalidColor);
			}
		}

		protected override string GetCursorName(World w, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return ConstructionUtils.Cursor("city-zone", "default");
		}
	}
}
