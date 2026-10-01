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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Fertile land, forest, ore, oil and stone of the map (design/06 3.1). Reads the map.bin resource bytes",
		"(type 1 fertile, 2 ore, 3 oil; density 1..255), counts tree actors as forest stock and generates a deterministic",
		"fallback for maps without resource bytes. Ore and oil deplete, fertile land renews and dies from ground pollution.")]
	public class NaturalResourceLayerInfo : TraitInfo, NotBefore<SpawnMapActorsInfo>
	{
		[Desc("Generate deterministic deposits when the map has no resource bytes.")]
		public readonly bool GenerateIfEmpty = true;

		[Desc("Extra salt for the fallback generator (the map UID is always mixed in).")]
		public readonly int Seed = 0;

		[Desc("Fertile richness (0..1000) = density * this.")]
		public readonly int FertileScale = 4;

		[Desc("Ore units = density * this.")]
		public readonly int OreScale = 2;

		[Desc("Oil units = density * this.")]
		public readonly int OilScale = 2;

		[Desc("Forest stock a single tree actor contributes to its cell (cap 100).")]
		public readonly int TreeStock = 100;

		[Desc("Actor name prefix that identifies tree actors.")]
		public readonly string TreePrefix = "tree-";

		[Desc("Stone yield factor (percent) on Clear and on Rough terrain.")]
		public readonly int StoneClear = 100;
		public readonly int StoneRough = 150;

		[Desc("Fertile richness regained per pulse (25 ticks) up to the base value. 96 pulses per clock day.")]
		public readonly int RenewPerPulse = 1;

		[Desc("Ground pollution (0..100) above which fertile soil is damaged.")]
		public readonly int PollutionThreshold = 10;

		[Desc("Richness lost per pulse per point of pollution above the threshold, in 1/1000.")]
		public readonly int PollutionWearMilli = 80;

		[Desc("Fish (design/06 P5): water cells within this distance of land hold fish, 1000 at the shore falling by FishFalloff per cell.")]
		public readonly int FishMaxDistance = 6;
		public readonly int FishFalloff = 150;

		[Desc("Fish regained per pulse up to the base stock.")]
		public readonly int FishRenewPerPulse = 1;

		[Desc("Surface water pollution (0..100) above which fish die, and the stock lost per pulse per point above it, in 1/1000.")]
		public readonly int WaterPollutionThreshold = 10;
		public readonly int WaterPollutionWearMilli = 80;

		[Desc("Trees die when ground pollution (0..100, ENV PollutionLayer) stays above this for TreeDeathPulses pulses (3 clock days). 0 = off.")]
		public readonly int TreeDeathGround = 60;
		public readonly int TreeDeathPulses = 288;

		[Desc("Pulses between StateHash refreshes.")]
		public readonly int HashPulses = 10;

		public override object Create(ActorInitializer init) { return new NaturalResourceLayer(init.Self, this); }
	}

	public class NaturalResourceLayer : ITick, IWorldLoaded, ISync, ICityAutoTestReporter
	{
		public readonly NaturalResourceLayerInfo Info;
		readonly World world;
		readonly Map map;

		readonly CellLayer<ushort> fertile;
		readonly CellLayer<ushort> fertileBase;
		readonly CellLayer<ushort> forest;
		readonly CellLayer<ushort> ore;
		readonly CellLayer<ushort> oreBase;
		readonly CellLayer<ushort> oil;
		readonly CellLayer<ushort> oilBase;
		readonly CellLayer<ushort> fish;
		readonly CellLayer<ushort> fishBase;
		readonly CellLayer<ushort> poison;
		readonly List<CPos> fishCells = [];
		readonly List<Actor> trees = [];
		long fishCaught;
		int treesKilled;

		readonly List<CPos> fertileCells = [];
		readonly List<CPos> oreCells = [];
		readonly List<CPos> oilCells = [];
		IPollutionMap pollution;
		int pulseCount;
		long oreRemoved;
		long oilRemoved;

		public NaturalResourceLayer(Actor self, NaturalResourceLayerInfo info)
		{
			Info = info;
			world = self.World;
			map = world.Map;
			fertile = new CellLayer<ushort>(map);
			fertileBase = new CellLayer<ushort>(map);
			forest = new CellLayer<ushort>(map);
			ore = new CellLayer<ushort>(map);
			oreBase = new CellLayer<ushort>(map);
			oil = new CellLayer<ushort>(map);
			oilBase = new CellLayer<ushort>(map);
			fish = new CellLayer<ushort>(map);
			fishBase = new CellLayer<ushort>(map);
			poison = new CellLayer<ushort>(map);

			// Trees are map actors created before WorldLoaded: subscribe now so roads, growth and hubs that clear or plant them keep the stand current.
			world.ActorAdded += OnActorAdded;
			world.ActorRemoved += OnActorRemoved;
		}

		/// <summary>Bumped whenever any cell changes (overlay caches).</summary>
		public int Version { get; private set; }

		/// <summary>True when the deposits came from the fallback generator rather than map.bin.</summary>
		public bool GeneratedFallback { get; private set; }

		[VerifySync]
		public int StateHash { get; private set; }

		bool IsTree(Actor a)
		{
			return a.Info.Name.StartsWith(Info.TreePrefix, StringComparison.Ordinal);
		}

		void OnActorAdded(Actor a)
		{
			if (!IsTree(a))
				return;

			var c = a.Location;
			if (!forest.Contains(c))
				return;

			trees.Add(a);
			forest[c] = (ushort)Math.Min(100, forest[c] + Info.TreeStock);
			Version++;
		}

		void OnActorRemoved(Actor a)
		{
			if (!IsTree(a))
				return;

			var c = a.Location;
			if (!forest.Contains(c))
				return;

			forest[c] = (ushort)Math.Max(0, forest[c] - Info.TreeStock);
			Version++;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			pollution = w.WorldActor.TraitsImplementing<IPollutionMap>().FirstOrDefault();

			var any = false;
			foreach (var cell in map.AllCells)
			{
				var r = map.Resources[cell];
				if (r.Type == 0 || r.Index == 0)
					continue;

				any = true;
				Load(cell, r.Type, r.Index);
			}

			if (!any && Info.GenerateIfEmpty)
			{
				GeneratedFallback = true;
				Generate();
			}

			GenerateFish();
			StateHash = ComputeHash();
		}

		// Fish: water cells within FishMaxDistance of land (multi-source BFS from the shore), richer near the coast.
		void GenerateFish()
		{
			var dist = new CellLayer<byte>(map);
			var queue = new Queue<CPos>();
			foreach (var c in map.AllCells)
			{
				if (map.GetTerrainInfo(c).Type != "Water")
				{
					dist[c] = 1;
					queue.Enqueue(c);
				}
			}

			while (queue.Count > 0)
			{
				var c = queue.Dequeue();
				if (dist[c] > Info.FishMaxDistance)
					continue;

				foreach (var d in CityUtils.Neighbours4)
				{
					var n = c + d;
					if (!map.Contains(n) || dist[n] != 0 || map.GetTerrainInfo(n).Type != "Water")
						continue;

					dist[n] = (byte)(dist[c] + 1);
					queue.Enqueue(n);
				}
			}

			var seed = IndustryHash.HashString(map.Uid) ^ 0x51f;
			foreach (var c in map.AllCells)
			{
				var d = (int)dist[c];
				if (d < 2 || d > Info.FishMaxDistance + 1 || map.GetTerrainInfo(c).Type != "Water")
					continue;

				var stock = 1000 - (d - 2) * Info.FishFalloff + (IndustryHash.Mix(c, seed) % 201 - 100);
				fish[c] = fishBase[c] = (ushort)Math.Clamp(stock, 1, 1000);
				fishCells.Add(c);
			}
		}

		void Load(CPos cell, int type, int density)
		{
			switch (type)
			{
				case 1:
					fertile[cell] = fertileBase[cell] = (ushort)Math.Min(1000, density * Info.FertileScale);
					fertileCells.Add(cell);
					break;
				case 2:
					ore[cell] = oreBase[cell] = (ushort)Math.Min(ushort.MaxValue, density * Info.OreScale);
					oreCells.Add(cell);
					break;
				case 3:
					oil[cell] = oilBase[cell] = (ushort)Math.Min(ushort.MaxValue, density * Info.OilScale);
					oilCells.Add(cell);
					break;
			}
		}

		// ---- fallback generator: integer blobs with value noise, seeded by the map UID ----------------------------------
		static int Noise(int x, int y, int scale, int salt)
		{
			var ix = Math.DivRem(x, scale, out var fx);
			var iy = Math.DivRem(y, scale, out var fy);
			int At(int a, int b) => IndustryHash.Mix(a, b, salt) & 255;
			var top = At(ix, iy) * (scale - fx) + At(ix + 1, iy) * fx;
			var bottom = At(ix, iy + 1) * (scale - fx) + At(ix + 1, iy + 1) * fx;
			return (top * (scale - fy) + bottom * fy) / (scale * scale);
		}

		bool IsLand(CPos c)
		{
			var t = map.GetTerrainInfo(c).Type;
			return t == "Clear" || t == "Rough";
		}

		void Generate()
		{
			var seed = IndustryHash.HashString(map.Uid) ^ Info.Seed;
			var b = map.Bounds;
			(int Count, int Type, int RMin, int RMax)[] plan = [(4, 1, 7, 12), (2, 2, 5, 7), (1, 3, 4, 6)];
			var used = new HashSet<CPos>();
			var order = 0;
			foreach (var (count, type, rMin, rMax) in plan)
			{
				for (var k = 0; k < count; k++)
				{
					order++;
					var margin = rMax + 2;
					var cx = b.Left + margin + IndustryHash.Mix(seed, order, 1) % Math.Max(1, b.Width - 2 * margin);
					var cy = b.Top + margin + IndustryHash.Mix(seed, order, 2) % Math.Max(1, b.Height - 2 * margin);
					var r = rMin + IndustryHash.Mix(seed, order, 3) % (rMax - rMin + 1);
					var centre = new MPos(cx, cy).ToCPos(map);
					for (var dy = -r - 3; dy <= r + 3; dy++)
						for (var dx = -r - 3; dx <= r + 3; dx++)
						{
							var c = centre + new CVec(dx, dy);
							if (!map.Contains(c) || used.Contains(c) || !IsLand(c))
								continue;

							// Squared distance, perturbed by noise, against r^2.
							var wobble = Noise(c.X, c.Y, 5, seed + order) - 128;
							var d2 = (dx * dx + dy * dy) * 256 + wobble * r * 12;
							if (d2 >= r * r * 256)
								continue;

							var core = 255 - d2 / (r * r);
							var density = Math.Clamp(40 + core * (130 + Noise(c.X, c.Y, 3, seed + 97) / 2) / 255, 1, 255);
							Load(c, type, density);
							used.Add(c);
						}
				}
			}
		}

		/// <summary>Fertile 0..1000, Forest 0..100, Ore/Oil units remaining, Stone yield percent (0 on water), Fish 0.</summary>
		public int GetAmount(NaturalResourceKind kind, CPos cell)
		{
			if (!fertile.Contains(cell))
				return 0;

			switch (kind)
			{
				case NaturalResourceKind.Fertile: return fertile[cell];
				case NaturalResourceKind.Forest: return forest[cell];
				case NaturalResourceKind.Ore: return ore[cell];
				case NaturalResourceKind.Oil: return oil[cell];
				case NaturalResourceKind.Fish: return fish[cell];
				case NaturalResourceKind.Stone:
					var t = map.GetTerrainInfo(cell).Type;
					return t == "Rough" ? Info.StoneRough : t == "Clear" ? Info.StoneClear : 0;
				default: return 0;
			}
		}

		/// <summary>Initial stock of a cell (what the richness percentage is measured against).</summary>
		public int GetInitial(NaturalResourceKind kind, CPos cell)
		{
			if (!fertile.Contains(cell))
				return 0;

			switch (kind)
			{
				case NaturalResourceKind.Fertile: return fertileBase[cell];
				case NaturalResourceKind.Forest: return 100;
				case NaturalResourceKind.Ore: return oreBase[cell];
				case NaturalResourceKind.Oil: return oilBase[cell];
				case NaturalResourceKind.Fish: return fishBase[cell];
				default: return GetAmount(kind, cell);
			}
		}

		/// <summary>Info view colour step for a cell: -1 = no resource, else 0 (poor) .. 10 (rich), measured against the initial stock (forest: stand).</summary>
		public int GetHeatFrame(NaturalResourceKind kind, CPos cell)
		{
			var initial = GetInitial(kind, cell);
			if (initial <= 0)
				return -1;

			var amount = GetAmount(kind, cell);
			var scale = 255 * Math.Max(Info.OreScale, Info.OilScale);
			if (kind == NaturalResourceKind.Fertile || kind == NaturalResourceKind.Fish)
				scale = 1000;
			else if (kind == NaturalResourceKind.Forest)
				scale = 100;

			return Math.Clamp(amount * 10 / Math.Max(1, scale), 0, 10);
		}

		/// <summary>Current stock against the initial stock, 0..100.</summary>
		public int GetRichnessPercent(NaturalResourceKind kind, CPos cell)
		{
			var initial = GetInitial(kind, cell);
			return initial <= 0 ? 0 : Math.Min(100, GetAmount(kind, cell) * 100 / initial);
		}

		/// <summary>Takes up to `units` out of a finite deposit (Ore, Oil) or fertile cell. Returns the units removed. Synced callers only.</summary>
		public int Remove(NaturalResourceKind kind, CPos cell, int units)
		{
			if (units <= 0 || !fertile.Contains(cell))
				return 0;

			CellLayer<ushort> layer;
			switch (kind)
			{
				case NaturalResourceKind.Ore: layer = ore; break;
				case NaturalResourceKind.Oil: layer = oil; break;
				case NaturalResourceKind.Fertile: layer = fertile; break;
				case NaturalResourceKind.Fish: layer = fish; break;
				default: return 0;
			}

			var take = Math.Min(units, layer[cell]);
			if (take <= 0)
				return 0;

			layer[cell] = (ushort)(layer[cell] - take);
			if (kind == NaturalResourceKind.Ore)
				oreRemoved += take;
			else if (kind == NaturalResourceKind.Oil)
				oilRemoved += take;
			else if (kind == NaturalResourceKind.Fish)
				fishCaught += take;

			Version++;
			return take;
		}

		/// <summary>Cells that hold (or held) the resource, in row-major order. Forest and Stone: every cell with stock/yield.</summary>
		public IEnumerable<CPos> CellsWith(NaturalResourceKind kind)
		{
			switch (kind)
			{
				case NaturalResourceKind.Fertile: return fertileCells;
				case NaturalResourceKind.Ore: return oreCells;
				case NaturalResourceKind.Oil: return oilCells;
				case NaturalResourceKind.Fish: return fishCells;
				case NaturalResourceKind.Forest: return map.AllCells.Where(c => forest[c] > 0);
				case NaturalResourceKind.Stone: return map.AllCells.Where(c => GetAmount(kind, c) > 0);
				default: return [];
			}
		}

		public int TotalAmount(NaturalResourceKind kind)
		{
			long sum = 0;
			switch (kind)
			{
				case NaturalResourceKind.Fertile: foreach (var c in fertileCells) sum += fertile[c]; break;
				case NaturalResourceKind.Ore: foreach (var c in oreCells) sum += ore[c]; break;
				case NaturalResourceKind.Oil: foreach (var c in oilCells) sum += oil[c]; break;
				case NaturalResourceKind.Fish: foreach (var c in fishCells) sum += fish[c]; break;
				case NaturalResourceKind.Forest: foreach (var c in map.AllCells) sum += forest[c]; break;
			}

			return (int)Math.Min(int.MaxValue, sum);
		}

		// ---- dynamics: once per pulse over the (small, static) set of fertile cells ------------------------------------
		void ITick.Tick(Actor self)
		{
			if (world.WorldTick % 25 != 0 || world.WorldTick == 0)
				return;

			pulseCount++;
			var changed = false;
			for (var i = 0; i < fertileCells.Count; i++)
			{
				var c = fertileCells[i];
				var cur = (int)fertile[c];
				var max = (int)fertileBase[c];
				var next = cur;
				if (pollution != null)
				{
					var over = pollution.GetGround(c) - Info.PollutionThreshold;
					if (over > 0)
						next -= over * Info.PollutionWearMilli / 1000;
				}

				if (next < max)
					next = Math.Min(max, next + Info.RenewPerPulse);

				next = Math.Max(0, next);
				if (next != cur)
				{
					fertile[c] = (ushort)next;
					changed = true;
				}
			}

			// Fish renew and die from surface water pollution.
			for (var i = 0; i < fishCells.Count; i++)
			{
				var c = fishCells[i];
				var cur = (int)fish[c];
				var max = (int)fishBase[c];
				var next = cur;
				if (pollution != null)
				{
					var over = pollution.GetWaterPollution(c) - Info.WaterPollutionThreshold;
					if (over > 0)
						next -= over * Info.WaterPollutionWearMilli / 1000;
				}

				if (next < max)
					next = Math.Min(max, next + Info.FishRenewPerPulse);

				next = Math.Max(0, next);
				if (next != cur)
				{
					fish[c] = (ushort)next;
					changed = true;
				}
			}

			StepTrees();
			if (changed)
				Version++;

			if (pulseCount % Math.Max(1, Info.HashPulses) == 0)
				StateHash = ComputeHash();
		}

		// Trees on heavily polluted ground die after TreeDeathPulses (checked every 4th pulse, staggered by list index).
		void StepTrees()
		{
			if (pollution == null || Info.TreeDeathGround <= 0)
				return;

			if (pulseCount % 16 == 0)
				trees.RemoveAll(t => t.Disposed || !t.IsInWorld);

			List<Actor> dying = null;
			for (var i = 0; i < trees.Count; i++)
			{
				if ((i + pulseCount) % 4 != 0)
					continue;

				var t = trees[i];
				if (t.Disposed || !t.IsInWorld)
					continue;

				var c = t.Location;
				if (pollution.GetGround(c) > Info.TreeDeathGround)
				{
					poison[c] = (ushort)Math.Min(ushort.MaxValue, poison[c] + 4);
					if (poison[c] >= Info.TreeDeathPulses)
						(dying ??= []).Add(t);
				}
				else if (poison[c] > 0)
					poison[c] = (ushort)Math.Max(0, poison[c] - 4);
			}

			if (dying == null)
				return;

			treesKilled += dying.Count;
			world.AddFrameEndTask(_ =>
			{
				foreach (var t in dying)
					if (!t.Disposed)
						t.Dispose();
			});
		}

		/// <summary>Order-independent sum-hash over all deposit arrays (fresh, not cached).</summary>
		public int ComputeHash()
		{
			unchecked
			{
				var h = 17;
				foreach (var c in map.AllCells)
				{
					var v = fertile[c] * 31 + forest[c] * 17 + ore[c] * 7 + oil[c] + fish[c] * 5;
					if (v != 0)
						h = h * 31 + (c.X * 1009 + c.Y) * 3 + v;
				}

				return h;
			}
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			return $"resources fertile={TotalAmount(NaturalResourceKind.Fertile) / 1000}k forest={TotalAmount(NaturalResourceKind.Forest) / 100} " +
				$"ore={TotalAmount(NaturalResourceKind.Ore)} oil={TotalAmount(NaturalResourceKind.Oil)} oreMined={oreRemoved} oilPumped={oilRemoved} " +
				$"cells F/O/Oil={fertileCells.Count}/{oreCells.Count}/{oilCells.Count}{(GeneratedFallback ? " generated" : string.Empty)} " +
				$"fish={TotalAmount(NaturalResourceKind.Fish) / 1000}k caught={fishCaught} treesKilled={treesKilled} hash={ComputeHash()}";
		}
	}
}
