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
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class PixelSnapTest
	{
		[TestCase(1f)]
		[TestCase(2f)]
		[TestCase(3f)]
		public void IntegerScalesKeepIntegerGeometry(float scale)
		{
			for (var x = -5; x < 40; x++)
			{
				for (var w = 1; w < 12; w++)
				{
					float o = x, l = w;
					PixelSnap.SnapSpan(scale, ref o, ref l);
					Assert.That(o, Is.EqualTo(x).Within(1e-4f));
					Assert.That(l, Is.EqualTo(w).Within(1e-4f));
				}
			}
		}

		[TestCase(0.75f)]
		[TestCase(1.25f)]
		[TestCase(1.5f)]
		[TestCase(1.75f)]
		[TestCase(2.5f)]
		public void ThinSpansHaveUniformDeviceWidth(float scale)
		{
			var expected = PixelSnap.DeviceLength(1, scale);
			for (var x = 0; x < 64; x++)
			{
				float o = x, l = 1;
				PixelSnap.SnapSpan(scale, ref o, ref l);
				Assert.That(l * scale, Is.EqualTo(expected).Within(1e-3f), $"x={x}");
				Assert.That(o * scale, Is.EqualTo(MathF.Round(o * scale)).Within(1e-3f), $"x={x}");
			}
		}

		[TestCase(1.25f)]
		[TestCase(1.5f)]
		[TestCase(1.75f)]
		public void WideSpansTileWithoutGaps(float scale)
		{
			// Adjacent wide spans [0, 10), [10, 20), ... must share their device pixel edges
			for (var x = 0; x < 200; x += 10)
			{
				float a = x, la = 10, b = x + 10, lb = 10;
				PixelSnap.SnapSpan(scale, ref a, ref la);
				PixelSnap.SnapSpan(scale, ref b, ref lb);
				Assert.That((a + la) * scale, Is.EqualTo(b * scale).Within(1e-3f));
			}
		}

		[Test]
		public void DeviceRectangleMatchesSnappedSpans()
		{
			var r = PixelSnap.ToDevice(new Rectangle(3, 5, 10, 1), 1.5f);
			Assert.That(r, Is.EqualTo(new Rectangle(4, 7, 15, 2)));
		}
	}
}
