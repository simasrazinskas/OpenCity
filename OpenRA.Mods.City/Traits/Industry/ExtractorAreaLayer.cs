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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	public enum AreaCellStatus : byte
	{
		/// <summary>Valid and the cell has resource value.</summary>
		Ok,

		/// <summary>Valid, but no output (flag yellow: CS2 allows resource-less areas with less income).</summary>
		NoResource,

		AlreadyInArea, OffMap, OutOfRange, Terrain, Road, Blocked, OtherHub, HubFootprint, LimitReached,
	}

	/// <summary>Result of planning an area paint or erase. The preview and the order handler use the same plan, so they always agree.</summary>
	public sealed class AreaPlan
	{
		public ExtractorHub Hub;
		public bool Add;

		/// <summary>Cells that will be added (or removed), row-major.</summary>
		public readonly List<CPos> Cells = [];

		/// <summary>Per-cell verdict for every cell of the dragged rectangle (preview colours).</summary>
		public readonly List<(CPos Cell, AreaCellStatus Status)> Verdicts = [];

		/// <summary>Trees that must be cleared (non-forestry areas).</summary>
		public readonly List<Actor> Trees = [];

		public int Cost;
		public int ResourceCells;
		public int EmptyCells;

		public bool IsEmpty => Cells.Count == 0;
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Binds map cells to extractor hubs (design/06 3.2) and implements IExtractorSource for the economy.")]
	public class ExtractorAreaLayerInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new ExtractorAreaLayer(init.Self); }
	}

	public class ExtractorAreaLayer : IExtractorSource, ISync, ICityAutoTestReporter
	{
		readonly World world;
		readonly CellLayer<uint> hubAt;
		readonly List<ExtractorHub> hubs = [];

		public ExtractorAreaLayer(Actor self)
		{
			world = self.World;
			hubAt = new CellLayer<uint>(world.Map);
		}

		/// <summary>Bumped on every area change or hub add/remove (overlay and info view caches).</summary>
		public int Version { get; private set; }

		/// <summary>A cell joined or left a hub's area.</summary>
		public event Action<CPos> CellChanged;

		/// <summary>All hubs ordered by ActorID.</summary>
		public IReadOnlyList<ExtractorHub> Hubs => hubs;

		[VerifySync]
		public int StateHash
		{
			get
			{
				unchecked
				{
					var h = 23;
					foreach (var hub in hubs)
					{
						h = h * 31 + (int)hub.Actor.ActorID;
						h = h * 31 + IndustryHash.HashString(hub.Product);
						h = h * 31 + hub.CellList.Count;
						h = h * 31 + hub.CapMilliPerDay;
						h = h * 31 + hub.TotalProducedMilli;
						foreach (var c in hub.CellList)
							h = h * 31 + c.X * 4099 + c.Y;
					}

					return h;
				}
			}
		}

		internal void RegisterHub(ExtractorHub hub)
		{
			if (hubs.Contains(hub))
				return;

			var i = hubs.Count;
			while (i > 0 && hubs[i - 1].Actor.ActorID > hub.Actor.ActorID)
				i--;

			hubs.Insert(i, hub);
			Version++;
		}

		internal void UnregisterHub(ExtractorHub hub)
		{
			if (!hubs.Remove(hub))
				return;

			foreach (var c in hub.CellList.ToArray())
				Release(hub, c);

			hub.CellList.Clear();
			Version++;
		}

		void Release(ExtractorHub hub, CPos cell)
		{
			if (hubAt.Contains(cell) && hubAt[cell] == hub.Actor.ActorID)
			{
				hubAt[cell] = 0;
				CellChanged?.Invoke(cell);
			}
		}

		/// <summary>ActorID of the hub whose area covers the cell, 0 = none. ZoneGrowth must not grow on cells where this is non-zero.</summary>
		public uint GetHub(CPos cell)
		{
			return hubAt.Contains(cell) ? hubAt[cell] : 0;
		}

		public ExtractorHub GetHubByActorId(uint actorId)
		{
			for (var i = 0; i < hubs.Count; i++)
				if (hubs[i].Actor.ActorID == actorId)
					return hubs[i];

			return null;
		}

		/// <summary>Hub covering the cell, or null.</summary>
		public ExtractorHub GetHubAt(CPos cell)
		{
			var id = GetHub(cell);
			return id == 0 ? null : GetHubByActorId(id);
		}

		/// <summary>Area cells of a hub in row-major order (empty if the hub is unknown).</summary>
		public IReadOnlyList<CPos> HubCells(uint hubActorId)
		{
			return GetHubByActorId(hubActorId)?.Cells ?? [];
		}

		static bool IsTree(Actor a) { return a.Info.Name.StartsWith("tree-", StringComparison.Ordinal); }

		static bool InRing(ExtractorHub hub, CPos cell)
		{
			var radius = hub.Info.Radius;
			var cells = hub.Actor.OccupiesSpace?.OccupiedCells();
			if (cells == null || cells.Length == 0)
				return Math.Max(Math.Abs(cell.X - hub.Actor.Location.X), Math.Abs(cell.Y - hub.Actor.Location.Y)) <= radius;

			foreach (var (c, _) in cells)
				if (Math.Max(Math.Abs(cell.X - c.X), Math.Abs(cell.Y - c.Y)) <= radius)
					return true;

			return false;
		}

		static bool InFootprint(ExtractorHub hub, CPos cell)
		{
			var cells = hub.Actor.OccupiesSpace?.OccupiedCells();
			if (cells == null)
				return hub.Actor.Location == cell;

			foreach (var (c, _) in cells)
				if (c == cell)
					return true;

			return false;
		}

		/// <summary>Verdict for a single cell. `pending` counts cells already planned in this paint (for the cell limit).</summary>
		public AreaCellStatus CheckCell(ExtractorHub hub, CPos cell, int pending, List<Actor> trees)
		{
			var map = world.Map;
			if (!map.Contains(cell))
				return AreaCellStatus.OffMap;

			var owner = hubAt[cell];
			if (owner == hub.Actor.ActorID)
				return AreaCellStatus.AlreadyInArea;

			if (owner != 0)
				return AreaCellStatus.OtherHub;

			if (InFootprint(hub, cell))
				return AreaCellStatus.HubFootprint;

			if (!InRing(hub, cell))
				return AreaCellStatus.OutOfRange;

			var terrain = map.GetTerrainInfo(cell).Type;
			if (hub.Info.Kind == NaturalResourceKind.Fish ? terrain != "Water" : terrain != "Clear" && terrain != "Rough")
				return AreaCellStatus.Terrain;

			var roads = world.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			if (roads != null && roads.IsRoad(cell))
				return AreaCellStatus.Road;

			foreach (var a in world.ActorMap.GetActorsAt(cell))
			{
				if (!IsTree(a))
					return AreaCellStatus.Blocked;

				if (hub.Info.Kind != NaturalResourceKind.Forest)
					trees?.Add(a);
			}

			if (hub.CellList.Count + pending >= hub.Info.MaxAreaCells)
				return AreaCellStatus.LimitReached;

			return hub.HasResource(cell) ? AreaCellStatus.Ok : AreaCellStatus.NoResource;
		}

		/// <summary>Plans painting (add) or erasing (remove) the rectangle between two corners.</summary>
		public AreaPlan Plan(ExtractorHub hub, CPos from, CPos to, bool add)
		{
			var plan = new AreaPlan { Hub = hub, Add = add };
			foreach (var cell in CityUtils.Rect(from, to))
			{
				if (!world.Map.Contains(cell))
					continue;

				if (!add)
				{
					var inArea = hubAt[cell] == hub.Actor.ActorID;
					plan.Verdicts.Add((cell, inArea ? AreaCellStatus.Ok : AreaCellStatus.AlreadyInArea));
					if (inArea)
						plan.Cells.Add(cell);

					continue;
				}

				var status = CheckCell(hub, cell, plan.Cells.Count, plan.Trees);
				plan.Verdicts.Add((cell, status));
				if (status == AreaCellStatus.Ok || status == AreaCellStatus.NoResource)
				{
					plan.Cells.Add(cell);
					if (status == AreaCellStatus.Ok)
						plan.ResourceCells++;
					else
						plan.EmptyCells++;
				}
			}

			if (add)
				plan.Cost = plan.Cells.Count * hub.Info.CostPerCell + plan.Trees.Count * hub.Info.ClearTreeCost;

			return plan;
		}

		/// <summary>Applies a plan (synced callers only; payment is the caller's job).</summary>
		public void Apply(AreaPlan plan)
		{
			var hub = plan.Hub;
			var zones = world.WorldActor.TraitOrDefault<ZoneLayer>();
			foreach (var c in plan.Cells)
			{
				if (plan.Add)
				{
					hubAt[c] = hub.Actor.ActorID;
					hub.CellList.Add(c);
					zones?.SetZone(c, ZoneType.None);
				}
				else
				{
					hubAt[c] = 0;
					hub.CellList.Remove(c);
				}

				CellChanged?.Invoke(c);
			}

			if (plan.Add)
				hub.CellList.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

			if (plan.Trees.Count > 0)
			{
				var trees = plan.Trees.ToArray();
				world.AddFrameEndTask(_ =>
				{
					foreach (var t in trees)
						if (!t.Disposed)
							t.Dispose();
				});
			}

			hub.RefreshCapacity();
			hub.SyncProps();
			Version++;
		}

		/// <summary>Re-draws a hub's area (product changed).</summary>
		internal void NotifyChanged(ExtractorHub hub)
		{
			foreach (var c in hub.CellList)
				CellChanged?.Invoke(c);

			Version++;
		}

		public ExtractorHub HubOfProperty(int propertyId)
		{
			var registry = world.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			var actor = registry?.Get(propertyId)?.Actor;
			return actor == null || actor.Disposed ? null : actor.TraitOrDefault<ExtractorHub>();
		}

		int IExtractorSource.Product(int propertyId) { return HubOfProperty(propertyId)?.ProductId ?? 0; }

		int IExtractorSource.CapacityMilliPerDay(int propertyId) { return HubOfProperty(propertyId)?.CapMilliPerDay ?? 0; }

		/// <summary>Whole units produced (the economy should accumulate fractions, or call OnProducedMilli).</summary>
		void IExtractorSource.OnProduced(int propertyId, int units) { OnProducedMilli(propertyId, units * 1000); }

		/// <summary>Milli-unit variant of IExtractorSource.OnProduced.</summary>
		public void OnProducedMilli(int propertyId, int milli) { HubOfProperty(propertyId)?.OnProducedMilli(milli); }

		/// <summary>Jobs (by hub size and richness) the economy may give the extractor company.</summary>
		public int JobsMax(int propertyId) { return HubOfProperty(propertyId)?.JobsMax ?? 0; }

		/// <summary>Clock days of ore/oil left, -1 for renewables or unknown.</summary>
		public int MonthsLeft(int propertyId) { return HubOfProperty(propertyId)?.MonthsLeft ?? -1; }

		string ICityAutoTestReporter.AutoTestReport()
		{
			var cells = 0;
			long cap = 0;
			var jobs = 0;
			long produced = 0;
			var felled = 0;
			foreach (var h in hubs)
			{
				cells += h.CellList.Count;
				cap += h.CapMilliPerDay;
				jobs += h.JobsMax;
				produced += h.TotalProducedMilli;
				felled += h.TreesFelled;
			}

			var registry = world.WorldActor.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			var detail = string.Join(" ", hubs.Select(h =>
			{
				var pr = registry?.GetByActor(h.Actor);
				var prop = pr == null ? string.Empty : $"/{pr.Kind}{(pr.Operational ? string.Empty : "!op")}{(pr.HasRoadAccess ? string.Empty : "!road")}/co{pr.CompanyId}";
				return $"{h.Actor.Info.Name}#{h.Actor.ActorID}:{h.Product}/{h.CellList.Count}c/{h.CapMilliPerDay / 1000}u/{h.JobsMax}j{prop}";
			}));
			return $"extractors hubs={hubs.Count} cells={cells} cap/day={cap / 1000} jobs={jobs} produced={produced / 1000} felled={felled} hash={StateHash}" +
				(hubs.Count > 0 ? $" [{detail}]" : string.Empty);
		}
	}
}
