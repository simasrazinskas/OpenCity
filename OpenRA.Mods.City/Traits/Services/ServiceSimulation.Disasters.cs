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
	// Disasters-lite: wildfires on tree cells (put out by firewatch towers and fire stations), lightning in storms,
	// blackout chirps when a power plant burns. All chances are 0 by default (see ServiceSimulationInfo).
	public partial class ServiceSimulation
	{
		struct WildFire
		{
			public Actor Tree;
			public CPos Cell;
			public int Start;
		}

		readonly List<WildFire> wild = [];
		List<Actor> trees;
		CityClimate climate;
		int cWild, cWildOut, cWildLost, cStorm;

		void ResolveDisasters()
		{
			climate = Find<CityClimate>();
		}

		/// <summary>Cells of burning trees (for renderers).</summary>
		public void GetWildfires(List<CPos> into)
		{
			for (var i = 0; i < wild.Count; i++)
				into.Add(wild[i].Cell);
		}

		public int WildfireCount => wild.Count;

		void RefreshTrees()
		{
			trees = [];
			foreach (var a in world.Actors)
				if (a.IsInWorld && !a.Disposed && a.Info.Name.StartsWith("tree-", StringComparison.Ordinal))
					trees.Add(a);
		}

		void NewDayDisasters()
		{
			if (Info.WildfirePermille <= 0 || climate == null || clock == null)
				return;

			if (climate.TemperatureX10 < Info.WildfireMinTempX10 || climate.Weather != CityWeather.Clear)
				return;

			if (ServiceMath.Hash(clock.DayIndex, 0, 81) % 1000 >= Info.WildfirePermille)
				return;

			RefreshTrees();
			if (trees.Count == 0)
				return;

			IgniteTree(trees[ServiceMath.Hash(clock.DayIndex, 1, 82) % trees.Count], Now);
		}

		/// <summary>ENV's CityDisasters (lightning) asks for a fire on a property; the normal fire and dispatch flow takes over.</summary>
		bool IFireStarter.TryStartFire(int propertyId, string cause)
		{
			var d = Data(propertyId);
			if (d == null || !d.Alive || d.FireDamage > 0 || !d.Building.IsOperational)
				return false;

			StartFire(d, Now);
			return d.FireDamage > 0;
		}

		void IgniteTree(Actor tree, int t)
		{
			for (var i = 0; i < wild.Count; i++)
				if (wild[i].Tree == tree)
					return;

			wild.Add(new WildFire { Tree = tree, Cell = tree.Location, Start = t });
			cWild++;
			Chirp("chirp-service-wildfire", 2, null);
		}

		void TickStorm(int t)
		{
			if (Info.StormLightningPermille <= 0 || climate == null || climate.Weather != CityWeather.Storm || propList.Count == 0)
				return;

			if (ServiceMath.Hash(t, 0, 66) % 1000 >= Info.StormLightningPermille)
				return;

			var d = propList[ServiceMath.Hash(t, 1, 67) % propList.Count];
			if (d.FireDamage == 0 && d.Alive)
			{
				cStorm++;
				StartFire(d, t);
				Chirp("chirp-service-lightning", 2, d);
			}
		}

		void TickWildfires(int t)
		{
			for (var i = wild.Count - 1; i >= 0; i--)
			{
				var w = wild[i];
				if (w.Tree.Disposed || !w.Tree.IsInWorld)
				{
					wild.RemoveAt(i);
					continue;
				}

				var burned = t - w.Start;
				if (Spotted(w.Cell, burned))
				{
					wild.RemoveAt(i);
					cWildOut++;
					continue;
				}

				if (burned >= Info.WildfireBurnTicks)
				{
					wild.RemoveAt(i);
					cWildLost++;
					var tree = w.Tree;
					world.AddFrameEndTask(_ =>
					{
						if (!tree.Disposed)
							tree.Dispose();
					});
					continue;
				}

				if (t % Math.Max(1, Info.WildfireSpreadInterval) == 0)
					SpreadWildfire(w, t);
			}
		}

		void SpreadWildfire(WildFire w, int t)
		{
			for (var dy = -1; dy <= 1; dy++)
			{
				for (var dx = -1; dx <= 1; dx++)
				{
					if (dx == 0 && dy == 0)
						continue;

					var c = new CPos(w.Cell.X + dx, w.Cell.Y + dy);
					if (!InMap(c))
						continue;

					var roll = ServiceMath.Hash(c.X * 997 + c.Y, t, 83) % 100;
					var p = registry?.GetAt(c);
					var pd = p != null ? Data(p.Id) : null;
					if (pd != null && pd.Alive && pd.FireDamage == 0 && roll < pd.FireHazard / 2)
					{
						StartFire(pd, t);
						continue;
					}

					if (roll >= Info.WildfireSpreadPercent)
						continue;

					foreach (var a in world.ActorMap.GetActorsAt(c))
						if (a.Info.Name.StartsWith("tree-", StringComparison.Ordinal) && !a.Disposed)
							IgniteTree(a, t);
				}
			}
		}

		/// <summary>A firewatch tower in radius sees the fire after a while; a fire station within its range arrives later.</summary>
		bool Spotted(CPos cell, int burned)
		{
			for (var i = 0; i < providers.Count; i++)
			{
				var s = providers[i];
				if (s.Group != CatchmentGroup.Fire || !s.Active)
					continue;

				var dx = s.Prop.Origin.X - cell.X;
				var dy = s.Prop.Origin.Y - cell.Y;
				if (s.Info.FirewatchRadius > 0)
				{
					if (burned >= Info.FirewatchSpotTicks && dx * dx + dy * dy <= s.Info.FirewatchRadius * s.Info.FirewatchRadius)
						return true;
				}
				else if (!s.Info.Helicopter && burned >= Info.WildfireStationTicks && Math.Abs(dx) + Math.Abs(dy) <= s.Range)
					return true;
			}

			return false;
		}
	}
}
