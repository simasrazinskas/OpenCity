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
using System.Globalization;

namespace OpenRA.Mods.City.Traits
{
	public enum CompanyKind : byte { Extractor, Processor, Retail, Office, Storage }

	/// <summary>How a resource reaches households: not at all, through shops (retail companies resell it) or directly from its producer.</summary>
	public enum SaleMode : byte { None, Shop, Direct }

	[Desc("One resource of the economy catalogue (rules/economy.yaml). Ids are assigned in file order, starting at 1.")]
	public class ResourceDef
	{
		[Desc("Wholesale price in cents (before the economy's MoneyScalePercent).")]
		public readonly int Price = 100;

		[Desc("Transport class 0..5 (0 = immaterial).")]
		public readonly int Weight = 1;

		[Desc("Milli-units one fully staffed worker produces per economy day at 100% efficiency.")]
		public readonly int Q = 1000;

		public readonly SaleMode Sale = SaleMode.None;

		[Desc("Household consumption in milli-units per resident per month.")]
		public readonly int Consumption = 0;

		[Desc("Retail price as percent of wholesale (shops and direct sellers).")]
		public readonly int Markup = 150;

		[Desc("Milli-units a tourist buys per month (0 = tourists do not buy it).")]
		public readonly int Tourist = 0;

		[Desc("False for goods that cannot be traded with the outside (lodging, recreation).")]
		public readonly bool Tradable = true;

		// Filled by the loader (not yaml fields).
		public string Key;
		public byte Id;
		public int Recipe = -1;

		/// <summary>Wholesale price in cents after money scaling.</summary>
		public int PriceCents;

		/// <summary>Price households pay, in cents.</summary>
		public int RetailCents;

		public ResourceDef(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
		}
	}

	[Desc("How a company makes one resource. The recipe's key is the output resource.")]
	public class RecipeDef
	{
		[Desc("Industrial (processor), Office or Commercial (service companies such as restaurants).")]
		public readonly ZoneCategory Zone = ZoneCategory.Industrial;

		[Desc("Inputs per unit of output as 'Resource:count' pairs, e.g. 'Livestock:1, Vegetables:1'.")]
		public readonly string[] Inputs = [];

		// Filled by the loader.
		public string Key;
		public byte Output;
		public CompanyKind Kind;
		public byte[] InputRes = [];
		public int[] InputQty = [];

		public RecipeDef(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
		}
	}

	/// <summary>Resolved resource and recipe tables (data only, no world access).</summary>
	public sealed class EconomyTables
	{
		public readonly ResourceDef[] Resources;
		public readonly RecipeDef[] Recipes;
		public readonly int ResourceCount;

		/// <summary>Resource ids that households buy (shop and direct goods with a consumption), ascending.</summary>
		public readonly byte[] ConsumerResources;

		/// <summary>Resource ids sold to households or tourists through shops or directly, ascending.</summary>
		public readonly byte[] SaleResources;

		readonly Dictionary<string, byte> byName = [];

		public EconomyTables(IReadOnlyList<ResourceDef> resources, IReadOnlyList<RecipeDef> recipes, int moneyScalePercent)
		{
			if (resources.Count > 63)
				throw new YamlException("CityEconomy: at most 63 resources are supported.");

			ResourceCount = resources.Count;
			Resources = new ResourceDef[ResourceCount + 1];
			var consumers = new List<byte>();
			var sales = new List<byte>();
			for (var i = 0; i < resources.Count; i++)
			{
				var r = resources[i];
				r.Id = (byte)(i + 1);
				r.PriceCents = Math.Max(1, r.Price * moneyScalePercent / 100);
				r.RetailCents = r.Sale == SaleMode.None ? r.PriceCents : Math.Max(r.PriceCents, r.PriceCents * r.Markup / 100);
				Resources[r.Id] = r;
				byName[r.Key] = r.Id;
				if (r.Sale != SaleMode.None)
					sales.Add(r.Id);

				if (r.Sale != SaleMode.None && r.Consumption > 0)
					consumers.Add(r.Id);
			}

			ConsumerResources = [.. consumers];
			SaleResources = [.. sales];

			Recipes = new RecipeDef[recipes.Count];
			for (var i = 0; i < recipes.Count; i++)
			{
				var rc = recipes[i];
				rc.Output = Find(rc.Key);
				rc.Kind = rc.Zone == ZoneCategory.Office ? CompanyKind.Office : rc.Zone == ZoneCategory.Commercial ? CompanyKind.Retail : CompanyKind.Processor;
				rc.InputRes = new byte[rc.Inputs.Length];
				rc.InputQty = new int[rc.Inputs.Length];
				for (var j = 0; j < rc.Inputs.Length; j++)
				{
					var parts = rc.Inputs[j].Split(':');
					rc.InputRes[j] = Find(parts[0].Trim());
					rc.InputQty[j] = parts.Length > 1 ? Math.Max(1, int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture)) : 1;
				}

				if (Resources[rc.Output].Recipe >= 0)
					throw new YamlException($"CityEconomy: resource {rc.Key} has two recipes.");

				Resources[rc.Output].Recipe = i;
				Recipes[i] = rc;
			}
		}

		public byte Find(string name)
		{
			if (!byName.TryGetValue(name, out var id))
				throw new YamlException($"CityEconomy: unknown resource '{name}'.");

			return id;
		}

		public bool TryFind(string name, out byte id) { return byName.TryGetValue(name, out id); }

		public string NameOf(int id) { return id >= 1 && id <= ResourceCount ? Resources[id].Key : null; }
	}
}
