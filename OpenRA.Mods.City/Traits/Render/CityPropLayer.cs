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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>When the '-lit' companion of a prop is drawn.</summary>
	public enum CityPropLight : byte
	{
		/// <summary>Only after dusk (street lamps, windows of kiosks, lit signs). Lamps also get a ground light pool.</summary>
		Night,

		/// <summary>Always (traffic signal heads, warning beacons): drawn full bright, ambient never darkens it.</summary>
		Always,

		/// <summary>Like Night, plus a ground light pool (street lamps).</summary>
		Lamp
	}

	/// <summary>
	/// A tall static prop on a cell (lamp, signal, pylon, mast, railing, sign). Image/Sequence/Frame pick the sprite
	/// (anchor at the prop's ground point); <see cref="Offset"/> moves that ground point from the cell centre. Props
	/// with a "&lt;sequence&gt;-lit" sequence get their emissive frame per <see cref="Light"/>.
	/// </summary>
	public readonly struct CityProp
	{
		public readonly string Image;
		public readonly string Sequence;
		public readonly int Frame;
		public readonly WVec Offset;
		public readonly int ZOffset;
		public readonly CityPropLight Light;

		public CityProp(string image, string sequence, int frame = 0, WVec offset = default, int zOffset = 0, CityPropLight light = CityPropLight.Night)
		{
			Image = image;
			Sequence = sequence;
			Frame = frame;
			Offset = offset;
			ZOffset = zOffset;
			Light = light;
		}
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Depth-sorted static props on cells (render only): road furniture, signals, lamps, pylons, poles, catenary masts,",
		"railings, signs. Pulls them from RoadLayer.GetProps, UtilityOverlay.GetProps and TrackProps (cached per cell until the",
		"networks change), plus anything pushed with Set; draws the visible ones sorted at their ground point with the ambient",
		"tint, seasonal variants, live signal lights and their '-lit' frames (lamps at night, signals always).")]
	public class CityPropLayerInfo : TraitInfo
	{
		[Desc("Props per cell. Slot ranges by owner: 0-3 roads (lamps, signals, signs, railings), 4-5 utilities (pylons),",
			"6-7 transit (masts, catenary, platform furniture).")]
		public readonly int Slots = 8;

		[PaletteReference]
		public readonly string Palette = "city";

		public override object Create(ActorInitializer init) { return new CityPropLayer(init.Self, this); }
	}

	public sealed class CityPropLayer : IRender, IWorldLoaded
	{
		readonly struct Resolved
		{
			public readonly ISpriteSequence Sequence;
			public readonly ISpriteSequence Lit;
			public readonly CityProp Prop;

			// Seasonal variants (spring, summer, autumn, winter) or traffic signal heads (red, amber, green) + their lit frames.
			public readonly ISpriteSequence[] Variants;
			public readonly ISpriteSequence[] LitVariants;
			public readonly int SignalArm;
			public readonly bool Pool;

			public Resolved(ISpriteSequence sequence, ISpriteSequence lit, in CityProp prop,
				ISpriteSequence[] variants = null, ISpriteSequence[] litVariants = null, int signalArm = -1, bool pool = false)
			{
				Sequence = sequence;
				Lit = lit;
				Prop = prop;
				Variants = variants;
				LitVariants = litVariants;
				SignalArm = signalArm;
				Pool = pool;
			}
		}

		readonly CityPropLayerInfo info;
		readonly World world;
		readonly Dictionary<CPos, Resolved[]> props = [];
		readonly List<IRenderable> buffer = [];
		readonly Dictionary<CPos, Resolved[]> pulled = [];
		readonly List<RoadProp> roadProps = [];
		readonly List<UtilityProp> utilityProps = [];
		readonly List<TrackProp> trackProps = [];
		readonly List<Resolved> resolving = [];
		RoadLayer roadLayer;
		UtilityOverlay utilityOverlay;
		UtilityNetwork utilityNetwork;
		RailLayer rail;
		TrafficSim traffic;
		int pulledVersion = int.MinValue;
		bool cables;
		InfoViewLayer infoViews;
		IsoSpriteCache cache;
		CityAtmosphere atmosphere;
		CityNightLights lights;
		PaletteReference palette;

		public CityPropLayer(Actor self, CityPropLayerInfo info)
		{
			this.info = info;
			world = self.World;
		}

		/// <summary>Bumped on every change (renderers that cache per cell can compare it).</summary>
		public int Version { get; private set; }

		/// <summary>The prop layer of a world, or null.</summary>
		public static CityPropLayer Get(World world) { return world?.WorldActor.TraitOrDefault<CityPropLayer>(); }

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			cache = IsoSpriteCache.For(w);
			atmosphere = w.WorldActor.TraitOrDefault<CityAtmosphere>();
			lights = w.WorldActor.TraitOrDefault<CityNightLights>();
			palette = info.Palette != null ? wr.Palette(info.Palette) : null;
			roadLayer = w.WorldActor.TraitOrDefault<RoadLayer>();
			utilityOverlay = w.WorldActor.TraitOrDefault<UtilityOverlay>();
			utilityNetwork = w.WorldActor.TraitOrDefault<UtilityNetwork>();
			rail = w.WorldActor.TraitOrDefault<RailLayer>();
			traffic = w.WorldActor.TraitOrDefault<TrafficSim>();
			infoViews = w.WorldActor.TraitOrDefault<InfoViewLayer>();
		}

		static readonly string[] SeasonSuffixes = ["-spring", "", "-autumn", "-winter"];
		static readonly string[] SignalStates = ["red", "amber", "green"];

		// Props of the networks (roads, utilities, rails) for one cell, resolved to sequences once and cached until any
		// owner reports a change.
		Resolved[] Pulled(CPos cell)
		{
			if (pulled.TryGetValue(cell, out var cached))
				return cached;

			resolving.Clear();
			if (roadLayer != null)
			{
				roadProps.Clear();
				roadLayer.GetProps(cell, roadProps);
				foreach (var rp in roadProps)
					ResolveRoad(rp);
			}

			if (utilityOverlay != null)
			{
				utilityProps.Clear();
				utilityOverlay.GetProps(cell, utilityProps, cables);
				foreach (var up in utilityProps)
					resolving.Add(new Resolved(up.Sequence, null, new CityProp(null, up.Sequence.Name, up.Frame, up.Offset, up.ZOffset)));
			}

			if (rail != null && rail.IsRail(cell))
			{
				var mask = 0;
				for (var i = 0; i < 4; i++)
					if (rail.IsRail(cell + CityUtils.Neighbours4[i]))
						mask |= 1 << i;

				trackProps.Clear();
				TrackProps.Rail(cell, mask, rail.IsCrossing(cell), trackProps);
				foreach (var tp in trackProps)
				{
					var seq = cache.Sequence(TrackProps.Image, tp.Sequence);
					if (seq != null)
						resolving.Add(new Resolved(seq, cache.Sequence(TrackProps.Image, tp.Sequence + "-lit"),
							new CityProp(TrackProps.Image, tp.Sequence, tp.Frame, tp.Offset, tp.ZOffset, CityPropLight.Always)));
				}
			}

			var result = resolving.Count == 0 ? [] : resolving.ToArray();
			pulled[cell] = result;
			return result;
		}

		void ResolveRoad(in RoadProp rp)
		{
			const string Image = RoadLayer.PropImage;
			var seq = cache.Sequence(Image, rp.Sequence);
			if (seq == null)
				return;

			var lit = rp.LitSequence != null ? cache.Sequence(Image, rp.LitSequence) : null;
			if (rp.NightOnly)
			{
				resolving.Add(new Resolved(seq, null, new CityProp(Image, rp.Sequence, rp.Frame, rp.Offset, rp.ZOffset - 512, CityPropLight.Night), pool: true));
				return;
			}

			ISpriteSequence[] variants = null, litVariants = null;
			if (rp.SignalArm >= 0)
			{
				variants = new ISpriteSequence[3];
				litVariants = new ISpriteSequence[3];
				for (var i = 0; i < 3; i++)
				{
					variants[i] = cache.Sequence(Image, RoadProp.SignalSequence(SignalStates[i], false)) ?? seq;
					litVariants[i] = cache.Sequence(Image, RoadProp.SignalSequence(SignalStates[i], true));
				}
			}
			else if (rp.Seasonal)
			{
				variants = new ISpriteSequence[4];
				for (var i = 0; i < 4; i++)
					variants[i] = cache.Sequence(Image, rp.Sequence + SeasonSuffixes[i]) ?? seq;
			}

			var light = rp.SignalArm >= 0 ? CityPropLight.Always : rp.Sequence.StartsWith("lamp", StringComparison.Ordinal) ? CityPropLight.Lamp : CityPropLight.Night;
			resolving.Add(new Resolved(seq, lit, new CityProp(Image, rp.Sequence, rp.Frame, rp.Offset, rp.ZOffset, light), variants, litVariants, rp.SignalArm));
		}

		int SourceVersion()
		{
			unchecked
			{
				var v = roadLayer?.PropsVersion ?? 0;
				v = v * 31 + (utilityNetwork?.LayoutVersion ?? 0);
				v = v * 31 + (roadLayer?.NetworkVersion ?? 0);
				return v;
			}
		}

		int Season()
		{
			if (atmosphere == null)
				return 1;

			if (atmosphere.SnowCover >= 0.3f)
				return 3;

			var month = (int)atmosphere.MonthF % 12;
			return month >= 2 && month <= 4 ? 0 : month >= 8 && month <= 10 ? 2 : 1;
		}

		/// <summary>Places (or replaces) the prop in a slot of a cell. Unknown image/sequence clears the slot.</summary>
		public void Set(CPos cell, int slot, in CityProp prop)
		{
			if (slot < 0 || slot >= info.Slots)
				throw new ArgumentOutOfRangeException(nameof(slot));

			cache ??= IsoSpriteCache.For(world);
			var seq = cache.Sequence(prop.Image, prop.Sequence);
			if (seq == null)
			{
				Clear(cell, slot);
				return;
			}

			if (!props.TryGetValue(cell, out var slots))
				props[cell] = slots = new Resolved[info.Slots];

			slots[slot] = new Resolved(seq, cache.Sequence(prop.Image, prop.Sequence + "-lit"), prop);
			Version++;
		}

		public void Clear(CPos cell, int slot)
		{
			if (!props.TryGetValue(cell, out var slots) || slot < 0 || slot >= slots.Length || slots[slot].Sequence == null)
				return;

			slots[slot] = default;
			Version++;
			foreach (var s in slots)
				if (s.Sequence != null)
					return;

			props.Remove(cell);
		}

		/// <summary>Clears every slot in [first, last] of every cell (owner rebuilds).</summary>
		public void ClearSlots(int first, int last)
		{
			var empty = new List<CPos>();
			foreach (var (cell, slots) in props)
			{
				var any = false;
				for (var i = 0; i < slots.Length; i++)
				{
					if (i >= first && i <= last)
						slots[i] = default;
					else if (slots[i].Sequence != null)
						any = true;
				}

				if (!any)
					empty.Add(cell);
			}

			foreach (var c in empty)
				props.Remove(c);

			Version++;
		}

		/// <summary>True when a cell holds a street lamp (pushed CityPropLight.Lamp or a road lamp with its own light pool).</summary>
		public bool HasLamp(CPos cell)
		{
			if (roadLayer != null || utilityOverlay != null || rail != null)
				foreach (var r in Pulled(cell))
					if (r.Pool || r.Prop.Light == CityPropLight.Lamp)
						return true;

			if (props.TryGetValue(cell, out var slots))
				foreach (var s in slots)
					if (s.Sequence != null && s.Prop.Light == CityPropLight.Lamp)
						return true;

			return false;
		}

		/// <summary>The road lamp light pool sprite (roadprops lamp-pool), or null.</summary>
		public Sprite LampPool => cache?.Sequence(RoadLayer.PropImage, "lamp-pool")?.GetSprite(0);

		public int Count => props.Count;

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			// Low-voltage poles along the roads only while a power view shows the network (the mockups keep streets clean).
			var mode = infoViews?.Mode ?? CityInfoView.None;
			var showCables = mode == CityInfoView.Power || mode == CityInfoView.PowerGrid;
			var version = SourceVersion() * 2 + (showCables ? 1 : 0);
			cables = showCables;
			if (version != pulledVersion)
			{
				pulled.Clear();
				pulledVersion = version;
			}

			buffer.Clear();
			var tint = atmosphere?.SpriteTint ?? Vector3.One;
			var night = lights?.LampAlpha ?? 0f;
			var season = Season();
			var map = world.Map;
			var pullAny = roadLayer != null || utilityOverlay != null || rail != null;
			foreach (var puv in wr.Viewport.AllVisibleCells)
			{
				var cell = ((MPos)puv).ToCPos(map);
				if (pullAny)
					foreach (var r in Pulled(cell))
						Draw(map, cell, r, tint, night, season);

				if (props.TryGetValue(cell, out var slots))
					foreach (var r in slots)
						if (r.Sequence != null)
							Draw(map, cell, r, tint, night, season);
			}

			return buffer.ToArray();
		}

		void Draw(Map map, CPos cell, in Resolved r, in Vector3 tint, float night, int season)
		{
			var p = r.Prop;
			var pos = map.CenterOfCell(cell) + p.Offset;
			if (r.Pool)
			{
				// Lamp light pool: an emissive ground decal, only at night, drawn under everything standing on the cell.
				if (night > 0f)
					buffer.Add(new SpriteRenderable(r.Sequence.GetSprite(p.Frame % Math.Max(1, r.Sequence.Length)), pos, WVec.Zero, p.ZOffset, palette,
						1f, night, Vector3.One, TintModifiers.IgnoreWorldTint, false));

				return;
			}

			var seq = r.Sequence;
			var lit = r.Lit;
			if (r.SignalArm >= 0 && r.Variants != null)
			{
				var state = traffic?.SignalLight(cell, r.SignalArm) ?? 0;
				seq = r.Variants[state];
				lit = r.LitVariants[state];
			}
			else if (r.Variants != null)
				seq = r.Variants[season];

			var frame = p.Frame % Math.Max(1, seq.Length);
			buffer.Add(new SpriteRenderable(seq.GetSprite(frame), pos, WVec.Zero, p.ZOffset, palette, 1f, 1f, tint, TintModifiers.None, false));

			var alpha = p.Light == CityPropLight.Always ? 1f : night;
			if (lit != null && alpha > 0f)
				buffer.Add(new SpriteRenderable(lit.GetSprite(frame % Math.Max(1, lit.Length)), pos, WVec.Zero, p.ZOffset + 1, palette,
					1f, alpha, Vector3.One, TintModifiers.IgnoreWorldTint, false));
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr) { yield break; }
	}
}
