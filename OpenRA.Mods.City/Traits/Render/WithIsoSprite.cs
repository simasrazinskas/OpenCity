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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.City.Widgets;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>Render state of a building that swaps its sprites (growables: construction stages, abandoned, collapsed, burnt).</summary>
	public interface IIsoSpriteState
	{
		/// <summary>State sequence suffix without the dash ("build1", "abandoned", ...), or null for the normal look.</summary>
		string IsoState(Actor self);

		/// <summary>False while the building must stay dark at night (abandoned, under construction).</summary>
		bool IsoLit(Actor self);
	}

	[Desc("Isometric sprite rendering for city structures and props (render only). Slices every sprite of the actor into",
		"32 px screen strips that sort at their own footprint column (RCT2), faces the building toward its access road,",
		"swaps state/season sprites, adds the '-lit' companion frames at night, applies the ambient and info view tints,",
		"draws the see-through stub and provides footprint-plus-height mouse bounds.")]
	public class WithIsoSpriteInfo : TraitInfo
	{
		[Desc("Slice sprites into footprint columns. Off for props that never overlap their neighbours.")]
		public readonly bool Slice = true;

		[Desc("Face the access road (IFacing for sequences with Facings: 4; growables read it too).")]
		public readonly bool AutoFacing = true;

		[Desc("Face the side with the most water cells instead of the road (harbours, dams, fishing hubs).")]
		public readonly bool FaceWater = false;

		[Desc("Draw the lot ground plus a one-storey stub while CityView.SeeThrough is on.")]
		public readonly bool SeeThrough = true;

		[Desc("Stub height in screen pixels at 1x (one storey).")]
		public readonly int StubHeight = 10;

		[Desc("Lot ground colour of the see-through view.")]
		public readonly Color StubGround = Color.FromArgb(255, 150, 150, 138);

		[Desc("Suffix of the emissive companion sequences.")]
		public readonly string LitSuffix = "-lit";

		[Desc("Snow cover (0..1) from which the winter sprites ('<sequence>-winter' or 'winter') are used.")]
		public readonly float WinterSnowCover = 0.3f;

		[Desc("Use the spring/autumn sequences as well (vegetation).")]
		public readonly bool Seasons = false;

		[Desc("Pick the frame group by a stable cell hash instead of the animation (vegetation and rubble variants).")]
		public readonly bool PickVariant = false;

		[Desc("Facing frames interleaved in the actor's own sequences (ART layout: frame = group * 4 + facing, group = animation",
			"frame or variant). WithIsoSprite picks the facing frame; 1 = no facings. Growables pick their own frames.")]
		public readonly int FacingFrames = 4;

		[Desc("Footprint polygon plus sprite height as mouse bounds.")]
		public readonly bool MouseBounds = true;

		public override object Create(ActorInitializer init) { return new WithIsoSprite(init.Self, this); }
	}

	public class WithIsoSprite : IRenderModifier, IMouseBounds, IFacing, INotifyCreated
	{
		const string InactiveState = "inactive";
		const string BurntState = "burnt";

		public readonly WithIsoSpriteInfo Info;
		readonly Actor self;
		readonly int width, depth;
		readonly bool isBuilding;
		readonly List<IRenderable> buffer = [];

		IsoSpriteCache cache;
		string image;
		CityBuilding building;
		IIsoSpriteState stateSource;
		IAutoMouseBounds[] autoBounds;
		CityAtmosphere atmosphere;
		CityNightLights nightLights;
		InfoViewLayer infoViews;
		IPropertyRegistry registry;
		IRoadNetwork roads;
		ServiceSimulation services;
		bool servicesResolved;
		bool remapFrames;
		ISpriteSequence fillSequence;

		int facing;
		int facingVersion = int.MinValue;
		bool iso;

		public WithIsoSprite(Actor self, WithIsoSpriteInfo info)
		{
			Info = info;
			this.self = self;
			var bi = self.Info.TraitInfoOrDefault<BuildingInfo>();
			isBuilding = bi != null;
			width = Math.Max(1, bi?.Dimensions.X ?? 1);
			depth = Math.Max(1, bi?.Dimensions.Y ?? 1);
		}

		void INotifyCreated.Created(Actor self)
		{
			cache = IsoSpriteCache.For(self.World);
			image = self.TraitOrDefault<RenderSprites>()?.GetImage(self);
			building = self.TraitOrDefault<CityBuilding>();
			stateSource = self.TraitsImplementing<IIsoSpriteState>().FirstOrDefault();
			fillSequence = cache.Sequence(image, "fill");
			remapFrames = Info.PickVariant || (Info.FacingFrames > 1 && stateSource is not WithGrowableSprite);
			autoBounds = self.TraitsImplementing<IAutoMouseBounds>().ToArray();
			var w = self.World.WorldActor;
			atmosphere = w.TraitOrDefault<CityAtmosphere>();
			nightLights = w.TraitOrDefault<CityNightLights>();
			infoViews = w.TraitOrDefault<InfoViewLayer>();
			registry = w.TraitsImplementing<IPropertyRegistry>().FirstOrDefault();
			roads = w.TraitsImplementing<IRoadNetwork>().FirstOrDefault();
		}

		/// <summary>Footprint size in cells (W along +X, D along +Y).</summary>
		public int2 Footprint => new(width, depth);

		/// <summary>Facing index: 0 = front toward +Y, 1 = +X, 2 = -Y, 3 = -X (isokit order).</summary>
		public int FacingIndex
		{
			get
			{
				if (!Info.AutoFacing || !self.IsInWorld)
					return 0;

				var version = (registry?.Version ?? 0) * 31 + (roads?.NetworkVersion ?? 0);
				if (version != facingVersion)
				{
					facingVersion = version;
					var p = registry?.GetByActor(self);
					facing = Info.FaceWater ? IsoFacing.TowardWater(self.World.Map, self.Location, width, depth)
						: IsoFacing.Choose(roads, self.Location, width, depth, p?.AccessRoad ?? CPos.Zero);
				}

				return facing;
			}
		}

		// Render-only facing for sequences with Facings: 4 (frame = facing index). Orientation stays None so hit shapes
		// and every other world-space consumer see an unrotated building.
		WAngle IFacing.Facing { get => new(FacingIndex * 256); set { } }
		WRot IFacing.Orientation => WRot.None;
		WAngle IFacing.TurnSpeed => WAngle.Zero;

		static bool IsIso(WorldRenderer wr) { return wr.IsIsometric; }

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			buffer.Clear();
			var center = self.CenterPosition;
			if (Info.SeeThrough && isBuilding && CityView.SeeThrough && IsIso(wr))
			{
				AddStub(center);
				return buffer.ToArray();
			}

			var infoTint = infoViews?.BuildingTint(self);
			var tint = infoTint ?? atmosphere?.SpriteTint ?? Vector3.One;
			var state = State();
			var season = state == null ? Season() : null;
			var litAlpha = state == null && infoTint == null && nightLights != null && (stateSource?.IsoLit(self) ?? true)
				? nightLights.LitAlpha(self, building) : 0f;
			iso = IsIso(wr);
			var slice = Info.Slice && CityView.Slicing && width + depth > 2 && iso;

			foreach (var renderable in r)
			{
				if (renderable is SpriteRenderable sr && sr.Rotation == WAngle.Zero && !sr.IsDecoration)
				{
					var sprite = Remap(sr.Sprite);

					if (state == null && fillSequence != null)
						sprite = FillSprite() ?? sprite;

					var shown = state != null ? cache.Companion(image, sprite, "-" + state, state) ?? sprite
						: season != null ? cache.Companion(image, sprite, "-" + season, season) ?? sprite
						: sprite;

					Emit(wr, sr, shown, center, sr.ZOffset, sr.Alpha, sr.Tint * tint, sr.TintModifiers, slice);
					if (litAlpha > 0f)
					{
						var lit = cache.Companion(image, sprite, Info.LitSuffix);
						if (lit != null)
							Emit(wr, sr, lit, center, sr.ZOffset + 1, sr.Alpha * litAlpha, Vector3.One, sr.TintModifiers | TintModifiers.IgnoreWorldTint, slice);
					}
				}
				else if (renderable is IModifyableRenderable m && (m.TintModifiers & TintModifiers.IgnoreWorldTint) == 0)
					buffer.Add(m.WithTint(m.Tint * tint, m.TintModifiers));
				else
					buffer.Add(renderable);
			}

			return buffer.ToArray();
		}

		string State()
		{
			if (building == null)
				return stateSource?.IsoState(self);

			if (!servicesResolved)
			{
				servicesResolved = true;
				services = CityUiContext.For(self.World).Get<ServiceSimulation>();
			}

			var p = services != null ? registry?.GetByActor(self) : null;
			if (p != null && services.IsBurning(p.Id))
				return BurntState;

			if (stateSource != null)
				return stateSource.IsoState(self);

			return building.IsOperational && building.HasPower ? null : InactiveState;
		}

		// Landfill / cemetery: "fill" = level * 4 + facing, levels 0, 25, 50, 75, 100 % full.
		Sprite FillSprite()
		{
			if (!servicesResolved)
			{
				servicesResolved = true;
				services = CityUiContext.For(self.World).Get<ServiceSimulation>();
			}

			var p = services != null ? registry?.GetByActor(self) : null;
			var percent = p != null ? services.GetFillPercent(p.Id) : -1;
			if (percent < 0)
				return null;

			var levels = Math.Max(1, fillSequence.Length / 4);
			var level = Math.Clamp((percent * (levels - 1) + 50) / 100, 0, levels - 1);
			return fillSequence.GetSprite(level * 4 + FacingIndex % 4);
		}

		string Season()
		{
			if (atmosphere == null)
				return null;

			if (atmosphere.SnowCover >= Info.WinterSnowCover)
				return "winter";

			if (!Info.Seasons)
				return null;

			var month = (int)atmosphere.MonthF % 12;
			return month >= 2 && month <= 4 ? "spring" : month >= 8 && month <= 10 ? "autumn" : null;
		}

		// ART sequences interleave the facings: frame = group * 4 + facing. WithSpriteBody plays the frames in order, so
		// frame i shows group i % groups (the animation keeps its speed) in the facing toward the road.
		Sprite Remap(Sprite sprite)
		{
			if (!remapFrames || !cache.TryGetSource(image, sprite, out var src) || src.Facings > 1)
				return sprite;

			var length = src.Sequence.Length;
			var per = Info.FacingFrames > 1 && length % Info.FacingFrames == 0 ? Info.FacingFrames : 1;
			var groups = Math.Max(1, length / per);
			var group = Info.PickVariant ? WithGrowableSpriteInfo.VariantFor(self.Location, groups) : src.Frame % groups;
			var frame = group * per + (per > 1 ? FacingIndex % per : 0);
			return frame == src.Frame ? sprite : IsoSpriteCache.SpriteOf(src.Sequence, frame, 0, 1);
		}

		/// <summary>Ground point of footprint column <paramref name="k"/> that the column's strip sorts at (see ENGINE-PLAN §3.1).</summary>
		public static WPos ColumnSortPos(WPos center, int width, int depth, int k)
		{
			var x0 = center.X - width * 512;
			var y0 = center.Y - depth * 512;

			// The front-left edge (Y = y0 + D) holds columns 0..W-1, the front-right edge (X = x0 + W) columns W..W+D-1.
			// Each strip sorts at its column's rearmost front-edge point: everything in front of the footprint in that
			// column has a larger X + Y, everything behind it a smaller one. Minus 1 so a mover exactly there wins.
			var p = k < width
				? new WPos(x0 + k * 1024, y0 + depth * 1024, center.Z)
				: new WPos(x0 + width * 1024, y0 + (depth - (k - width) - 1) * 1024, center.Z);
			return p - new WVec(1, 1, 0);
		}

		void Emit(WorldRenderer wr, SpriteRenderable sr, Sprite sprite, WPos center, int zOffset, float alpha, in Vector3 tint,
			TintModifiers modifiers, bool slice)
		{
			var scale = sr.Scale;
			var half = new int2((int)(0.5f * scale * sprite.Size.X), (int)(0.5f * scale * sprite.Size.Y));
			if (!slice)
			{
				buffer.Add(new IsoStripRenderable(sprite, sr.Anchor, sr.Offset, half, iso ? ColumnSortPos(center, width, depth, 0) : sr.Pos,
					zOffset, sr.Palette, scale, alpha, tint, modifiers, false));
				return;
			}

			// Screen x of the footprint's left corner relative to the sprite's left edge, in unscaled sprite pixels.
			var anchorX = wr.ScreenPxPosition(sr.Anchor).X + wr.ScreenPxOffset(sr.Offset).X;
			var left = wr.ScreenPxPosition(center + new WVec(-width * 512, depth * 512, 0)).X;
			var spriteLeft = anchorX - half.X + scale * sprite.Offset.X;
			var origin = (int)MathF.Round((left - spriteLeft) / scale);
			var columnWidth = Math.Max(1, (int)MathF.Round(wr.ScreenPxOffset(new WVec(1024, 0, 0)).X / scale));

			foreach (var strip in cache.Strips(sprite, origin, width + depth, columnWidth))
				buffer.Add(new IsoStripRenderable(strip.Sprite, sr.Anchor, sr.Offset, half,
					ColumnSortPos(center, width, depth, strip.Column), zOffset, sr.Palette, scale, alpha, tint, modifiers, false));
		}

		void AddStub(WPos center)
		{
			var min = center - new WVec(width * 512, depth * 512, 0);
			var max = center + new WVec(width * 512, depth * 512, 0);
			var sort = ColumnSortPos(center, width, depth, 0);
			var ground = Info.StubGround;
			buffer.Add(new IsoBoxRenderable(min, max, 0, ground, ground, ground, sort, -1));

			var inset = new WVec(Math.Min(160, width * 96), Math.Min(160, depth * 96), 0);
			var c = IsoFacing.StubColor(building);
			buffer.Add(new IsoBoxRenderable(min + inset, max - inset, Info.StubHeight * 32,
				Shade(c, 1.12f), Shade(c, 1f), Shade(c, 0.72f), sort, 0));
		}

		static Color Shade(Color c, float f)
		{
			return Color.FromArgb(255, Math.Min(255, (int)(c.R * f)), Math.Min(255, (int)(c.G * f)), Math.Min(255, (int)(c.B * f)));
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> r) { return r; }

		Polygon IMouseBounds.MouseoverBounds(Actor self, WorldRenderer wr)
		{
			if (!Info.MouseBounds)
				return Polygon.Empty;

			var c = self.CenterPosition;
			var w = width * 512;
			var d = depth * 512;
			var left = wr.ScreenPxPosition(c + new WVec(-w, d, 0));
			var front = wr.ScreenPxPosition(c + new WVec(w, d, 0));
			var right = wr.ScreenPxPosition(c + new WVec(w, -d, 0));
			var back = wr.ScreenPxPosition(c + new WVec(-w, -d, 0));

			int top;
			if (Info.SeeThrough && isBuilding && CityView.SeeThrough)
				top = back.Y - Info.StubHeight;
			else
			{
				top = back.Y;
				foreach (var ab in autoBounds)
				{
					var b = ab.AutoMouseoverBounds(self, wr);
					if (!b.IsEmpty)
					{
						top = Math.Min(top, b.Top);
						break;
					}
				}
			}

			if (top >= back.Y)
				return new Polygon([back, right, front, left]);

			return new Polygon([left, front, right, new int2(right.X, top), new int2(left.X, top)]);
		}
	}
}
