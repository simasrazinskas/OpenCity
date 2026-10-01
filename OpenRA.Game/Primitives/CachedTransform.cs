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

namespace OpenRA.Primitives
{
	public class CachedTransform<T, U>
	{
		readonly Func<T, U> transform;
		readonly Func<int> version;

		bool initialized;
		T lastInput;
		U lastOutput;
		int lastVersion;

		public CachedTransform(Func<T, U> transform)
		{
			this.transform = transform;
		}

		/// <summary>A cache that is also invalidated whenever `version` returns a new value.</summary>
		public CachedTransform(Func<T, U> transform, Func<int> version)
		{
			this.transform = transform;
			this.version = version;
		}

		public U Update(T input)
		{
			var v = version?.Invoke() ?? 0;
			if (initialized && v == lastVersion && ((input == null && lastInput == null) || (input != null && input.Equals(lastInput))))
				return lastOutput;

			lastVersion = v;
			lastInput = input;
			lastOutput = transform(input);
			initialized = true;

			return lastOutput;
		}
	}
}
