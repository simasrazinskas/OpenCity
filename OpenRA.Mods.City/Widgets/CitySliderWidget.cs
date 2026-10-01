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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.City.Widgets
{
	/// <summary>
	/// Integer slider that follows an external value (GetValue) but lets the user drag freely.
	/// OnChange fires while dragging (cosmetic only), OnCommit fires once when the mouse is released.
	/// </summary>
	public class CitySliderWidget : InputWidget
	{
		public int MinimumValue = 0;
		public int MaximumValue = 30;

		/// <summary>Values snap to multiples of this step (counted from MinimumValue).</summary>
		public int Step = 1;
		public Color TrackColor = Color.FromArgb(150, 8, 14, 22);
		public Color FillColor = CityUi.Accent;
		public Color ThumbColor = Color.White;
		public Func<int> GetValue = () => 0;
		public Action<int> OnChange = _ => { };
		public Action<int> OnCommit = _ => { };

		const int HoldMs = 1500;

		int dragValue;
		bool dragging;
		int pendingValue;
		bool hasPending;
		long pendingUntil;

		public CitySliderWidget() { }

		protected CitySliderWidget(CitySliderWidget other)
			: base(other)
		{
			MinimumValue = other.MinimumValue;
			MaximumValue = other.MaximumValue;
			Step = other.Step;
			TrackColor = other.TrackColor;
			FillColor = other.FillColor;
			ThumbColor = other.ThumbColor;
			GetValue = other.GetValue;
			OnChange = other.OnChange;
			OnCommit = other.OnCommit;
		}

		public override CitySliderWidget Clone() { return new CitySliderWidget(this); }

		/// <summary>The value currently shown: the drag value while dragging, otherwise the external value.</summary>
		public int DisplayValue => CurrentValue;

		int CurrentValue
		{
			get
			{
				if (dragging)
					return dragValue;

				var actual = GetValue();
				if (hasPending)
				{
					if (actual == pendingValue || Game.RunTime > pendingUntil)
						hasPending = false;
					else
						return pendingValue;
				}

				return actual;
			}
		}

		int ValueFromPx(int x)
		{
			var rb = RenderBounds;
			var usable = Math.Max(1, rb.Width - rb.Height);
			var t = (x - rb.X - rb.Height / 2f) / usable;
			var raw = MinimumValue + t * (MaximumValue - MinimumValue);
			var step = Math.Max(1, Step);
			var snapped = MinimumValue + (int)Math.Round((raw - MinimumValue) / step) * step;
			return Math.Clamp(snapped, MinimumValue, MaximumValue);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Button != MouseButton.Left || IsDisabled())
				return false;

			if (mi.Event == MouseInputEvent.Down && !TakeMouseFocus(mi))
				return false;

			if (!HasMouseFocus)
				return false;

			switch (mi.Event)
			{
				case MouseInputEvent.Down:
					dragging = true;
					dragValue = ValueFromPx(mi.Location.X);
					OnChange(dragValue);
					break;

				case MouseInputEvent.Move:
					if (dragging)
					{
						var value = ValueFromPx(mi.Location.X);
						if (value != dragValue)
						{
							dragValue = value;
							OnChange(value);
						}
					}

					break;

				case MouseInputEvent.Up:
					if (dragging)
					{
						dragging = false;
						pendingValue = dragValue;
						hasPending = true;
						pendingUntil = Game.RunTime + HoldMs;
						OnCommit(dragValue);
					}

					YieldMouseFocus(mi);
					break;
			}

			return true;
		}

		public override void Draw()
		{
			var rb = RenderBounds;
			var value = CurrentValue;
			var thumbSize = rb.Height;
			var usable = rb.Width - thumbSize;
			var range = Math.Max(1, MaximumValue - MinimumValue);
			var thumbX = rb.X + usable * (value - MinimumValue) / range;
			var trackHeight = Math.Max(4, rb.Height / 4);
			var trackY = rb.Y + (rb.Height - trackHeight) / 2;

			WidgetUtils.FillRectWithColor(new Rectangle(rb.X + thumbSize / 2, trackY, usable, trackHeight), TrackColor);
			WidgetUtils.FillRectWithColor(new Rectangle(rb.X + thumbSize / 2, trackY, thumbX - rb.X, trackHeight), FillColor);
			WidgetUtils.FillRectWithColor(new Rectangle(thumbX + 2, rb.Y + 2, thumbSize - 4, thumbSize - 4), ThumbColor);
		}
	}
}
