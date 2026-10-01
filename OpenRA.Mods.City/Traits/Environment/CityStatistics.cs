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
using OpenRA.Traits;

namespace OpenRA.Mods.City.Traits
{
	/// <summary>One chirper message. Text is a fluent key; the UI formats it with `$arg`.</summary>
	public struct ChirpEntry
	{
		public int Tick;
		public string Key;
		public int CitizenId;
		public string Arg;

		/// <summary>Importance (affected buildings or citizens); many likes mean a widespread problem.</summary>
		public int Likes;

		/// <summary>Where it happened (for the locate button), or CPos.Zero.</summary>
		public CPos Cell;

		/// <summary>True when Cell is a real location.</summary>
		public bool HasCell;

		/// <summary>Author display text when CitizenId is 0 (e.g. "City Hall"), or null.</summary>
		public string Author;
	}

	[TraitLocation(SystemActors.World)]
	[Desc("Monthly statistics history (ring buffers, one slot per game month) and the chirper feed.")]
	public class CityStatisticsInfo : TraitInfo
	{
		[Desc("Months of history kept per series (120 = ten years).")]
		public readonly int Months = 120;

		[Desc("Chirper messages kept.")]
		public readonly int ChirpCapacity = 200;

		[Desc("Maximum chirps posted per game day (a day is one month); citizen life events are exempt.")]
		public readonly int MaxChirpsPerDay = 6;

		[Desc("Samples are taken this many ticks after midnight, after the monthly settlement of the economy.")]
		public readonly int SampleDelay = 1;

		[Desc("Post the built-in chirps (milestones, shortages, weather).")]
		public readonly bool BuiltInChirps = true;

		public override object Create(ActorInitializer init) { return new CityStatistics(init.Self, this); }
	}

	/// <summary>
	/// World trait implementing ICityStatistics. Series are named ints. Other work packages either call Record(series, value) whenever they
	/// have a fresh value (the last write before the month rolls over wins), or Register(series, sampler) once.
	/// </summary>
	public partial class CityStatistics : ICityStatistics, ITick, ISync, ICityAutoTestReporter, IChirperSource
	{
		sealed class Series
		{
			public string Name;
			public int[] Ring;
			public int Count;
			public int Head;
			public int Pending;
			public bool HasPending;
			public bool External;
			public Func<int> Sampler;
		}

		public readonly CityStatisticsInfo Info;
		readonly World world;
		readonly List<Series> series = [];
		readonly Dictionary<string, int> index = [];
		readonly List<ChirpEntry> chirps = [];
		readonly Dictionary<string, int> chirpsToday = [];
		CityClock clock;
		int chirpDay = -1;
		int chirpsPostedToday;

		public CityStatistics(Actor self, CityStatisticsInfo info)
		{
			Info = info;
			world = self.World;
		}

		[VerifySync]
		public int StateHash { get; private set; }

		/// <summary>Number of completed months (slots filled in the longest series).</summary>
		public int MonthsRecorded { get; private set; }

		public int SeriesCount => series.Count;

		/// <summary>Bumped when a month is closed or a chirp is posted (for UI refresh).</summary>
		public int Version { get; private set; }

		/// <summary>Chirper messages, oldest first.</summary>
		public IReadOnlyList<ChirpEntry> Chirps => chirps;

		/// <summary>Bumped on every new chirp (UI redraw trigger).</summary>
		public int ChirpVersion { get; private set; }

		int IChirperSource.Version => ChirpVersion;
		IReadOnlyList<ChirpEntry> IChirperSource.Entries => chirps;

		public string SeriesName(int i) { return series[i].Name; }

		Series GetOrCreate(string name)
		{
			if (index.TryGetValue(name, out var i))
				return series[i];

			var s = new Series { Name = name, Ring = new int[Math.Max(12, Info.Months)] };
			index[name] = series.Count;
			series.Add(s);
			return s;
		}

		/// <summary>Registers a series sampled automatically when a month closes (unless Record was called for it that month).</summary>
		public void Register(string name, Func<int> sampler)
		{
			GetOrCreate(name).Sampler = sampler;
		}

		public void Record(string name, int value)
		{
			var s = GetOrCreate(name);
			s.Pending = value;
			s.HasPending = true;
			s.External = true;
		}

