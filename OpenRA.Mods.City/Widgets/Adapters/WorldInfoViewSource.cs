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
using OpenRA.Mods.City.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Info view values from the industry and progression WPs: natural resources (by kind and richness), freight load on the
	/// roads, company stock fill of production buildings and tourist attractions. Transit coverage comes from the transit layer itself.
	/// </summary>
	public sealed class WorldInfoViewSource : IInfoViewSource
	{
		public const int ResourceKinds = 6;

		readonly World world;
		readonly NaturalResourceLayer resources;
		readonly Logistics logistics;
		readonly CityEconomy economy;
		readonly Progression progression;
		readonly IPropertyRegistry properties;

		readonly Dictionary<CPos, int> tourism = [];
		int tourismStamp = -1;

		public WorldInfoViewSource(World world, NaturalResourceLayer resources, Logistics logistics, CityEconomy economy, Progression progression,
			IPropertyRegistry properties)
		{
			this.world = world;
			this.resources = resources;
			this.logistics = logistics;
			this.economy = economy;
			this.progression = progression;
			this.properties = properties;
		}

		public bool Supports(CityInfoView mode)
		{
			switch (mode)
			{
				case CityInfoView.NaturalResources: return resources != null;
				case CityInfoView.Freight: return logistics != null;
				case CityInfoView.Production: return economy != null && properties != null;
				case CityInfoView.Tourism: return progression != null;
				default: return false;
			}
		}

		// Freight, stock and attractions change every pulse; a coarse stamp keeps the overlay refreshing without a version of its own.
		public int Version => (resources?.Version ?? 0) + world.WorldTick / 50;

		/// <summary>Resource view cell value: kind * 16 + colour step 0..10 (see InfoRamp.Resource).</summary>
		public static string KindName(int kind)
		{
			return CityUi.Message("label-resource-kind-" + ((NaturalResourceKind)kind).ToString().ToLowerInvariant(), ((NaturalResourceKind)kind).ToString());
		}

		public int GetCell(CityInfoView mode, CPos cell)
		{
			switch (mode)
			{
				case CityInfoView.NaturalResources: return Resource(cell);
				case CityInfoView.Freight:
					var load = logistics.GetFreightLoad(cell);
					return load > 0 ? load : -1;
				case CityInfoView.Production: return Production(cell);
				default: return Tourism(cell);
			}
		}

		int Resource(CPos cell)
		{
			var bestKind = -1;
			var bestFrame = -1;
			for (var kind = 0; kind < ResourceKinds; kind++)
			{
				var frame = resources.GetHeatFrame((NaturalResourceKind)kind, cell);
				if (frame > bestFrame)
				{
					bestFrame = frame;
					bestKind = kind;
				}
			}

			return bestFrame < 0 ? -1 : bestKind * 16 + bestFrame;
		}

		int Production(CPos cell)
		{
			var property = properties.GetAt(cell);
			if (property == null || property.CompanyId == 0)
				return -1;

			var company = economy.CompanyOf(property.Id);
			if (company == null || company.StockCap <= 0 || company.Output == 0)
				return -1;

			return Math.Clamp(company.StockOut * 100 / company.StockCap, 0, 100);
		}

		int Tourism(CPos cell)
		{
			var stamp = world.WorldTick / 100;
			if (stamp != tourismStamp)
			{
				tourismStamp = stamp;
				RebuildTourism();
			}

			return tourism.TryGetValue(cell, out var value) ? value : -1;
		}

		void RebuildTourism()
		{
			tourism.Clear();
			var best = 1;
			foreach (var tp in world.ActorsWithTrait<ProgressionValue>())
				best = Math.Max(best, tp.Trait.Info.Attractiveness);

			foreach (var tp in world.ActorsWithTrait<ProgressionValue>())
			{
				var attractiveness = tp.Trait.Info.Attractiveness;
				if (attractiveness <= 0 || tp.Actor.IsDead || !tp.Actor.IsInWorld)
					continue;

				var value = Math.Max(15, attractiveness * 100 / best);
				foreach (var (cell, _) in tp.Actor.OccupiesSpace.OccupiedCells())
					tourism[cell] = value;
			}

			// Where visitors arrive.
			foreach (var cell in progression.ArrivalCells)
				tourism[cell] = 100;
		}
	}
}
