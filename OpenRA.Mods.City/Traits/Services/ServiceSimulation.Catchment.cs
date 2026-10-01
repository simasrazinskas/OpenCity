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

namespace OpenRA.Mods.City.Traits
{
	// Road catchments: a multi-source BFS over road cells from every provider's access road keeps the two nearest providers
	// per cell. Properties are assigned to the nearest one; overloaded providers shed their farthest properties to the
	// second one. Satisfaction = capacity / load, written (with a distance falloff) into the coverage maps.
	public partial class ServiceSimulation
	{
		sealed class Catchment
		{
			public int[] Prov0, Prov1;
			public short[] D0, D1;
			public readonly List<ServiceState> List = [];
			public bool Dirty = true;
			public byte[] Map;
			public readonly List<int> Visited = [];
			public readonly List<int> Cand = [];
		}

		Catchment[] groups;
		byte[][] kindMaps;
		int[] qCell, qProv;
		short[] qDist;
		int lastRoadVersion = -1;

		void InitCatchments()
		{
			var n = mapWidth * mapHeight;
			groups = new Catchment[ServiceMath.GroupCount];
			for (var g = 0; g < groups.Length; g++)
				groups[g] = new Catchment { Prov0 = new int[n], Prov1 = new int[n], D0 = new short[n], D1 = new short[n], Map = new byte[n] };

			kindMaps = new byte[ServiceMath.KindCount][];
			for (var g = 0; g < groups.Length; g++)
			{
				var k = (int)ServiceMath.KindOf((CatchmentGroup)g);
				if (kindMaps[k] == null)
				{
					// Education has four groups: its kind map is the best of them.
					kindMaps[k] = ServiceMath.KindOf((CatchmentGroup)g) == ServiceKind.Education ? new byte[n] : groups[g].Map;
					coverage?.SetExternalCoverage((ServiceKind)k, kindMaps[k]);
				}
			}

			qCell = new int[n * 2];
			qProv = new int[n * 2];
			qDist = new short[n * 2];
		}

		void MarkAllDirty()
		{
			if (groups == null)
				return;

			for (var g = 0; g < groups.Length; g++)
				groups[g].Dirty = true;
		}

		void CatchmentPulse(int pulse)
		{
			if (roads != null && roads.NetworkVersion != lastRoadVersion)
			{
				lastRoadVersion = roads.NetworkVersion;
				MarkAllDirty();
			}

			for (var g = 0; g < groups.Length; g++)
			{
				if (groups[g].Dirty)
				{
					Recompute((CatchmentGroup)g);
					return;
				}
			}

			var g2 = pulse % Math.Max(Info.CatchmentIntervalPulses, groups.Length);
			if (g2 < groups.Length)
				Recompute((CatchmentGroup)g2);
		}

		/// <summary>Provider units of capacity for the coverage load of this group.</summary>
		int CapacityUnits(ServiceState s)
		{
			if (!s.Active)
				return 0;

			switch (s.Group)
			{
				case CatchmentGroup.Garbage: return s.Stored >= s.Capacity && s.Throughput == 0 ? 0 : s.Fleet * Info.HaulKgPerVehicleDay;
				case CatchmentGroup.Health: return s.Capacity * Info.ResidentsPerBed;
				case CatchmentGroup.Deathcare: return s.Stored >= s.Capacity && s.Throughput == 0 ? 0 : s.Fleet * Info.ResidentsPerHearse;
				case CatchmentGroup.Police: return s.Fleet * Info.CitizensPerPoliceVehicle;
				case CatchmentGroup.Fire: return s.Fleet * Info.CitizensPerFireVehicle;
				case CatchmentGroup.Post: return s.Fleet * Info.MailPerVanDay;
				case CatchmentGroup.Parks: return s.Capacity * (50 + s.Condition / 2) / 100;
				default: return s.Capacity;
			}
		}

