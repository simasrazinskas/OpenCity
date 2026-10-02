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
using System.IO;
using OpenRA.Graphics;

namespace OpenRA.Mods.City.UIArt
{
	public sealed partial class CityChromeGenerator
	{
		/// <summary>The width/height of the repeating middle of a panel bitmap, in device pixels.</summary>
		int Mid => unit * (int)Math.Ceiling(64f / unit);

		/// <summary>Panels declared in yaml with `Panel: kind`. The widget kit itself is drawn by .Rct.cs; only the load screen band is left here.</summary>
		void GeneratePanel(string collection, string kind)
		{
			switch (kind)
			{
				case "Stripe":
					GenerateStripe(collection);
					break;
				default:
					throw new InvalidDataException($"Unknown chrome panel kind `{kind}` for `{collection}` (the RCT kit is generated per family, see chrome.yaml).");
			}
		}

		/// <summary>The load screen band: a dark steel stripe with gold edges, as a centre-only panel 256 logical pixels high.</summary>
		void GenerateStripe(string collection)
		{
			ctx.AddPanel(collection, StripeTile().ToBitmap(), 0, 0, 0, 0, PanelSides.Center);
		}

		ChromeCanvas StripeTile()
		{
			var h = D(256);
			var w = Mid;
			var canvas = new ChromeCanvas(w, h);
			var p = new PixelPainter(canvas, unit);
			int y0 = D(100), y1 = D(156);
			p.Fill(0, y0, w, y1 - y0, C("PanelDark"));
			p.HLine(0, y0, w, C("Gold2"));
			p.HLine(0, y0 + unit, w, C("Gold0"));
			p.HLine(0, y1 - 2 * unit, w, C("Gold0"));
			p.HLine(0, y1 - unit, w, C("Gold2"));
			return canvas;
		}
	}
}
