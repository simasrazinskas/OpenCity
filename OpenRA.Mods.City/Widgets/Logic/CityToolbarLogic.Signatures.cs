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
using System.Globalization;
using System.Text;
using OpenRA.Mods.City.Traits;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.City.Widgets.Logic
{
	/// <summary>Signature buildings in the palette: their tooltip lists the unlock rules (have / need) and the bonuses (ISignatureUnlocks.GetStatus).</summary>
	public partial class CityToolbarLogic
	{
		[FluentReference("progress")]
		const string SignatureLocked = "label-signature-locked";

		[FluentReference]
		const string SignatureReady = "label-signature-ready";

		[FluentReference]
		const string SignatureBuilt = "label-signature-built";

		[FluentReference("rule", "have", "need")]
		const string SignatureRuleOpen = "label-signature-rule-open";

		[FluentReference("rule", "have", "need")]
		const string SignatureRuleMet = "label-signature-rule-met";

		[FluentReference("value", "radius")]
		const string SignatureWellBeing = "label-signature-wellbeing";

		[FluentReference("value")]
		const string SignatureAttractiveness = "label-signature-attractiveness";

		[FluentReference("value")]
		const string SignatureEfficiency = "label-signature-efficiency";

		[FluentReference("value")]
		const string SignaturePollution = "label-signature-pollution";

		bool IsSignature(string actorName)
		{
			return actorName != null && world.Map.Rules.Actors.TryGetValue(actorName, out var actor) && actor.HasTraitInfo<SignatureBuildingInfo>();
		}

		string SignatureTooltip(string actorName)
		{
			var status = ctx.Get<ISignatureUnlocks>()?.GetStatus(actorName);
			if (status == null)
				return "";

			var text = new StringBuilder();
			if (status.Built)
				text.Append(FluentProvider.GetMessage(SignatureBuilt));
			else if (status.Unlocked)
				text.Append(FluentProvider.GetMessage(SignatureReady));
			else
				text.Append(FluentProvider.GetMessage(SignatureLocked, "progress", status.Progress));

			if (!status.Built && !status.Unlocked && status.Rules != null)
			{
				foreach (var rule in status.Rules)
				{
					text.Append('\n').Append(FluentProvider.GetMessage(rule.Met ? SignatureRuleMet : SignatureRuleOpen,
						"rule", RuleName(rule),
						"have", rule.Have.ToString("N0", CultureInfo.CurrentCulture),
						"need", rule.Need.ToString("N0", CultureInfo.CurrentCulture)));
				}
			}

			if (status.WellBeing > 0)
				text.Append('\n').Append(FluentProvider.GetMessage(SignatureWellBeing, "value", status.WellBeing, "radius", status.WellBeingRadius));

			if (status.Attractiveness > 0)
				text.Append('\n').Append(FluentProvider.GetMessage(SignatureAttractiveness, "value", status.Attractiveness));

			if (status.EfficiencyPercent > 0)
				text.Append('\n').Append(FluentProvider.GetMessage(SignatureEfficiency, "value", status.EfficiencyPercent));

			if (status.PollutionReductionPercent > 0)
				text.Append('\n').Append(FluentProvider.GetMessage(SignaturePollution, "value", status.PollutionReductionPercent));

			return text.ToString();
		}

		string RuleName(SignatureRuleStatus rule)
		{
			var kind = (rule.Kind ?? "").ToLowerInvariant();
			var target = rule.Target ?? "";
			if (Enum.TryParse<ZoneType>(target, out var zone))
				target = CityUi.ZoneName(zone);
			else if (target.Length > 0 && world.Map.Rules.Actors.TryGetValue(target, out var actor) && actor.TraitInfoOrDefault<TooltipInfo>() is { } tooltip)
				target = FluentProvider.GetMessage(tooltip.Name);

			var key = "label-signature-rule-" + kind;
			return FluentProvider.TryGetMessage(key, out _) ? FluentProvider.GetMessage(key, "target", target) : CityUi.Prettify(rule.Kind ?? "");
		}
	}
}