		/// <summary>The most recent closed month of a series (0 if none).</summary>
		public int Latest(string name)
		{
			if (!index.TryGetValue(name, out var i))
				return 0;

			var s = series[i];
			return s.Count == 0 ? 0 : s.Ring[(s.Head + s.Ring.Length - 1) % s.Ring.Length];
		}

		/// <summary>Value being collected for the running month (last Record, else the latest closed month).</summary>
		public int Current(string name)
		{
			if (!index.TryGetValue(name, out var i))
				return 0;

			var s = series[i];
			if (s.HasPending)
				return s.Pending;

			if (s.Sampler != null)
				return s.Sampler();

			return Latest(name);
		}

		public IReadOnlyList<int> History(string name, int count)
		{
			if (!index.TryGetValue(name, out var i))
				return [];

			var s = series[i];
			var n = Math.Min(Math.Max(0, count), s.Count);
			var result = new int[n];
			for (var k = 0; k < n; k++)
				result[k] = s.Ring[(s.Head - n + k + s.Ring.Length * 2) % s.Ring.Length];

			return result;
		}

		void ITick.Tick(Actor self)
		{
			clock ??= self.TraitOrDefault<CityClock>();
			if (clock == null)
				return;

			var delay = Math.Max(1, Info.SampleDelay);
			if (world.WorldTick > delay && clock.TickOfDay == delay)
				CloseMonth();

			if (Info.BuiltInChirps && world.WorldTick % 25 == 3)
				DetectEvents();
		}

		void CloseMonth()
		{
			RegisterResourceSeries();
			RefreshExposure();
			for (var k = 0; k < series.Count; k++)
			{
				var s = series[k];
				if (!s.External && s.Sampler != null)
				{
					s.Pending = s.Sampler();
					s.HasPending = true;
				}

				if (!s.HasPending && s.Count == 0)
					continue;

				var v = s.HasPending ? s.Pending : s.Ring[(s.Head + s.Ring.Length - 1) % s.Ring.Length];
				s.Ring[s.Head] = v;
				s.Head = (s.Head + 1) % s.Ring.Length;
				s.Count = Math.Min(s.Ring.Length, s.Count + 1);
				s.HasPending = false;
				s.External = false;
				MonthsRecorded = Math.Max(MonthsRecorded, s.Count);
			}

			unchecked
			{
				var h = 17 + MonthsRecorded;
				for (var k = 0; k < series.Count; k++)
				{
					var s = series[k];
					h = h * 31 + (s.Count == 0 ? 0 : s.Ring[(s.Head + s.Ring.Length - 1) % s.Ring.Length]);
				}

				StateHash = h * 31 + chirps.Count;
			}

			Version++;
		}

		/// <summary>Posts a chirper message. Fluent key with an optional `$arg`; citizenId 0 = a city service.</summary>
		public void Chirp(string fluentKey, int citizenId = 0, string arg = null)
		{
			Post(fluentKey, citizenId, arg, 0, CPos.Zero);
		}

		/// <summary>Posts a chirper message with importance and a map location for the locate button.</summary>
		public bool Post(string fluentKey, int citizenId, string arg, int likes, CPos cell)
		{
			if (string.IsNullOrEmpty(fluentKey))
				return false;

			var day = clock?.DayIndex ?? 0;
			if (day != chirpDay)
			{
				chirpDay = day;
				chirpsPostedToday = 0;
				chirpsToday.Clear();
			}

			// Citizen life events are exempt from the daily cap; everything else is limited and de-duplicated per day.
			if (citizenId == 0)
			{
				if (chirpsPostedToday >= Info.MaxChirpsPerDay || chirpsToday.ContainsKey(fluentKey))
					return false;

				chirpsToday[fluentKey] = 1;
				chirpsPostedToday++;
			}

			chirps.Add(new ChirpEntry
			{
				Tick = world.WorldTick,
				Key = fluentKey,
				CitizenId = citizenId,
				Arg = arg,
				Likes = likes,
				Cell = cell,
				HasCell = cell != CPos.Zero
			});
			ChirpVersion++;
			if (chirps.Count > Info.ChirpCapacity)
				chirps.RemoveAt(0);

			Version++;
			return true;
		}

		string ICityAutoTestReporter.AutoTestReport()
		{
			var last = chirps.Count > 0 ? chirps[^1].Key : "-";
			return $"stats series={series.Count} months={MonthsRecorded} pop={Latest("population")} money={Latest("money")} " +
				$"chirps={chirps.Count} last={last} hash={StateHash}";
		}
	}
}
