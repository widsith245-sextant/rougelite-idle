using System;
using System.Collections.Generic;
using System.Linq;
using RougeliteIdle.Core;

namespace RougeliteIdle.Combat.Effects;

public sealed class EffectTriggerBus
{
	private readonly CombatSettlementPipeline _pipeline;
	private EffectHandlerServices _services = new();

	public EffectTriggerBus(CombatSettlementPipeline pipeline)
	{
		_pipeline = pipeline;
	}

	public void Configure(EffectHandlerServices services) => _services = services;

	public void Emit(
		EffectTriggerKind kind,
		CombatUnitData owner,
		CombatHitContext? context = null)
	{
		context ??= CombatHitContext.Create(owner, owner, 0f, false, owner.DamageType);

		var instances = owner.ActiveEffects
			.Where(e => MatchesTrigger(e, kind))
			.OrderByDescending(e => e.Priority)
			.ToList();

		foreach (var instance in instances)
		{
			CombatEffectRegistry.ApplyInstance(owner, context, instance, kind, _services);
			if (instance.EffectId == "MARK_STACK_BURST" && instance.Pile >= 5)
			{
				CombatEffectRegistry.ApplyInstance(owner, context, instance, EffectTriggerKind.OnPileThreshold, _services);
			}
		}
	}

	public void EmitToAllUnits(IReadOnlyList<CombatUnitData> units, EffectTriggerKind kind, float dt = 0f)
	{
		foreach (var unit in units)
		{
			if (unit.CurrentHp <= 0f)
			{
				continue;
			}

			if (kind == EffectTriggerKind.OnTimeElapsed)
			{
				TickTimedEffects(unit, dt);
			}
			else
			{
				Emit(kind, unit);
			}
		}
	}

	private void TickTimedEffects(CombatUnitData unit, float dt)
	{
		for (var i = unit.ActiveEffects.Count - 1; i >= 0; i--)
		{
			var effect = unit.ActiveEffects[i];
			var def = CombatEffectLoader.Get(effect.EffectId);
			if (def == null)
			{
				unit.ActiveEffects.RemoveAt(i);
				continue;
			}

			if (def.TickInterval <= 0f && !HasTrigger(def, EffectTriggerKind.PassiveAlways))
			{
				continue;
			}

			effect.TickTimer -= dt;
			if (effect.TickTimer > 0f)
			{
				continue;
			}

			effect.TickTimer = def.TickInterval > 0f ? def.TickInterval : 2f;
			var ctx = CombatHitContext.Create(unit, unit, 0f, false, unit.DamageType);
			CombatEffectRegistry.ApplyInstance(unit, ctx, effect, EffectTriggerKind.OnTimeElapsed, _services);
		}

		Emit(EffectTriggerKind.PassiveAlways, unit);
	}

	private static bool MatchesTrigger(ActiveCombatEffect effect, EffectTriggerKind trigger)
	{
		var def = CombatEffectLoader.Get(effect.EffectId);
		return def != null && HasTrigger(def, trigger);
	}

	private static bool HasTrigger(CombatEffectDefinition def, EffectTriggerKind trigger)
	{
		if (def.TriggerConditions == null)
		{
			return false;
		}

		var name = trigger.ToString();
		foreach (var condition in def.TriggerConditions)
		{
			if (string.Equals(condition, name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
