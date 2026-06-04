using System;
using System.Collections.Generic;

namespace RougeliteIdle.Combat.Effects.Handlers;

internal static class ControlTacticHandlers
{
	public static void Register(Action<string, EffectHandler> register)
	{
		register("STUN_NEXT_ACTION", StunNextAction);
		register("CHARGE_ATTACK", ChargeAttack);
		register("BACKSTAB", Backstab);
		register("SWAP_CURSE", SwapCurse);
		register("ECHO", Echo);
		register("RECURSION", Recursion);
	}

	private static void StunNextAction(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.StunNextAction = true;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void ChargeAttack(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnMoveEnd)
		{
			owner.ChargeAttackPending = true;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void Backstab(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices services)
	{
		if (trigger != EffectTriggerKind.OnDealDamage || !ReferenceEquals(owner, ctx.Source))
		{
			return;
		}

		if (services.Battlefield == null || !services.Battlefield.IsBehindTarget(ctx.Source, ctx.Target))
		{
			return;
		}

		ctx.PercentBonus += 0.3f;
		CombatEffectRegistry.TryApplyInternal(
			"MARK_TAKEDAMAGE",
			ctx.Source,
			ctx.Target,
			1,
			inst.Intensity > 0f ? inst.Intensity : 2f);
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void SwapCurse(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices services)
	{
		if (trigger != EffectTriggerKind.OnForceSwap)
		{
			return;
		}

		if (services.Battlefield == null || services.Enemies == null)
		{
			return;
		}

		var adjacent = services.Battlefield.GetAdjacentUnits(owner, services.Enemies);
		foreach (var enemy in adjacent)
		{
			CombatEffectRegistry.TryApplyInternal("VULNERABILITY", owner, enemy, inst.Pile, 1f);
		}
	}

	private static void Echo(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnAction && !string.IsNullOrEmpty(ctx.SkillId))
		{
			owner.EchoRepeatSkillId = ctx.SkillId;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void Recursion(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnAction && !string.IsNullOrEmpty(ctx.SkillId))
		{
			owner.RecursionSkillId = ctx.SkillId;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}
}