		int NeedUnits(CatchmentGroup g, PropData d)
		{
			var res = Residents(d);
			switch (g)
			{
				case CatchmentGroup.Garbage: return GarbagePerDay(d, res, Workers(d));
				case CatchmentGroup.Health:
				case CatchmentGroup.Deathcare:
				case CatchmentGroup.Parks:
				case CatchmentGroup.Admin: return res;
				case CatchmentGroup.Edu1:
				case CatchmentGroup.Edu2:
				case CatchmentGroup.Edu3:
				case CatchmentGroup.Edu4:
				{
					var i = (int)g - (int)CatchmentGroup.Edu1;
					return i < Info.StudentPercent.Length ? res * Info.StudentPercent[i] / 100 : 0;
				}

				case CatchmentGroup.Police:
				case CatchmentGroup.Telecom: return res + Workers(d);
				case CatchmentGroup.Fire: return res + Workers(d) + 2;
				case CatchmentGroup.Post: return res * Info.MailPerResident + Workers(d) * Info.MailPerJob;
				default: return 0;
			}
		}

		int Falloff(int dist, int range)
		{
			if (range <= 0)
				return 100;

			return 100 - (100 - Info.EdgeCoveragePercent) * Math.Min(dist, range) / range;
		}

		bool HasAccess(PropData d)
		{
			var a = AccessOf(d);
			return a != CPos.Zero && InMap(a);
		}

		/// <summary>Road cell a property uses: the registry's access road, or (lots a few cells behind a street) the nearest road cell within AccessSearch cells.</summary>
		CPos AccessOf(PropData d)
		{
			var version = (roads?.NetworkVersion ?? 0) * 31 + (registry?.Version ?? 0);
			if (d.AccessVersion == version)
				return d.Access;

			d.AccessVersion = version;
			var p = d.Prop;
			d.Access = p.AccessRoad;
			if (d.Access != CPos.Zero || roads == null)
				return d.Access;

			var r = Info.AccessSearch;
			var best = int.MaxValue;
			var x1 = p.Origin.X + p.Width - 1;
			var y1 = p.Origin.Y + p.Depth - 1;
			for (var y = p.Origin.Y - r; y <= y1 + r; y++)
			{
				for (var x = p.Origin.X - r; x <= x1 + r; x++)
				{
					var c = new CPos(x, y);
					if (!InMap(c) || !roads.IsRoad(c))
						continue;

					var dx = x < p.Origin.X ? p.Origin.X - x : x > x1 ? x - x1 : 0;
					var dy = y < p.Origin.Y ? p.Origin.Y - y : y > y1 ? y - y1 : 0;
					if (dx + dy < best)
					{
						best = dx + dy;
						d.Access = c;
					}
				}
			}

			return d.Access;
		}

		void Recompute(CatchmentGroup group)
		{
			var gi = (int)group;
			var c = groups[gi];
			c.Dirty = false;
			c.List.Clear();
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != group)
					continue;

				s.Load = 0;
				s.Assigned.Clear();
				s.Satisfaction = 0;
				if (s.Active && !s.Info.Prison && !s.Info.Helicopter && s.Info.FirewatchRadius == 0)
					c.List.Add(s);
			}

			if (group == CatchmentGroup.Telecom || group == CatchmentGroup.Admin)
				RecomputeRadius(group, c);
			else if (roads != null)
				RecomputeRoads(group, c);

			// Refresh the kind map (education: best of four levels).
			var kind = ServiceMath.KindOf(group);
			if (kind == ServiceKind.Education)
			{
				var km = kindMaps[(int)kind];
				Array.Clear(km);
				for (var g = (int)CatchmentGroup.Edu1; g <= (int)CatchmentGroup.Edu4; g++)
				{
					var m = groups[g].Map;
					for (var i = 0; i < km.Length; i++)
						if (m[i] > km[i])
							km[i] = m[i];
				}
			}

