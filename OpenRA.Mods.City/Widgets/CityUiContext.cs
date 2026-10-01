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
using System.Linq;
using System.Runtime.CompilerServices;
using OpenRA.Mods.City.Traits;
using OpenRA.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Finds the simulation providers of CityInterfaces.cs (and the UI's own provider interfaces) for one world, once.
	/// Every provider may be null: panels and tools hide themselves then. Read-only; UI code never mutates the sim.
	/// </summary>
	public sealed class CityUiContext
	{
		static readonly ConditionalWeakTable<World, CityUiContext> Cache = [];

		readonly World world;
		readonly Dictionary<Type, object> found = [];
		readonly Dictionary<string, bool> features = [];
		readonly List<IInspectionContributor> contributors;
		readonly List<IInfoViewSource> sources;

		CityUiContext(World world)
		{
			this.world = world;
			contributors = Find<IInspectionContributor>(all: true).ToList();
			sources = Find<IInfoViewSource>(all: true).ToList();

			// Adapters over the simulation traits that do not implement the UI's provider interfaces themselves.
			contributors.Add(new InspectionAdapter(Get<ServiceSimulation>(), Get<Logistics>(), Get<CityEconomy>(), Get<PropertyRegistry>()));
			sources.Add(new WorldInfoViewSource(world, Get<NaturalResourceLayer>(), Get<Logistics>(), Get<CityEconomy>(), Get<Progression>(), Get<IPropertyRegistry>()));
		}

		public static CityUiContext For(World world)
		{
			if (!Cache.TryGetValue(world, out var context))
			{
				context = new CityUiContext(world);
				Cache.Add(world, context);
			}

			return context;
		}

		public ICitizenPopulation Citizens => Get<ICitizenPopulation>();
		public ITrafficService Traffic => Get<ITrafficService>();
		public ICityEconomy Economy => Get<ICityEconomy>();
		public ICityServices Services => Get<ICityServices>();
		public IUtilityNetwork Utilities => Get<IUtilityNetwork>();
		public IPollutionMap Pollution => Get<IPollutionMap>();
		public ICityStatistics Statistics => Get<ICityStatistics>();
		public IDemandModel Demand => Get<IDemandModel>();
		public IProgression Progression => Get<IProgression>();
		public IPropertyRegistry Properties => Get<IPropertyRegistry>();
		public IRoadNetwork Roads => Get<IRoadNetwork>();
		public IChirperSource Chirper => Get<IChirperSource>() ?? Statistics as IChirperSource;
		public IEconomyUiSource EconomyUi => Adapt<IEconomyUiSource>(() =>
			Get<CityEconomy>() is { } e ? new EconomyUiAdapter(e, CityUi.GetManager(world), world, Get<IPropertyRegistry>(), Get<Logistics>()) : null);
		public IProgressionUiSource ProgressionUi => Adapt<IProgressionUiSource>(() => Get<Progression>() is { } p ? new ProgressionUiAdapter(world, p) : null);
		public ITransitUiSource TransitUi => Adapt<ITransitUiSource>(() => Get<TransitLayer>() is { } t ? new TransitUiAdapter(world, t) : null);
		public IExtractorSource Extractors => Get<IExtractorSource>();
		public ICityProblems Problems => Get<ICityProblems>();
		public IFollowSource Follow => Get<IFollowSource>();
		public IAchievementSource Achievements => Adapt<IAchievementSource>(() => Get<Progression>() is { } p ? new ProgressionExtrasAdapter(world, p) : null);
		public ITileInfoSource TileInfo => Adapt<ITileInfoSource>(() => Get<Progression>() is { } p ? new ProgressionExtrasAdapter(world, p) : null);
		public IUpgradeSource Upgrades => Adapt<IUpgradeSource>(() => Get<ServiceSimulation>() is { } s ? new UpgradeAdapter(s) : null);
		public IReadOnlyList<IInspectionContributor> Contributors => contributors;

		/// <summary>Citizen shown in the citizen panel (0 = none). Local UI state, never synced.</summary>
		public int SelectedCitizen { get; set; }

		/// <summary>District selected in the districts panel (0 = none); the policies panel edits its policies.</summary>
		public int SelectedDistrict { get; set; }

		/// <summary>Trip id of the vehicle shown in the vehicle card (0 = none).</summary>
		public int SelectedVehicle { get; set; }

		/// <summary>Whether any tool (build, road, zone, ...) is the active order generator; the vehicle picker then leaves clicks alone.</summary>
		public Func<bool> AnyToolActive { get; set; } = () => false;

		/// <summary>Tile under the cursor while the tile tool is active (-1 = none).</summary>
		public int HoverTileX { get; set; } = -1;
		public int HoverTileY { get; set; } = -1;

		/// <summary>When set, the HUD keeps the camera on this world position every frame (cancelled by Escape or by scrolling).</summary>
		public Func<WPos?> FollowTarget { get; set; }

		/// <summary>Centres the camera on a world position. Set by the HUD logic that owns the WorldRenderer.</summary>
		public Action<WPos> CenterOnWorld { get; set; }

		/// <summary>Selects an actor and centres the camera on it (alert strip, locate buttons).</summary>
		public Action<uint, CPos> Locate { get; set; }

		/// <summary>Centres the camera on a cell. Set by the HUD logic that owns the WorldRenderer.</summary>
		public Action<CPos> CenterOn { get; set; }

		/// <summary>Starts a tool (id, order generator), cancelling the running tool and closing panels. Set by the toolbar logic.</summary>
		public Action<string, IOrderGenerator> ActivateTool { get; set; }

		/// <summary>Whether the tool with this id is the active order generator. Set by the toolbar logic.</summary>
		public Func<string, bool> IsToolActive { get; set; } = _ => false;

		/// <summary>Cancels the active tool. Set by the toolbar logic.</summary>
		public Action CancelTool { get; set; } = () => { };
		public IReadOnlyList<IInfoViewSource> Sources => sources;

		/// <summary>The legacy coverage layer (service coverage, land value, pollution), or null.</summary>
		public CityCoverageLayer CoverageLayer => Get<CityCoverageLayer>();
		public bool Coverage => CoverageLayer != null;

		/// <summary>The provider of type T on the world actor or the local player actor, or null.</summary>
		public T Get<T>() where T : class
		{
			if (found.TryGetValue(typeof(T), out var cached))
				return (T)cached;

			var result = Find<T>(all: false).FirstOrDefault();
			found[typeof(T)] = result;
			return result;
		}

		T Adapt<T>(Func<T> make) where T : class
		{
			if (found.TryGetValue(typeof(T), out var cached))
				return (T)cached;

			var result = Find<T>(all: false).FirstOrDefault() ?? make();
			found[typeof(T)] = result;
			return result;
		}

		IEnumerable<T> Find<T>(bool all) where T : class
		{
			var worldMatches = world.WorldActor.TraitsImplementing<T>();
			var player = world.LocalPlayer;
			var playerMatches = player != null ? player.PlayerActor.TraitsImplementing<T>() : [];
			var matches = worldMatches.Concat(playerMatches);
			return all ? matches : matches.Take(1);
		}

		/// <summary>
		/// Whether a simulation feature exists, judged by the trait infos on the world and player actors, so the UI
		/// compiles without the other work packages. Names are the trait info class names (e.g. "TransitToolsInfo").
		/// </summary>
		public bool HasTrait(params string[] infoNames)
		{
			var key = string.Join("|", infoNames);
			if (features.TryGetValue(key, out var value))
				return value;

			value = Scan(world.WorldActor.Info, infoNames);
			if (!value && world.LocalPlayer != null)
				value = Scan(world.LocalPlayer.PlayerActor.Info, infoNames);

			features[key] = value;
			return value;
		}

		static bool Scan(ActorInfo info, string[] infoNames)
		{
			foreach (var trait in info.TraitInfos<TraitInfo>())
			{
				var name = trait.GetType().Name;
				foreach (var wanted in infoNames)
					if (name == wanted)
						return true;
			}

			return false;
		}

		/// <summary>All trait infos with this class name on the world actor (e.g. every "RoadTypeInfo@..." instance), yaml order.</summary>
		public IEnumerable<TraitInfo> WorldTraitInfos(string infoName)
		{
			return world.WorldActor.Info.TraitInfos<TraitInfo>().Where(t => t.GetType().Name == infoName);
		}
	}
}
