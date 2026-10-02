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
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	[Flags]
	public enum ScrollDirection { None = 0, Up = 1, Left = 2, Down = 4, Right = 8 }

	public interface INotifyViewportZoomExtentsChanged
	{
		void ViewportZoomExtentsChanged(float minZoom, float maxZoom);
	}

	public static class ViewportExts
	{
		public static bool Includes(this ScrollDirection d, ScrollDirection s)
		{
			// PERF: Enum.HasFlag is slower and requires allocations.
			return (d & s) == s;
		}

		public static ScrollDirection Set(this ScrollDirection d, ScrollDirection s, bool val)
		{
			return (d.Includes(s) != val) ? d ^ s : d;
		}
	}

	public class Viewport
	{
		readonly WorldRenderer worldRenderer;
		readonly WorldViewportSizes viewportSizes;
		readonly GraphicSettings graphicSettings;

		// Map bounds (world-px)
		readonly Rectangle mapBounds;
		readonly Size tileSize;

		// Isometric projection: the ground rectangle (world units) that the point under the view center is kept inside.
		readonly bool isometric;
		readonly Vector2 isoGroundMin;
		readonly Vector2 isoGroundMax;

		/// <summary>Isometric projection: how far (world px) sprites may rise above the top of the map diamond before the scissor clips them.</summary>
		public const int IsometricScissorTopMargin = 1024;

		/// <summary>Isometric projection: how many cells inside the map bounds the ground point under the view center is kept.</summary>
		public const int IsometricViewInset = 2;

		// Viewport geometry (world-px)
		public Vector2 CenterLocation { get; private set; }

		public WPos CenterPosition => worldRenderer.ProjectedPosition(int2.FromVector(CenterLocation));

		public int2 TopLeft => int2.FromVector(CenterLocation) - ViewportSize.ToInt2() / 2;
		public int2 BottomRight => int2.FromVector(CenterLocation) + ViewportSize.ToInt2() / 2;
		public Size ViewportSize { get; private set; }
		ProjectedCellRegion cells;
		bool cellsDirty = true;

		ProjectedCellRegion allCells;
		bool allCellsDirty = true;

		WorldViewport lastViewportDistance;
		Size lastNativeResolution;

		float zoom = 1f;
		bool unlockMinZoom;
		float unlockedMinZoomScale;
		float unlockedMinZoom = 1f;
		float defaultScale;
		bool overrideUserScale;

		// Zoom level snapping and animation (WorldViewportSizes.ZoomLevels).
		float[] zoomLevels = [];
		float defaultLevelZoom = 1f;
		float zoomStepAccumulator;
		bool zoomAnimating;
		float zoomFrom;
		float zoomTarget;
		long zoomStartTime;
		int2? zoomAnchor;

		public Func<Vector2> ViewportCenterProvider;
		public event Action ViewportTick;

		public float Zoom
		{
			get => zoom;

			private set
			{
				zoom = value;
				ViewportSize = Size.FromVector(1f / zoom * Game.Renderer.NativeResolution.ToVector2());
				if (viewportSizes.KeepViewInsideMap)
					CenterLocation = ClampCenter(CenterLocation);
				cellsDirty = true;
				allCellsDirty = true;
			}
		}

		public float MinZoom { get; private set; } = 1f;
		public float MaxZoom { get; private set; } = 2f;

		public void OverrideDefaultHeight(float height)
		{
			defaultScale = viewportSizes.DefaultScale * Game.Renderer.NativeResolution.Height / height;
			overrideUserScale = true;
			UpdateViewportZooms(false);
		}

		public void AdjustZoom(float dz)
		{
			if (zoomLevels.Length > 0)
			{
				StepZoom(dz, null);
				return;
			}

			// Exponential ensures that equal positive and negative steps have the same effect
			Zoom = (zoom * (float)Math.Exp(dz)).Clamp(unlockMinZoom ? unlockedMinZoom : MinZoom, MaxZoom);
		}

		public void AdjustZoom(float dz, int2 center)
		{
			if (zoomLevels.Length > 0)
			{
				StepZoom(dz, center);
				return;
			}

			var oldCenter = worldRenderer.Viewport.ViewToWorldPx(center);
			AdjustZoom(dz);
			var newCenter = worldRenderer.Viewport.ViewToWorldPx(center);

			var candidateCenterLocation = CenterLocation + (oldCenter - newCenter).ToVector2();
			CenterLocation = ClampCenter(candidateCenterLocation);
		}

		/// <summary>
		/// Sets the zoom immediately (no zoom level snapping or animation), keeping the world point under
		/// <paramref name="anchor"/> (view pixels, default: the viewport center) in place.
		/// </summary>
		public void SetZoom(float value, int2? anchor = null)
		{
			zoomAnimating = false;
			ApplyZoom(value.Clamp(unlockMinZoom ? unlockedMinZoom : MinZoom, MaxZoom), anchor);
		}

		/// <summary>Steps between the configured ZoomLevels: one wheel notch or hotkey press is one level.</summary>
		void StepZoom(float dz, int2? anchor)
		{
			if (dz == 0)
				return;

			// Small (e.g. touchpad) deltas accumulate until they add up to a wheel notch.
			if (Math.Sign(dz) != Math.Sign(zoomStepAccumulator))
				zoomStepAccumulator = 0;

			zoomStepAccumulator += dz;
			var notch = 0.999f * Math.Clamp(Game.Settings.Game.ZoomSpeed, 0.001f, 0.25f);
			var steps = (int)(zoomStepAccumulator / notch);
			if (steps == 0)
				return;

			zoomStepAccumulator -= steps * notch;

			// Continue from the level that is being animated towards, so quick wheel spins skip ahead.
			var from = zoomAnimating ? zoomTarget : zoom;
			var index = Array.FindLastIndex(zoomLevels, l => l <= from * 1.001f);
			if (steps > 0 && (index < 0 || zoomLevels[index] < from * 0.999f))
				steps--;

			index = Math.Clamp(index + steps, 0, zoomLevels.Length - 1);
			AnimateZoomTo(zoomLevels[index], anchor);
		}

		void AnimateZoomTo(float target, int2? anchor)
		{
			zoomAnchor = anchor;
			if (viewportSizes.ZoomAnimationDuration <= 0 || target == zoom)
			{
				zoomAnimating = false;
				ApplyZoom(target, anchor);
				return;
			}

			zoomFrom = zoom;
			zoomTarget = target;
			zoomStartTime = Game.RunTime;
			zoomAnimating = true;
		}

		void TickZoomAnimation()
		{
			if (!zoomAnimating)
				return;

			// Ease out in log space, so that zooming in and out feel symmetric.
			var t = Math.Clamp((float)(Game.RunTime - zoomStartTime) / viewportSizes.ZoomAnimationDuration, 0f, 1f);
			var eased = 1 - (1 - t) * (1 - t) * (1 - t);
			var z = t >= 1 ? zoomTarget : (float)Math.Exp(float.Lerp(MathF.Log(zoomFrom), MathF.Log(zoomTarget), eased));
			zoomAnimating = t < 1;
			ApplyZoom(z, zoomAnchor);
		}

		/// <summary>Changes the zoom while keeping the world point under <paramref name="anchor"/> (view pixels) fixed.</summary>
		void ApplyZoom(float value, int2? anchor)
		{
			var view = (anchor ?? (Game.Renderer.Resolution.ToInt2() / 2)).ToVector2();
			var before = ViewToWorldPxF(view);
			Zoom = value;
			var after = ViewToWorldPxF(view);
			CenterLocation = ClampCenter(CenterLocation + before - after);
		}

		Vector2 ViewToWorldPxF(Vector2 view)
			=> Game.Renderer.UIScale / Zoom * view + CenterLocation - (ViewportSize.ToInt2() / 2).ToVector2();

		public void ToggleZoom()
		{
			if (zoomLevels.Length > 0)
			{
				// Reset to the default level, or jump to the closest level when already there.
				AnimateZoomTo(Math.Abs(zoom - defaultLevelZoom) > 0.001f ? defaultLevelZoom : MaxZoom, null);
				return;
			}

			// Unlocked zooms always reset to the default zoom
			if (zoom < MinZoom)
				Zoom = MinZoom;
			else
				Zoom = zoom > MinZoom ? MinZoom : MaxZoom;
		}

		public void UnlockMinimumZoom(float scale)
		{
			unlockMinZoom = true;
			unlockedMinZoomScale = scale;
			UpdateViewportZooms(false);
		}

		public static long LastMoveRunTime = 0;
		public static int2 LastMousePos;

		public ScrollDirection GetBlockedDirections()
		{
			var ret = ScrollDirection.None;
			if (isometric)
			{
				// A direction is blocked when the clamp keeps less than half of a small step in that direction.
				const float Step = 4f;
				foreach (var (dir, flag) in IsoScrollDirections)
				{
					var moved = Vector2.Dot(ClampCenter(CenterLocation + Step * dir) - CenterLocation, dir);
					if (moved < Step / 2)
						ret |= flag;
				}

				return ret;
			}

			var (min, max) = CenterRange();
			if (CenterLocation.Y <= min.Y)
				ret |= ScrollDirection.Up;
			if (CenterLocation.X <= min.X)
				ret |= ScrollDirection.Left;
			if (CenterLocation.Y >= max.Y)
				ret |= ScrollDirection.Down;
			if (CenterLocation.X >= max.X)
				ret |= ScrollDirection.Right;

			return ret;
		}

		static readonly (Vector2 Dir, ScrollDirection Flag)[] IsoScrollDirections =
		[
			(new Vector2(0, -1), ScrollDirection.Up),
			(new Vector2(-1, 0), ScrollDirection.Left),
			(new Vector2(0, 1), ScrollDirection.Down),
			(new Vector2(1, 0), ScrollDirection.Right),
		];

		public Viewport(WorldRenderer wr, Map map)
		{
			worldRenderer = wr;
			tileSize = map.Rules.TerrainInfo.TileSize;
			viewportSizes = Game.ModData.GetOrCreate<WorldViewportSizes>();
			graphicSettings = Game.Settings.Graphics;
			defaultScale = viewportSizes.DefaultScale;

			// Calculate map bounds in world-px
			if (wr.World.Type == WorldType.Editor)
			{
				// The full map is visible in the editor
				var width = map.MapSize.Width * tileSize.Width;
				var height = map.MapSize.Height * tileSize.Height;
				if (wr.World.Map.Grid.Type == MapGridType.RectangularIsometric)
					height /= 2;

				mapBounds = new Rectangle(0, 0, width, height);
				CenterLocation = new int2(width / 2, height / 2).ToVector2();
			}
			else if (wr.IsIsometric)
			{
				// The map bounds are a diamond on screen: use the bounding box of its four corners.
				isometric = true;
				var b = map.Bounds;
				var ts = map.Grid.TileScale;
				var corners = new[]
				{
					wr.ScreenPxPosition(new WPos(b.Left * ts, b.Top * ts, 0)),
					wr.ScreenPxPosition(new WPos(b.Right * ts, b.Top * ts, 0)),
					wr.ScreenPxPosition(new WPos(b.Right * ts, b.Bottom * ts, 0)),
					wr.ScreenPxPosition(new WPos(b.Left * ts, b.Bottom * ts, 0)),
				};

				mapBounds = Rectangle.FromLTRB(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y));

				// Keep the ground point under the view center a few cells inside the bounds (never past their middle).
				var insetX = Math.Min(IsometricViewInset * ts, b.Width * ts / 2);
				var insetY = Math.Min(IsometricViewInset * ts, b.Height * ts / 2);
				isoGroundMin = new Vector2(b.Left * ts + insetX, b.Top * ts + insetY);
				isoGroundMax = new Vector2(b.Right * ts - insetX, b.Bottom * ts - insetY);
				CenterLocation = wr.ScreenPosition((isoGroundMin + isoGroundMax) / 2);
			}
			else
			{
				var tl = wr.ScreenPxPosition(map.ProjectedTopLeft);
				var br = wr.ScreenPxPosition(map.ProjectedBottomRight);
				mapBounds = Rectangle.FromLTRB(tl.X, tl.Y, br.X, br.Y);
				CenterLocation = ((tl + br) / 2).ToVector2();
			}

			UpdateViewportZooms();
		}

		public void Tick()
		{
			if (lastViewportDistance != graphicSettings.ViewportDistance)
				UpdateViewportZooms();
			else if (lastNativeResolution != Game.Renderer.NativeResolution)
			{
				// Keep the camera center and zoom while refreshing the visible region and buffer capacity.
				UpdateViewportZooms(false);
			}

			TickZoomAnimation();

			if (ViewportCenterProvider != null)
				Center(ViewportCenterProvider());

			ViewportTick?.Invoke();
		}

		static float CalculateMinimumZoom(float minHeight, float maxHeight)
		{
			var h = Game.Renderer.NativeResolution.Height;

			// Check the easy case: the native resolution is within the maximum limit
			// Also catches the case where the user may force a resolution smaller than the minimum window size
			if (h <= maxHeight)
				return 1;

			// Find a clean fraction that brings us within the desired range to reduce aliasing
			var step = 1f;
			while (true)
			{
				var testZoom = 1f;
				while (true)
				{
					var nextZoom = testZoom + step;
					if (h < minHeight * nextZoom)
						break;

					testZoom = nextZoom;
				}

				if (h < maxHeight * testZoom)
					return testZoom;

				step /= 2;
			}
		}

		void UpdateViewportZooms(bool resetCurrentZoom = true)
		{
			lastViewportDistance = graphicSettings.ViewportDistance;
			lastNativeResolution = Game.Renderer.NativeResolution;

			var vd = graphicSettings.ViewportDistance;
			if (overrideUserScale || (viewportSizes.AllowNativeZoom && vd == WorldViewport.Native))
				MinZoom = defaultScale;
			else
			{
				var range = viewportSizes.GetSizeRange(vd);
				MinZoom = CalculateMinimumZoom(range.X, range.Y) * defaultScale;
			}

			MaxZoom = Math.Max(MinZoom, Math.Min(
				MinZoom * viewportSizes.MaxZoomScale,
				Game.Renderer.NativeResolution.Height * defaultScale / viewportSizes.MaxZoomWindowHeight));

			if (viewportSizes.ZoomLevels.Length > 0)
				UpdateZoomLevels();
			else if (unlockMinZoom)
			{
				// Spectators and the map editor support zooming out by an extra factor of two.
				// TODO: Allow zooming out until the full map is visible
				// We need to improve our viewport scroll handling to center the map as we zoom out
				// before this will work well enough to enable
				unlockedMinZoom = MinZoom * unlockedMinZoomScale;
			}

			zoomAnimating = false;
			if (resetCurrentZoom)
				Zoom = zoomLevels.Length > 0 ? defaultLevelZoom : MinZoom;
			else
				Zoom = Zoom.Clamp(unlockMinZoom ? unlockedMinZoom : MinZoom, MaxZoom);

			var minZoom = unlockMinZoom ? unlockedMinZoom : MinZoom;
			var maxSize = Size.FromVector(1f / minZoom * Game.Renderer.NativeResolution.ToVector2());
			Game.Renderer.SetMaximumViewportSize(maxSize);

			foreach (var t in worldRenderer.World.WorldActor.TraitsImplementing<INotifyViewportZoomExtentsChanged>())
				t.ViewportZoomExtentsChanged(minZoom, MaxZoom);
		}

		/// <summary>
		/// Fixed zoom levels replace the continuous zoom range: the levels themselves are absolute (native window pixels per
		/// world pixel) so that whole levels stay pixel-perfect, and the viewport distance setting picks the closest default level.
		/// </summary>
		void UpdateZoomLevels()
		{
			// MinZoom still holds the zoom that the classic viewport distance logic picked for this window size.
			var preferred = MinZoom;
			var levels = viewportSizes.ZoomLevels.Where(l => l > 0).Order().ToList();
			if (unlockMinZoom)
			{
				// Spectators and the map editor may zoom out further, in halving steps.
				var unlockedMin = levels[0] * unlockedMinZoomScale;
				for (var l = levels[0] / 2; l >= unlockedMin * 0.999f; l /= 2)
					levels.Insert(0, l);
			}

			zoomLevels = [.. levels];
			MinZoom = viewportSizes.ZoomLevels.Where(l => l > 0).Min();
			MaxZoom = zoomLevels[^1];
			unlockedMinZoom = zoomLevels[0];
			defaultLevelZoom = zoomLevels.MinBy(l => Math.Abs(Math.Log(l / preferred)));
		}

		/// <summary>
		/// The range that the viewport center may take: the map bounds, or (WorldViewportSizes.KeepViewInsideMap) the map bounds
		/// shrunk by half the viewport, so that zooming out never reveals the void beyond the map edge.
		/// A map axis smaller than the viewport keeps the view centred on that axis.
		/// </summary>
		(Vector2 Min, Vector2 Max) CenterRange()
		{
			Vector2 min = new(mapBounds.Left, mapBounds.Top);
			Vector2 max = new(mapBounds.Right, mapBounds.Bottom);
			if (!viewportSizes.KeepViewInsideMap)
				return (min, max);

			var half = ViewportSize.ToInt2().ToVector2() / 2;
			var mid = (min + max) / 2;
			min += half;
			max -= half;
			if (min.X > max.X)
				min.X = max.X = mid.X;
			if (min.Y > max.Y)
				min.Y = max.Y = mid.Y;

			return (min, max);
		}

		Vector2 ClampCenter(Vector2 center)
		{
			if (isometric)
			{
				// Keep the ground point under the view center inside the (inset) map bounds: the view slides along the
				// diamond's edges and stops at its corners. Positions that are already inside are returned unchanged.
				var ground = worldRenderer.GroundPosition(center);
				var clamped = Vector2.Clamp(ground, isoGroundMin, isoGroundMax);
				return clamped == ground ? center : worldRenderer.ScreenPosition(clamped);
			}

			var (min, max) = CenterRange();
			return Vector2.Clamp(center, min, max);
		}

		/// <summary>
		/// Moves the view to the map edge in the given screen direction. Under the isometric projection this keeps the
		/// screen column (up/down) or row (left/right) and stops at the diamond's edge.
		/// </summary>
		public void JumpToMapEdge(ScrollDirection direction)
		{
			var map = worldRenderer.World.Map;
			if (!isometric)
			{
				var center = CenterPosition;
				if (direction == ScrollDirection.Up)
					Center(new WPos(center.X, 0, 0));
				else if (direction == ScrollDirection.Down)
					Center(new WPos(center.X, map.ProjectedBottomRight.Y, 0));
				else if (direction == ScrollDirection.Left)
					Center(new WPos(0, center.Y, 0));
				else if (direction == ScrollDirection.Right)
					Center(new WPos(map.ProjectedBottomRight.X, center.Y, 0));

				return;
			}

			// d = x - y is the screen column and s = x + y the screen row of a ground point.
			var g = worldRenderer.GroundPosition(CenterLocation);
			var (min, max) = (isoGroundMin, isoGroundMax);
			var d = Math.Clamp(g.X - g.Y, min.X - max.Y, max.X - min.Y);
			var s = Math.Clamp(g.X + g.Y, min.X + min.Y, max.X + max.Y);
			if (direction == ScrollDirection.Up)
				s = Math.Max(2 * min.X - d, 2 * min.Y + d);
			else if (direction == ScrollDirection.Down)
				s = Math.Min(2 * max.X - d, 2 * max.Y + d);
			else if (direction == ScrollDirection.Left)
				d = Math.Max(2 * min.X - s, s - 2 * max.Y);
			else if (direction == ScrollDirection.Right)
				d = Math.Min(2 * max.X - s, s - 2 * min.Y);

			CenterLocation = ClampCenter(worldRenderer.ScreenPosition(new Vector2((s + d) / 2, (s - d) / 2)));
			cellsDirty = true;
			allCellsDirty = true;
		}

		public CPos ViewToWorld(int2 view)
		{
			var world = ViewToWorldPx(view);
			var map = worldRenderer.World.Map;
			var candidates = CandidateMouseoverCells(world).ToList();

			foreach (var uv in candidates)
			{
				// Coarse filter to nearby cells
				var p = map.CenterOfCell(uv.ToCPos(map.Grid.Type));
				var s = worldRenderer.ScreenPxPosition(p);
				if (Math.Abs(s.X - world.X) <= tileSize.Width && Math.Abs(s.Y - world.Y) <= tileSize.Height)
				{
					var ramp = map.Grid.Ramps[map.Ramp.Contains(uv) ? map.Ramp[uv] : 0];
					var pos = map.CenterOfCell(uv.ToCPos(map)) - new WVec(0, 0, ramp.CenterHeightOffset);
					var screen = ramp.Corners.Select(c => worldRenderer.ScreenPxPosition(pos + c)).ToImmutableArray();
					if (screen.PolygonContains(world))
						return uv.ToCPos(map);
				}
			}

			// Mouse is not directly over a cell (perhaps on a cliff)
			// Try and find the closest cell
			if (candidates.Count > 0)
			{
				return candidates.MinBy(uv =>
				{
					var p = map.CenterOfCell(uv.ToCPos(map.Grid.Type));
					var s = worldRenderer.ScreenPxPosition(p);
					var dx = Math.Abs(s.X - world.X);
					var dy = Math.Abs(s.Y - world.Y);

					return dx * dx + dy * dy;
				}).ToCPos(map);
			}

			// Something is very wrong, but lets return something that isn't completely bogus and hope the caller can recover
			return worldRenderer.World.Map.CellContaining(worldRenderer.ProjectedPosition(ViewToWorldPx(view)));
		}

		/// <summary>Returns an unfiltered list of all cells that could potentially contain the mouse cursor.</summary>
		IEnumerable<MPos> CandidateMouseoverCells(int2 world)
		{
			var map = worldRenderer.World.Map;
			var tileScale = map.Grid.TileScale / 2;
			var minPos = worldRenderer.ProjectedPosition(world);

			// Find all the cells that could potentially have been clicked.
			MPos a;
			MPos b;
			if (map.Grid.Type == MapGridType.RectangularIsometric)
			{
				// TODO: this generates too many cells.
				a = map.CellContaining(minPos - new WVec(tileScale, 0, 0)).ToMPos(map.Grid.Type);
				b = map.CellContaining(minPos + new WVec(tileScale, tileScale * map.Grid.MaximumTerrainHeight, 0)).ToMPos(map.Grid.Type);
			}
			else
			{
				a = map.CellContaining(minPos).ToMPos(map.Grid.Type);
				b = map.CellContaining(minPos + new WVec(0, tileScale * map.Grid.MaximumTerrainHeight, 0)).ToMPos(map.Grid.Type);
			}

			for (var v = b.V; v >= a.V; v--)
				for (var u = b.U; u >= a.U; u--)
					yield return new MPos(u, v);
		}

		public int2 ViewToWorldPx(int2 view)
			=> int2.FromVector(Game.Renderer.UIScale / Zoom * view.ToVector2() + CenterLocation - (ViewportSize.ToInt2() / 2).ToVector2());

		public int2 WorldToViewPx(int2 world)
			=> int2.FromVector(Zoom / Game.Renderer.UIScale * (world.ToVector2() - CenterLocation + (ViewportSize.ToInt2() / 2).ToVector2()));

		public int2 WorldToViewPx(in Vector3 world)
			=> int2.FromVector(Zoom / Game.Renderer.UIScale * (world.AsVector2() - CenterLocation + ViewportSize.ToVector2() / 2));

		public void Center(IEnumerable<Actor> actors)
		{
			var actorsCollection = actors as IReadOnlyCollection<Actor>;
			actorsCollection ??= actors.ToList();

			if (actorsCollection.Count == 0)
				return;

			Center(actorsCollection.Select(a => a.CenterPosition).Average());
		}

		public void Center(WPos pos)
		{
			CenterLocation = ClampCenter(worldRenderer.ScreenPxPosition(pos).ToVector2());
			cellsDirty = true;
			allCellsDirty = true;
		}

		public void Center(Vector2 pos)
		{
			CenterLocation = ClampCenter(worldRenderer.ScreenPosition(pos));
			cellsDirty = true;
			allCellsDirty = true;
		}

		public void Scroll(Vector2 delta, bool ignoreBorders)
		{
			// Convert scroll delta from world-px to viewport-px
			CenterLocation += 1f / Zoom * delta;
			cellsDirty = true;
			allCellsDirty = true;

			if (!ignoreBorders)
				CenterLocation = ClampCenter(CenterLocation);
		}

		// Rectangle (in viewport coords) that contains things to be drawn
		public Rectangle GetScissorBounds(bool insideBounds)
		{
			if (isometric)
			{
				// The map is a diamond: clip to its bounding box (the void around it is drawn by an IRenderBackdrop), with
				// extra room above for tall sprites on the back rows. Cells outside the bounds are not drawn by the terrain.
				var r = insideBounds
					? Rectangle.FromLTRB(mapBounds.Left - tileSize.Width / 2, mapBounds.Top - IsometricScissorTopMargin,
						mapBounds.Right + tileSize.Width / 2, mapBounds.Bottom + tileSize.Height / 2)
					: new Rectangle(TopLeft.X, TopLeft.Y, ViewportSize.Width, ViewportSize.Height);
				var view = new Rectangle(TopLeft.X, TopLeft.Y, ViewportSize.Width, ViewportSize.Height);
				r = Rectangle.Intersect(r, view);
				return new Rectangle(r.X - TopLeft.X, r.Y - TopLeft.Y, Math.Max(0, r.Width), Math.Max(0, r.Height));
			}

			// Visible rectangle in world coordinates (expanded to the corners of the cells)
			var bounds = insideBounds ? VisibleCellsInsideBounds : AllVisibleCells;
			var map = worldRenderer.World.Map;
			var ctl = map.CenterOfCell(((MPos)bounds.TopLeft).ToCPos(map)) - new WVec(512, 512, 0);
			var cbr = map.CenterOfCell(((MPos)bounds.BottomRight).ToCPos(map)) + new WVec(512, 512, 0);

			// Convert to screen coordinates
			var tl = worldRenderer.ScreenPxPosition(ctl - new WVec(0, 0, ctl.Z)) - TopLeft;
			var br = worldRenderer.ScreenPxPosition(cbr - new WVec(0, 0, cbr.Z)) - TopLeft;

			// Add an extra half-cell fudge to avoid clipping isometric tiles
			return Rectangle.FromLTRB(tl.X - tileSize.Width / 2, tl.Y - tileSize.Height / 2,
				br.X + tileSize.Width / 2, br.Y + tileSize.Height / 2);
		}

		ProjectedCellRegion CalculateVisibleCells(bool insideBounds)
		{
			var map = worldRenderer.World.Map;
			if (isometric)
				return CalculateIsometricVisibleCells(map, insideBounds);

			// Calculate the projected cell position at the corners of the visible area
			var tl = (PPos)map.CellContaining(worldRenderer.ProjectedPosition(TopLeft)).ToMPos(map);
			var br = (PPos)map.CellContaining(worldRenderer.ProjectedPosition(BottomRight)).ToMPos(map);

			// RectangularIsometric maps don't have straight edges, and so we need an additional
			// cell margin to include the cells that are half visible on each edge.
			if (map.Grid.Type == MapGridType.RectangularIsometric)
			{
				tl = new PPos(tl.U - 1, tl.V - 1);
				br = new PPos(br.U + 1, br.V + 1);
			}

			// Clamp to the visible map bounds, if requested
			if (insideBounds)
			{
				tl = map.Clamp(tl);
				br = map.Clamp(br);
			}

			return new ProjectedCellRegion(map, tl, br);
		}

		/// <summary>Cell bounding box of the rotated view: all four view corners, plus margins for half-visible edge cells.</summary>
		ProjectedCellRegion CalculateIsometricVisibleCells(Map map, bool insideBounds)
		{
			var tl = TopLeft;
			var br = BottomRight;
			Span<Vector2> corners =
			[
				worldRenderer.GroundPosition(tl.ToVector2()),
				worldRenderer.GroundPosition(new Vector2(br.X, tl.Y)),
				worldRenderer.GroundPosition(br.ToVector2()),
				worldRenderer.GroundPosition(new Vector2(tl.X, br.Y)),
			];

			var min = corners[0];
			var max = corners[0];
			foreach (var c in corners)
			{
				min = Vector2.Min(min, c);
				max = Vector2.Max(max, c);
			}

			// Cell-based renderers draw a little beyond their cell (e.g. props, vehicles): extend the bottom rows that
			// rise into the view, and one cell on each side for half-visible edge cells.
			var ts = (float)map.Grid.TileScale;
			var ctl = new PPos((int)MathF.Floor(min.X / ts) - 1, (int)MathF.Floor(min.Y / ts) - 1);
			var cbr = new PPos((int)MathF.Floor(max.X / ts) + 2, (int)MathF.Floor(max.Y / ts) + 2);
			if (insideBounds)
			{
				ctl = map.Clamp(ctl);
				cbr = map.Clamp(cbr);
			}

			return new ProjectedCellRegion(map, ctl, cbr);
		}

		public ProjectedCellRegion VisibleCellsInsideBounds
		{
			get
			{
				if (cellsDirty)
				{
					cells = CalculateVisibleCells(true);
					cellsDirty = false;
				}

				return cells;
			}
		}

		public ProjectedCellRegion AllVisibleCells
		{
			get
			{
				if (allCellsDirty)
				{
					allCells = CalculateVisibleCells(false);
					allCellsDirty = false;
				}

				return allCells;
			}
		}
	}
}
