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

using System.IO;

namespace OpenRA.Mods.City.UIArt
{
	public sealed partial class CityChromeGenerator
	{
		/// <summary>Fixed-size art, drawn at D(w) x D(h) device pixels. Only the logo is left: the RCT2 kit is in .Rct.cs.</summary>
		ChromeCanvas Art(string recipe, int w, int h)
		{
			return recipe switch
			{
				"logo" => Logo(w, h),
				_ => throw new InvalidDataException($"Unknown chrome art recipe `{recipe}`.")
			};
		}
	}
}
