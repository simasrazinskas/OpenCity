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
using OpenRA.Mods.City.Widgets;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Renders the selected info view (coloured cell overlay) and a hover read-out. Mode is local UI state, not synced.")]
	public class InfoViewLayerInfo : TraitInfo
	{
		[Desc("Minimum real time in milliseconds between two overlay refreshes.")]
		public readonly int RefreshInterval = 500;

		[Desc("Residents sampled per building for the citizen based views (education, wealth, age, happiness).")]
		public readonly int ResidentSamples = 16;

		public override object Create(ActorInitializer init) { return new InfoViewLayer(init.Self, this); }
	}

	/// <summary>
	/// Draws the active <see cref="CityInfoView"/> as translucent cell colours. Values come from the providers of
	/// CityInterfaces.cs (pollution, utilities, traffic, services, properties, citizens) and from any
	/// <see cref="IInfoViewSource"/>; the legacy coverage layer is the fallback. Everything is read only.
	/// </summary>
	public class InfoViewLayer : IRenderAboveWorld, IRenderAnnotations, INotifyActorDisposing
	{
		readonly InfoViewLayerInfo info;
		readonly World world;
		readonly CellLayer<int> shown;
		readonly CellLayer<int> next;
		readonly CellLayer<bool> built;

		CityUiContext ctx;
		CityInfoView mode;
		bool dirty;
		long lastRefresh;
		int lastVersion = -1;

		public InfoViewLayer(Actor self, InfoViewLayerInfo info)
		{
			this.info = info;
			world = self.World;
			shown = new CellLayer<int>(world.Map);
			next = new CellLayer<int>(world.Map);
			built = new CellLayer<bool>(world.Map);
			shown.Clear(-1);
		}

		/// <summary>The active info view. Local UI state, never synced.</summary>
		public CityInfoView Mode
		{
			get => mode;
			set
			{
				if (mode == value)
					return;

				mode = value;
				dirty = true;
			}
		}

		CityUiContext Ctx => ctx ??= CityUiContext.For(world);

		int Version()
		{
			var c = Ctx;
			var v = c.CoverageLayer?.Version ?? 0;
			v += c.Roads?.NetworkVersion ?? 0;
			v += c.Utilities?.Version ?? 0;
			v += c.Pollution?.Version ?? 0;
			v += c.Properties?.Version ?? 0;
			foreach (var s in c.Sources)
				v += s.Version;

			return v;
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (mode == CityInfoView.None && !dirty)
				return;

			var now = Game.RunTime;
			var version = Version();
			var versionChanged = version != lastVersion;
			if (dirty || now - lastRefresh >= info.RefreshInterval || (versionChanged && now - lastRefresh >= info.RefreshInterval / 4))
			{
				Refresh();
				lastRefresh = now;
				lastVersion = version;
				dirty = false;
			}

			if (mode == CityInfoView.None)
				return;

			var def = InfoViews.Get(mode);
			var ramp = def?.Ramp ?? InfoRamp.Good;
			var map = world.Map;
			var renderer = Game.Renderer.WorldRgbaColorRenderer;
			foreach (var puv in wr.Viewport.AllVisibleCells)
			{
				var cell = ((MPos)puv).ToCPos(map);
				if (!shown.Contains(cell))
					continue;

				var value = shown[cell];
				if (value < 0)
					continue;

				var color = InfoViews.RampColor(ramp, value);
				if (color.A == 0)
					continue;

				var r = map.Grid.Ramps[map.Ramp[cell]];
				var wpos = map.CenterOfCell(cell) - new WVec(0, 0, r.CenterHeightOffset);
				var c0 = wr.Screen3DPosition(wpos + r.Corners[0]);
				var c1 = wr.Screen3DPosition(wpos + r.Corners[1]);
				var c2 = wr.Screen3DPosition(wpos + r.Corners[2]);
				var c3 = wr.Screen3DPosition(wpos + r.Corners[3]);
				renderer.FillRect(c0, c1, c2, c3, color);
			}
		}

		void Refresh()
		{
			next.Clear(-1);

			if (mode != CityInfoView.None)
				Compute();

			foreach (var cell in world.Map.AllCells)
				shown[cell] = next[cell];
		}

		/// <summary>Raw value (0..100, or category index) of the active view at a cell, -1 when nothing is drawn.</summary>
		public int ValueAt(CPos cell)
		{
			return mode != CityInfoView.None && shown.Contains(cell) ? shown[cell] : -1;
		}

		// ---- computation ----
		void Compute()
		{
			var c = Ctx;
			switch (mode)
			{
				case CityInfoView.Power:
				case CityInfoView.PowerGrid:
					Buildings((a, b) => c.Utilities != null ? c.Utilities.PowerPercent(a) : b.HasPower ? 100 : 0);
					break;
				case CityInfoView.Water:
				case CityInfoView.WaterGrid:
					Buildings((a, b) => c.Utilities != null ? c.Utilities.WaterPercent(a) : b.HasWater ? 100 : 0);
					break;
				case CityInfoView.Sewage:
					Buildings((a, b) => c.Utilities == null ? -1 : c.Utilities.HasSewage(a) ? 100 : 0);
					break;
				case CityInfoView.Happiness:
					ComputeHappiness();
					break;
				case CityInfoView.Traffic:
					Roads(TrafficValue);
					break;
				case CityInfoView.Roads:
					Roads(cell => c.Roads == null ? -1 : (int)c.Roads.GetClass(cell));
					break;
				case CityInfoView.LandValue:
					ComputeLandValue();
					break;
				case CityInfoView.Pollution:
					Terrain(cell => c.Pollution != null
						? Math.Max(c.Pollution.GetGround(cell), Math.Max(c.Pollution.GetAir(cell), c.Pollution.GetNoise(cell) / 2))
						: c.CoverageLayer?.GetPollution(cell) ?? 0, true);
					break;
				case CityInfoView.AirPollution:
					Terrain(cell => c.Pollution?.GetAir(cell) ?? 0, true);
					break;
				case CityInfoView.GroundPollution:
					Terrain(cell => c.Pollution?.GetGround(cell) ?? 0, true);
					break;
				case CityInfoView.Noise:
					Terrain(cell => c.Pollution?.GetNoise(cell) ?? 0, true);
					break;
				case CityInfoView.Groundwater:
					Terrain(cell => c.Pollution?.GetGroundwater(cell) ?? 0, true);
					break;
				case CityInfoView.Police:
				case CityInfoView.Fire:
				case CityInfoView.Health:
				case CityInfoView.Education:
				case CityInfoView.Parks:
				case CityInfoView.Garbage:
				case CityInfoView.Deathcare:
				case CityInfoView.Telecom:
				case CityInfoView.Post:
				case CityInfoView.Crime:
					ComputeService();
					break;
				case CityInfoView.BuildingLevel:
					Properties(p => (p.Level - 1) * 25);
					break;
				case CityInfoView.Education2:
					Residents(v => (int)v.Education * 25);
					break;
				case CityInfoView.Wealth:
					Residents(v => v.HouseholdCash / 30);
					break;
				case CityInfoView.Age:
					Residents(v => v.Age * 100 / 84);
					break;
				case CityInfoView.Districts:
					if (c.Progression != null)
						Terrain(cell => c.Progression.GetDistrict(cell) - 1, false);

					break;
				default:
					foreach (var s in c.Sources)
						if (s.Supports(mode))
						{
							var source = s;
							Terrain(cell => source.GetCell(mode, cell), false);
							break;
						}

					break;
			}
		}

		void Mark(CPos cell, int value)
		{
			if (value < 0 || !next.Contains(cell))
				return;

			next[cell] = value > 100 && InfoViews.Get(mode)?.Ramp != InfoRamp.Category ? 100 : value;
		}

		void Buildings(Func<Actor, CityBuilding, int> valueOf)
		{
			foreach (var tp in world.ActorsWithTrait<CityBuilding>())
			{
				var actor = tp.Actor;
				if (actor.IsDead || !actor.IsInWorld)
					continue;

				var value = valueOf(actor, tp.Trait);
				foreach (var (cell, _) in actor.OccupiesSpace.OccupiedCells())
					Mark(cell, value);
			}
		}

		void Roads(Func<CPos, int> valueOf)
		{
			var roads = Ctx.Roads;
			if (roads == null)
				return;

			foreach (var cell in roads.RoadCells)
				Mark(cell, valueOf(cell));
		}

		void Terrain(Func<CPos, int> valueOf, bool skipZero)
		{
			var map = world.Map;
			foreach (var cell in map.AllCells)
			{
				if (!map.Contains(cell))
					continue;

				var value = valueOf(cell);
				if (value < 0 || (skipZero && value <= 0))
					continue;

				Mark(cell, value);
			}
		}

		void Properties(Func<Property, int> valueOf)
		{
			var properties = Ctx.Properties;
			if (properties == null)
				return;

			foreach (var p in properties.All)
			{
				var value = valueOf(p);
				for (var y = 0; y < Math.Max(1, p.Depth); y++)
					for (var x = 0; x < Math.Max(1, p.Width); x++)
						Mark(p.Origin + new CVec(x, y), value);
			}
		}

		void Residents(Func<CitizenView, int> valueOf)
		{
			var citizens = Ctx.Citizens;
			if (citizens == null)
				return;

			Properties(p =>
			{
				if (p.Residents <= 0)
					return -1;

				long sum = 0;
				var count = 0;
				foreach (var id in citizens.ResidentsOf(p.Id))
				{
					if (citizens.TryGetCitizen(id, out var view))
					{
						sum += valueOf(view);
						count++;
					}

					if (count >= info.ResidentSamples)
						break;
				}

				return count == 0 ? -1 : (int)(sum / count);
			});
		}

		void ComputeHappiness()
		{
			var citizens = Ctx.Citizens;
			if (citizens == null || Ctx.Properties == null)
			{
				Buildings((_, b) => b.Happiness);
				return;
			}

			// Houses show the mood of their residents, other buildings the building's own happiness.
			Buildings((_, b) => b.Happiness);
			Residents(v => v.Happiness);
		}

		void ComputeLandValue()
		{
			var coverage = Ctx.CoverageLayer;
			if (coverage != null)
			{
				var map = world.Map;
				Terrain(cell => map.GetTerrainInfo(cell).Type == "Water" ? -1 : coverage.GetLandValue(cell), false);
				return;
			}

			Properties(p => p.LandValue);
		}

		void ComputeService()
		{
			var c = Ctx;
			var services = c.Services;
			var coverage = c.CoverageLayer;
			if (services == null && coverage == null)
				return;

			// Mark the built-up area (roads and building footprints) so empty coverage shows up as "bad" there;
			// elsewhere only cells that actually have a value are drawn.
			built.Clear(false);
			if (c.Roads != null)
				foreach (var cell in c.Roads.RoadCells)
					if (built.Contains(cell))
						built[cell] = true;

			foreach (var tp in world.ActorsWithTrait<CityBuilding>())
			{
				if (tp.Actor.IsDead || !tp.Actor.IsInWorld)
					continue;

				foreach (var (cell, _) in tp.Actor.OccupiesSpace.OccupiedCells())
					if (built.Contains(cell))
						built[cell] = true;
			}

			var kind = InfoViews.ServiceOf(mode);
			var crime = mode == CityInfoView.Crime;
			var map = world.Map;
			foreach (var cell in map.AllCells)
			{
				if (!map.Contains(cell))
					continue;

				int value;
				if (services != null)
					value = services.GetSatisfaction(kind, cell);
				else
					value = coverage.GetCoverage(LegacyService(mode), cell);

				// Crime pressure is the lack of police satisfaction, only where people are.
				if (crime)
				{
					if (!built[cell])
						continue;

					value = 100 - value;
				}
				else if (value <= 0 && !built[cell])
					continue;

				Mark(cell, value);
			}
		}

		static CityService LegacyService(CityInfoView view)
		{
			switch (view)
			{
				case CityInfoView.Police: return CityService.Police;
				case CityInfoView.Fire: return CityService.Fire;
				case CityInfoView.Health: return CityService.Health;
				case CityInfoView.Education: return CityService.Education;
				default: return CityService.Parks;
			}
		}

		/// <summary>Traffic view mode: false shows how slowly traffic moves (flow), true how full the lanes are (volume).</summary>
		public bool TrafficVolume
		{
			get => trafficVolume;
			set
			{
				if (trafficVolume == value)
					return;

				trafficVolume = value;
				dirty = true;
			}
		}

		bool trafficVolume;

		// Both modes colour 0 (green) to 100 (red).
		int TrafficValue(CPos cell)
		{
			var info = Ctx.Get<ITrafficInfo>();
			if (info != null)
				return trafficVolume ? info.GetTrafficVolumePercent(cell) : 100 - info.GetTrafficFlowPercent(cell);

			return Ctx.Traffic?.GetTrafficLoad(cell) ?? 0;
		}

		// ---- hover read-out ----
		bool IRenderAnnotations.SpatiallyPartitionable => false;

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (mode == CityInfoView.None || Ui.MouseOverWidget is not WorldInteractionControllerWidget)
				yield break;

			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			if (!world.Map.Contains(cell))
				yield break;

			var text = Describe(cell);
			if (string.IsNullOrEmpty(text))
				yield break;

			// Stacked above the hovered cell in UI pixels, so the lines never overlap or drift apart when zooming.
			var font = Game.Renderer.Fonts["Bold"];
			var lines = text.Split('\n');
			var lineHeight = CityAnnotationText.LineHeight(font);
			var topEdge = world.Map.CenterOfCell(cell) - new WVec(0, 512, 0);
			for (var i = 0; i < lines.Length; i++)
				yield return new CityAnnotationText(font, topEdge, new int2(0, -6 - (lines.Length - i) * lineHeight), i == 0 ? Color.White : CityUi.Muted, lines[i]);
		}

		string Describe(CPos cell)
		{
			var value = ValueAt(cell);
			if (value < 0)
				return null;

			var def = InfoViews.Get(mode);
			var c = Ctx;
			var name = def?.Name ?? mode.ToString();
			switch (mode)
			{
				case CityInfoView.Roads:
					if (c.Roads == null)
						return null;

					return name + ": " + CityUi.Message("label-roadclass-" + c.Roads.GetClass(cell).ToString().ToLowerInvariant(), c.Roads.GetClass(cell).ToString()) +
						"\n" + FluentProvider.GetMessage("label-road-detail",
							"lanes", c.Roads.GetLanes(cell), "speed", c.Roads.GetSpeedPercent(cell), "parking", c.Roads.GetParkingSlots(cell));
				case CityInfoView.Traffic:
					var flow = c.Traffic?.GetTrafficFlow(cell);
					return name + ": " + FluentProvider.GetMessage("label-city-percent", "value", value) +
						(flow.HasValue ? "\n" + FluentProvider.GetMessage("label-road-flow", "value", flow.Value) : "");
				case CityInfoView.Districts:
					return name + ": " + DistrictName(value + 1);
				case CityInfoView.NaturalResources:
					return name + ": " + WorldInfoViewSource.KindName(value / 16) + " " + FluentProvider.GetMessage("label-city-percent", "value", value % 16 * 10);
				default:
					return name + ": " + FluentProvider.GetMessage("label-city-percent", "value", value);
			}
		}

		string DistrictName(int id)
		{
			var districts = Ctx.ProgressionUi?.Districts;
			if (districts != null)
				foreach (var d in districts)
					if (d.Id == id && !string.IsNullOrEmpty(d.Name))
						return d.Name;

			return "#" + id;
		}

		void INotifyActorDisposing.Disposing(Actor self) { }
	}
}
