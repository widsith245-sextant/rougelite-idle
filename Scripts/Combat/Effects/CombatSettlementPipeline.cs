using System;
using System.Collections.Generic;
using System.Linq;
using RougeliteIdle.Core;

namespace RougeliteIdle.Combat.Effects;

public sealed class CombatSettlementPipeline
{
	private readonly EventBus _eventBus;

	public CombatSettlementPipeline(EventBus eventBus)
	{
		_eventBus = eventBus;
	}

	public void ApplyOutgoingModifiers(CombatUnitData source, CombatHitContext context, EffectHandlerServices? services = null)
	{
		RunTriggerEffects(source, context, EffectTriggerKind.OnDealDamage, services);
		context.FinalAmount = Math.Max(0f, (context.RawAmount + context.FlatBonus) * (1f + context.PercentBonus));
	}

	public void ApplyIncomingModifiers(CombatUnitData target, CombatHitContext context, EffectHandlerServices? services = null)
	{
		RunTriggerEffects(target, context, EffectTriggerKind.OnDamaged, services);
		if (context.PercentReduction > 0f)
		{
			context.FinalAmount *= Math.Max(0.05f, 1f - context.PercentReduction);
		}
	}

	public void EmitDamage(CombatHitContext context)
	{
		if (context.Cancelled || context.FinalAmount <= 0f)
		{
			return;
		}

		_eventBus.EmitDamageDealt(
			context.Source.Id,
			context.Target.Id,
			context.FinalAmount,
			context.IsCrit,
			context.DamageType,
			context.DisplayTag);
	}

	private static void RunTriggerEffects(
		CombatUnitData owner,
		CombatHitContext context,
		EffectTriggerKind trigger,
		EffectHandlerServices? services)
	{
		var instances = owner.ActiveEffects
			.Where(e => MatchesTrigger(e, trigger))
			.OrderByDescending(e => e.Priority)
			.ToList();

		foreach (var instance in instances)
		{
			CombatEffectRegistry.ApplyInstance(owner, context, instance, trigger, services);
		}
	}

	private static bool MatchesTrigger(ActiveCombatEffect effect, EffectTriggerKind trigger)
	{
		var def = CombatEffectLoader.Get(effect.EffectId);
		if (def?.TriggerConditions == null || def.TriggerConditions.Count == 0)
		{
			return false;
		}

		var triggerName = trigger.ToString();
		foreach (var condition in def.TriggerConditions)
		{
			if (string.Equals(condition, triggerName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
