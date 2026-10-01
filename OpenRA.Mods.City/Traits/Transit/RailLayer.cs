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
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Rail network: one track per cell on its own layer, autotiled by neighbour mask. Rail never shares a cell with a road except a straight level crossing.",
		"A rail network is connected to the outside when it reaches the border of the map.")]
	public class RailLayerInfo : TraitInfo
	{
		[Desc("Construction cost per rail cell.")]
		public readonly int CostPerCell = 40;

		[Desc("Percent of the cost refunded when track is removed.")]
		public readonly int RefundPercent = 50;

		public readonly string Image = "rail";

		[Desc("Sequence with 18 frames: 0-15 neighbour mask (bit0 N, bit1 E, bit2 S, bit3 W),",
			"16 level crossing with horizontal rail, 17 level crossing with vertical rail.")]
		public readonly string Sequence = "rail";

		[PaletteReference]
		public readonly string Palette = "terrain";

		public override object Create(ActorInitializer init) { return new RailLayer(init.Self, this); }
	}

	/// <summary>What a rail or tram track drag will do. Shared by the order handler and the UI preview so they always agree.</summary>
	public sealed class TrackPlan
	{
		public List<CPos> Path = [];

		/// <summary>Cells that get new track (the affordable, valid prefix of the path, without existing track).</summary>
		public readonly List<CPos> Build = [];

		/// <summary>Cells of Build that are level crossings.</summary>
		public readonly List<CPos> Crossings = [];

		public int Cost;

		/// <summary>Fluent key explaining why the drag stops early, or null.</summary>
		public string ErrorKey;
	}

	public class RailLayer : IWorldLoaded, IRenderOverlay, INotifyActorDisposing
	{
		const byte RailFlag = 1;
		const byte CrossingFlag = 2;

		public const string ErrorBlocked = "notification-transit-rail-blocked";
		public const string ErrorCrossing = "notification-transit-rail-needs-crossing";
		public const string ErrorTerrain = "notification-transit-rail-terrain";
		public const string ErrorNotOwned = "notification-transit-not-owned";
		public const string ErrorMoney = "notification-transit-no-money";

		public readonly RailLayerInfo Info;
		readonly World world;
		readonly CellLayer<byte> flags;
		readonly CellLayer<bool> edgeConnected;
		TrackOverlay overlay;
		IRoadNetwork roads;
		IProgression progression;
		int edgeVersion = -1;
		bool disposed;

		public RailLayer(Actor self, RailLayerInfo info)
		{
			Info = info;
			world = self.World;
			flags = new CellLayer<byte>(world.Map);
			edgeConnected = new CellLayer<bool>(world.Map);
		}

		/// <summary>Bumped whenever track is added or removed.</summary>
		public int NetworkVersion { get; private set; }

		public int RailCellCount { get; private set; }

		public event Action<CPos> RailChanged;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			roads = w.WorldActor.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
			overlay = new TrackOverlay(w, wr, Info.Image, Info.Sequence, Info.Palette);
		}

		public bool IsRail(CPos cell) => flags.Contains(cell) && (flags[cell] & RailFlag) != 0;

		public bool IsCrossing(CPos cell) => flags.Contains(cell) && (flags[cell] & CrossingFlag) != 0;

		public bool CanEnter(CPos from, int dir) => true;

		/// <summary>True when the track at this cell belongs to a rail network that reaches the map border.</summary>
		public bool IsConnectedToEdge(CPos cell)
		{
			if (!IsRail(cell))
				return false;

			if (edgeVersion != NetworkVersion)
				RebuildEdge();

			return edgeConnected[cell];
		}

		bool IsBorder(CPos cell)
		{
			var m = cell.ToMPos(world.Map);
			var b = world.Map.Bounds;
			return m.U <= b.Left || m.U >= b.Right - 1 || m.V <= b.Top || m.V >= b.Bottom - 1;
		}

		/// <summary>Rail cells on the map border that belong to a network (where intercity trains enter), by ascending cell order.</summary>
		public List<CPos> BorderCells { get; } = [];

		void RebuildEdge()
		{
			edgeVersion = NetworkVersion;
			edgeConnected.Clear();
			BorderCells.Clear();
			var queue = new Queue<CPos>();
			foreach (var c in AllCells())
			{
				if (!IsBorder(c))
					continue;

				BorderCells.Add(c);
				edgeConnected[c] = true;
				queue.Enqueue(c);
			}

			while (queue.Count > 0)
			{
				var c = queue.Dequeue();
				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (IsRail(n) && !edgeConnected[n])
					{
						edgeConnected[n] = true;
						queue.Enqueue(n);
					}
				}
			}
		}

		/// <summary>All track cells in row-major order (deterministic).</summary>
		public IEnumerable<CPos> AllCells()
		{
			var b = world.Map.Bounds;
			for (var v = b.Top; v < b.Bottom; v++)
			{
				for (var u = b.Left; u < b.Right; u++)
				{
					var c = new MPos(u, v).ToCPos(world.Map);
					if (IsRail(c))
						yield return c;
				}
			}
		}

		IProgression Progression()
		{
			if (progression != null)
				return progression;

			progression = world.WorldActor.TraitsImplementing<IProgression>().FirstOrDefault();
			if (progression != null)
				return progression;

			foreach (var p in world.Players)
			{
				progression = p.PlayerActor.TraitsImplementing<IProgression>().FirstOrDefault();
				if (progression != null)
					break;
			}

			return progression;
		}

		int RoadMask(CPos cell)
		{
			var mask = 0;
			for (var i = 0; i < 4; i++)
				if (roads != null && roads.IsRoad(cell + CityUtils.Neighbours4[i]))
					mask |= 1 << i;

			return mask;
		}

		/// <summary>Plans a drag from `from` to `to` (an L-shaped path). `funds` &lt; 0 means unlimited.</summary>
		public TrackPlan Plan(CPos from, CPos to, int funds)
		{
			var plan = new TrackPlan { Path = CityUtils.RoadPath(from, to) };
			var path = plan.Path;
			for (var i = 0; i < path.Count; i++)
			{
				var cell = path[i];
				if (!world.Map.Contains(cell))
				{
					plan.ErrorKey = ErrorBlocked;
					break;
				}

				if (IsRail(cell))
					continue;

				var progress = Progression();
				if (progress != null && !progress.IsCellOwned(cell))
				{
					plan.ErrorKey = ErrorNotOwned;
					break;
				}

				if (roads != null && roads.IsRoad(cell))
				{
					// Only a straight rail over a straight perpendicular road is a level crossing.
					var prev = i > 0 ? path[i - 1] : (path.Count > 1 ? path[i + 1] : cell);
					var next = i < path.Count - 1 ? path[i + 1] : prev;
					var horizontal = prev.Y == cell.Y && next.Y == cell.Y && path.Count > 1;
					var vertical = prev.X == cell.X && next.X == cell.X && path.Count > 1;
					var mask = RoadMask(cell);
					if (!((horizontal && mask == 5) || (vertical && mask == 10)))
					{
						plan.ErrorKey = ErrorCrossing;
						break;
					}

					plan.Crossings.Add(cell);
				}
				else
				{
					var roadLayer = world.WorldActor.TraitOrDefault<RoadLayer>();
					if (roadLayer != null && !roadLayer.IsBuildableTerrain(cell))
					{
						plan.ErrorKey = ErrorTerrain;
						break;
					}

					if (ConstructionUtils.HasBlockingActor(world, cell))
					{
						plan.ErrorKey = ErrorBlocked;
						break;
					}
				}

				var cost = Info.CostPerCell + ConstructionUtils.AutoClearCost(world, cell);
				if (funds >= 0 && plan.Cost + cost > funds)
				{
					plan.ErrorKey = ErrorMoney;
					break;
				}

				plan.Cost += cost;
				plan.Build.Add(cell);
			}

			return plan;
		}

		/// <summary>Builds the planned cells. The caller pays.</summary>
		public void Apply(TrackPlan plan)
		{
			foreach (var c in plan.Build)
			{
				ConstructionUtils.ClearAutoClearActors(world, c);
				flags[c] = (byte)(RailFlag | (plan.Crossings.Contains(c) ? CrossingFlag : 0));
				RailCellCount++;
				Changed(c);
			}
		}

		/// <summary>Removes the rail on the cells of a drag. Returns the number of cells removed.</summary>
		public int Remove(CPos from, CPos to)
		{
			var n = 0;
			foreach (var c in CityUtils.RoadPath(from, to))
			{
				if (!IsRail(c))
					continue;

				flags[c] = 0;
				RailCellCount--;
				n++;
				Changed(c);
			}

			return n;
		}

		void Changed(CPos cell)
		{
			NetworkVersion++;
			Refresh(cell);
			foreach (var d in CityUtils.Neighbours4)
				Refresh(cell + d);

			RailChanged?.Invoke(cell);
		}

		void Refresh(CPos cell)
		{
			if (overlay == null || !flags.Contains(cell))
				return;

			if (!IsRail(cell))
			{
				overlay.Clear(cell);
				return;
			}

			var mask = 0;
			for (var i = 0; i < 4; i++)
				if (IsRail(cell + CityUtils.Neighbours4[i]))
					mask |= 1 << i;

			var frame = mask;
			if (IsCrossing(cell))
				frame = (mask & 5) != 0 ? 17 : 16;

			overlay.Set(cell, frame);
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			overlay?.Draw(wr.Viewport);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			overlay?.Dispose();
			disposed = true;
		}
	}
}
