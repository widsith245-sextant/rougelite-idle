using System;
using System.Collections.Generic;
using Godot;
using RougeliteIdle.Combat.Effects.Handlers;
using RougeliteIdle.Core;

namespace RougeliteIdle.Combat.Effects;

public static class CombatEffectRegistry
{
	private static readonly Dictionary<string, EffectHandler> Handlers =
		new(StringComparer.OrdinalIgnoreCase);

	private static EventBus? _eventBus;

	static CombatEffectRegistry()
	{
		RegisterAll();
	}

	public static void BindEventBus(EventBus eventBus) => _eventBus = eventBus;

	public static void RegisterAll()
	{
		Handlers.Clear();
		void Register(string id, EffectHandler handler) => Handlers[id] = handler;
		DirectModifierHandlers.Register(Register);
		MarkEffectHandlers.Register(Register);
		ControlTacticHandlers.Register(Register);
	}

	public static void Register(string effectId, EffectHandler handler) => Handlers[effectId] = handler;

	public static void TryApply(string effectId, CombatUnitData source, CombatUnitData? target, int pile = 1, float intensity = 0f) =>
		TryApplyInternal(effectId, source, target, pile, intensity);

	internal static void TryApplyInternal(
		string effectId,
		CombatUnitData source,
		CombatUnitData? target,
		int pile = 1,
		float intensity = 0f)
	{
		if (string.IsNullOrEmpty(effectId) || target == null)
		{
			return;
		}

		var def = CombatEffectLoader.Get(effectId);
		if (def == null)
		{
			GD.Print($"[CombatEffect] unknown effectId={effectId}");
			return;
		}

		AddOrRefreshInstance(target, effectId, source.Id, pile, intensity, def);
	}

	public static void ApplyInstance(
		CombatUnitData owner,
		CombatHitContext context,
		ActiveCombatEffect instance,
		EffectTriggerKind trigger,
		EffectHandlerServices? services = null)
	{
		services ??= new EffectHandlerServices();
		if (Handlers.TryGetValue(instance.EffectId, out var handler))
		{
			handler(owner, context, instance, trigger, services);
			PruneEmptyInstance(owner, instance);
			return;
		}

		GD.Print($"[CombatEffect] no handler for {instance.EffectId} trigger={trigger}");
	}

	public static void RemoveEffect(CombatUnitData target, string effectId)
	{
		for (var i = target.ActiveEffects.Count - 1; i >= 0; i--)
		{
			if (target.ActiveEffects[i].EffectId == effectId)
			{
				target.ActiveEffects.RemoveAt(i);
			}
		}
	}

	public static void ClearEffects(CombatUnitData target) => target.ActiveEffects.Clear();

	private static void AddOrRefreshInstance(
		CombatUnitData target,
		string effectId,
		string sourceId,
		int pile,
		float intensity,
		CombatEffectDefinition def)
	{
		ActiveCombatEffect? existing = null;
		foreach (var effect in target.ActiveEffects)
		{
			if (effect.EffectId == effectId)
			{
				existing = effect;
				break;
			}
		}

		if (existing != null)
		{
			existing.Pile = Math.Min(existing.Pile + pile, def.MaxPile > 0 ? def.MaxPile : int.MaxValue);
			existing.Intensity = intensity > 0f ? intensity : existing.Intensity;
			existing.RemainingDuration = def.Duration > 0f ? def.Duration : existing.RemainingDuration;
			var shieldAdd = ComputeInitialShield(effectId, def, pile);
			if (shieldAdd > 0f)
			{
				existing.Shield = Math.Max(existing.Shield, shieldAdd);
			}

			EmitApplied(target, existing, def);
			return;
		}

		var instance = new ActiveCombatEffect
		{
			EffectId = effectId,
			SourceId = sourceId,
			Pile = pile,
			Intensity = intensity,
			Shield = ComputeInitialShield(effectId, def, pile),
			Priority = def.Priority,
			RemainingDuration = def.Duration,
			TickTimer = def.TickInterval > 0f ? def.TickInterval : 2f,
			Stacks = pile,
			SourceAttackSnapshot = 0f,
		};

		target.ActiveEffects.Add(instance);
		EmitApplied(target, instance, def);
	}

	private static float ComputeInitialShield(string effectId, CombatEffectDefinition def, int pile)
	{
		if (effectId is "SHIELD" or "SHIELD_WITH_RETALIATION")
		{
			return def.ShieldBase > 0f ? def.ShieldBase + pile : Math.Max(10f, pile * 10f);
		}

		return def.ShieldBase > 0f ? def.ShieldBase + pile : 0f;
	}

	private static void EmitApplied(CombatUnitData target, ActiveCombatEffect instance, CombatEffectDefinition def)
	{
		_eventBus?.EmitCombatEffectApplied(
			target.Id,
			instance.EffectId,
			string.IsNullOrEmpty(def.NameCn) ? def.DisplayName : def.NameCn,
			def.Category,
			instance.Pile,
			instance.Intensity);
	}

	private static void PruneEmptyInstance(CombatUnitData owner, ActiveCombatEffect instance)
	{
		if (instance.Pile > 0 || instance.Shield > 0f || instance.RemainingDuration > 0f)
		{
			return;
		}

		owner.ActiveEffects.Remove(instance);
	}
}