			coverage?.Touch();
		}

		void RecomputeRoads(CatchmentGroup group, Catchment c)
		{
			var gi = (int)group;
			var n = mapWidth * mapHeight;
			Array.Clear(c.Prov0);
			Array.Clear(c.Prov1);
			Array.Clear(c.Map);
			c.Visited.Clear();

			var head = 0;
			var tail = 0;
			for (var i = 0; i < c.List.Count; i++)
			{
				var s = c.List[i];
				var root = AccessOf(s.Need);
				if (root == CPos.Zero || !InMap(root))
					continue;

				var idx = CellIndex(root);
				if (c.Prov0[idx] == 0)
				{
					c.Prov0[idx] = i + 1;
					c.D0[idx] = 0;
				}
				else if (c.Prov1[idx] == 0 && c.Prov0[idx] != i + 1)
				{
					c.Prov1[idx] = i + 1;
					c.D1[idx] = 0;
				}
				else
					continue;

				qCell[tail] = idx;
				qProv[tail] = i + 1;
				qDist[tail] = 0;
				tail++;
			}

			while (head < tail)
			{
				var idx = qCell[head];
				var prov = qProv[head];
				var dist = qDist[head];
				head++;
				if (dist >= c.List[prov - 1].Range)
					continue;

				var cell = new CPos(idx % mapWidth, idx / mapWidth);
				for (var dir = 0; dir < 4; dir++)
				{
					var nb = cell + CityUtils.Neighbours4[dir];
					if (!InMap(nb) || !roads.IsRoad(nb) || !roads.CanEnter(cell, dir))
						continue;

					var ni = CellIndex(nb);
					if (c.Prov0[ni] == prov || c.Prov1[ni] == prov)
						continue;

					if (c.Prov0[ni] == 0)
					{
						c.Prov0[ni] = prov;
						c.D0[ni] = (short)(dist + 1);
					}
					else if (c.Prov1[ni] == 0)
					{
						c.Prov1[ni] = prov;
						c.D1[ni] = (short)(dist + 1);
					}
					else
						continue;

					if (tail < qCell.Length)
					{
						qCell[tail] = ni;
						qProv[tail] = prov;
						qDist[tail] = (short)(dist + 1);
						tail++;
					}
				}
			}

			// Candidates: properties whose access road is reached by at least one provider.
			var cand = c.Cand;
			cand.Clear();
			var load = new long[c.List.Count];
			var cap = new long[c.List.Count];
			for (var i = 0; i < c.List.Count; i++)
				cap[i] = CapacityUnits(c.List[i]);

			var assign = new int[propList.Count];
			var need = new int[propList.Count];
			for (var k = 0; k < propList.Count; k++)
			{
				var d = propList[k];
				d.Sat[gi] = 0;
				d.ProviderId[gi] = 0;
				if (!HasAccess(d))
					continue;

				var idx = CellIndex(AccessOf(d));
				var a = c.Prov0[idx];
				if (a != 0 && !Allows(c.List[a - 1], d.Prop.Origin))
					a = c.Prov1[idx] != 0 && Allows(c.List[c.Prov1[idx] - 1], d.Prop.Origin) ? c.Prov1[idx] : 0;

				if (a == 0)
					continue;

				assign[k] = a;
				need[k] = NeedUnits(group, d);
				load[a - 1] += need[k];
				cand.Add(k);
			}

			// Overflow: a provider over capacity hands its farthest properties to the second provider of their cell.
			for (var i = 0; i < c.List.Count; i++)
			{
				if (load[i] <= cap[i])
					continue;

				var mine = new List<int>();
				foreach (var k in cand)
					if (assign[k] == i + 1 && need[k] > 0)
						mine.Add(k);

				mine.Sort((x, y) =>
				{
					var dx = c.D0[CellIndex(AccessOf(propList[x]))];
					var dy = c.D0[CellIndex(AccessOf(propList[y]))];
					return dx != dy ? dy.CompareTo(dx) : x.CompareTo(y);
				});

				foreach (var k in mine)
				{
					if (load[i] <= cap[i])
						break;

					var idx = CellIndex(AccessOf(propList[k]));
					var b = c.Prov1[idx];
					if (b == 0 || b == i + 1 || load[b - 1] + need[k] > cap[b - 1] || !Allows(c.List[b - 1], propList[k].Prop.Origin))
						continue;

					load[i] -= need[k];
					load[b - 1] += need[k];
					assign[k] = b;
				}
			}

			for (var i = 0; i < c.List.Count; i++)
			{
				var s = c.List[i];
				s.Load = (int)Math.Min(load[i], int.MaxValue);
				s.Satisfaction = load[i] <= 0 ? 100 : (int)Math.Min(100, cap[i] * 100 / load[i]);
			}

			foreach (var k in cand)
			{
				var d = propList[k];
				var s = c.List[assign[k] - 1];
				var idx = CellIndex(AccessOf(d));
				var dist = assign[k] == c.Prov0[idx] ? c.D0[idx] : c.D1[idx];
				d.ProviderId[gi] = s.Prop.Id;
				d.Dist[gi] = dist;
				d.Sat[gi] = (byte)(s.Satisfaction * Falloff(dist, s.Range) / 100 * PropPenalty(group, d) / 100);
				s.Assigned.Add(d.Prop.Id);
			}

			BuildMap(group, c, n);
		}

		// Piles, unburied bodies and fires lower what a covered property actually experiences.
		int PropPenalty(CatchmentGroup group, PropData d)
		{
			switch (group)
			{
				case CatchmentGroup.Garbage:
					return 100 - Math.Clamp((d.GarbageKg - Info.CollectKg) * 100 / Math.Max(1, Info.SeverePileKg - Info.CollectKg), 0, 100);
				case CatchmentGroup.Deathcare:
					return d.Bodies > 0 && d.BodySince > 0 && Now - d.BodySince > Info.BodyOverdueTicks ? 40 : 100;
				case CatchmentGroup.Fire:
					return d.FireDamage > 0 ? 30 : 100;
				default:
					return 100;
			}
		}

		void BuildMap(CatchmentGroup group, Catchment c, int n)
		{
			var map = c.Map;
			var strengthOf = new int[c.List.Count];
			for (var i = 0; i < c.List.Count; i++)
				strengthOf[i] = c.List[i].Info.Strength;

			// Road cells (and everything the BFS reached).
			for (var idx = 0; idx < n; idx++)
			{
				var a = c.Prov0[idx];
				if (a == 0)
					continue;

				var s = c.List[a - 1];
				var v = s.Satisfaction * Falloff(c.D0[idx], s.Range) / 100 * strengthOf[a - 1] / 100;
				var b = c.Prov1[idx];
				if (b != 0)
				{
					var s2 = c.List[b - 1];
					v = Math.Max(v, s2.Satisfaction * Falloff(c.D1[idx], s2.Range) / 100 * strengthOf[b - 1] / 100);
				}

				map[idx] = (byte)v;
				c.Visited.Add(idx);
			}

			// Spread to the ground next to roads.
			var spread = Info.GroundSpread;
			foreach (var idx in c.Visited)
			{
				int v = map[idx];
				var x = idx % mapWidth;
				var y = idx / mapWidth;
				for (var dy = -spread; dy <= spread; dy++)
				{
					for (var dx = -spread; dx <= spread; dx++)
					{
						var m = Math.Abs(dx) + Math.Abs(dy);
						if (m == 0 || m > spread)
							continue;

						var nx = x + dx;
						var ny = y + dy;
						if (nx < 0 || ny < 0 || nx >= mapWidth || ny >= mapHeight)
							continue;

						var v2 = v - Info.GroundFalloff * m;
						var ni = ny * mapWidth + nx;
						if (v2 > map[ni])
							map[ni] = (byte)v2;
					}
				}
			}

			// Building footprints show their own experienced level.
			for (var k = 0; k < propList.Count; k++)
			{
				var d = propList[k];
				var v = d.Sat[(int)group];
				if (v == 0)
					continue;

				var s = ProviderOf(d, group);
				v = (byte)(v * (s != null ? s.Info.Strength : 100) / 100);
				for (var yy = 0; yy < d.Prop.Depth; yy++)
				{
					for (var xx = 0; xx < d.Prop.Width; xx++)
					{
						var cell = d.Prop.Origin + new CVec(xx, yy);
						if (!InMap(cell))
							continue;

						var ci = CellIndex(cell);
						if (v > map[ci])
							map[ci] = v;
					}
				}
			}
		}

		ServiceState ProviderOf(PropData d, CatchmentGroup g)
		{
			var id = d.ProviderId[(int)g];
			return id == 0 ? null : Data(id)?.Service;
		}

		/// <summary>Radius coverage (telecom, welfare): the nearest provider within its radius takes the load.</summary>
		void RecomputeRadius(CatchmentGroup group, Catchment c)
		{
			var gi = (int)group;
			Array.Clear(c.Map);
			var load = new long[c.List.Count];
			var cap = new long[c.List.Count];
			for (var i = 0; i < c.List.Count; i++)
				cap[i] = CapacityUnits(c.List[i]);

			var assign = new int[propList.Count];
			var dists = new int[propList.Count];
			for (var k = 0; k < propList.Count; k++)
			{
				var d = propList[k];
				d.Sat[gi] = 0;
				d.ProviderId[gi] = 0;
				var best = -1;
				var bestD = int.MaxValue;
				for (var i = 0; i < c.List.Count; i++)
				{
					var s = c.List[i];
					var dx = d.Prop.Origin.X - s.Prop.Origin.X;
					var dy = d.Prop.Origin.Y - s.Prop.Origin.Y;
					var d16 = Exts.ISqrt((dx * dx + dy * dy) * 256);
					if (d16 < s.Range * 16 && d16 < bestD)
					{
						bestD = d16;
						best = i;
					}
				}

				if (best < 0)
					continue;

				assign[k] = best + 1;
				dists[k] = bestD;
				load[best] += NeedUnits(group, d);
			}

			for (var i = 0; i < c.List.Count; i++)
			{
				var s = c.List[i];
				s.Load = (int)Math.Min(load[i], int.MaxValue);
				s.Satisfaction = load[i] <= 0 ? 100 : (int)Math.Min(100, cap[i] * 100 / load[i]);
			}

			for (var k = 0; k < propList.Count; k++)
			{
				if (assign[k] == 0)
					continue;

				var d = propList[k];
				var s = c.List[assign[k] - 1];
				d.ProviderId[gi] = s.Prop.Id;
				d.Dist[gi] = (short)(dists[k] / 16);
				d.Sat[gi] = (byte)(s.Satisfaction * (s.Range * 16 - dists[k]) / (s.Range * 16) * s.Info.Strength / 100);
				s.Assigned.Add(d.Prop.Id);
			}

			// Stamp the coverage disc of each provider.
			foreach (var s in c.List)
			{
				var r16 = s.Range * 16;
				for (var y = Math.Max(0, s.Prop.Origin.Y - s.Range); y <= Math.Min(mapHeight - 1, s.Prop.Origin.Y + s.Range); y++)
				{
					for (var x = Math.Max(0, s.Prop.Origin.X - s.Range); x <= Math.Min(mapWidth - 1, s.Prop.Origin.X + s.Range); x++)
					{
						var dx = x - s.Prop.Origin.X;
						var dy = y - s.Prop.Origin.Y;
						var d16 = Exts.ISqrt((dx * dx + dy * dy) * 256);
						if (d16 >= r16)
							continue;

						var v = s.Satisfaction * (r16 - d16) / r16 * s.Info.Strength / 100;
						var ci = y * mapWidth + x;
						if (v > c.Map[ci])
							c.Map[ci] = (byte)v;
					}
				}
			}
		}

		/// <summary>Up to two providers covering a road cell for a group, nearest first.</summary>
		void ProvidersAt(CatchmentGroup g, CPos road, out ServiceState a, out int da, out ServiceState b, out int db, CPos districtCell = default)
		{
			a = b = null;
			da = db = 0;
			if (groups == null || !InMap(road))
				return;

			var c = groups[(int)g];
			var idx = CellIndex(road);
			if (c.Prov0[idx] != 0 && c.Prov0[idx] <= c.List.Count)
			{
				a = c.List[c.Prov0[idx] - 1];
				da = c.D0[idx];
			}

			if (c.Prov1[idx] != 0 && c.Prov1[idx] <= c.List.Count)
			{
				b = c.List[c.Prov1[idx] - 1];
				db = c.D1[idx];
			}

			// Providers limited to other districts do not serve this location.
			if (districtCell != CPos.Zero)
			{
				if (a != null && !Allows(a, districtCell))
				{
					a = null;
					da = 0;
				}

				if (b != null && !Allows(b, districtCell))
				{
					b = null;
					db = 0;
				}

				if (a == null && b != null)
				{
					a = b;
					da = db;
					b = null;
					db = 0;
				}
			}
		}

		ServiceState NearestWith(CatchmentGroup g, CPos road, Func<ServiceState, bool> ok)
		{
			ProvidersAt(g, road, out var a, out _, out var b, out _, road);
			if (a != null && a.Alive && a.Active && ok(a))
				return a;

			if (b != null && b.Alive && b.Active && ok(b))
				return b;

			return null;
		}
	}
}
