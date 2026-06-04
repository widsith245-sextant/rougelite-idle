using System;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat.Effects.Handlers;

internal static class DirectModifierHandlers
{
	public static void Register(Action<string, EffectHandler> register)
	{
		register("ATK_FLAT_UP", AtkFlatUp);
		register("ATK_PERCENT_UP", AtkPercentUp);
		register("DEF_PERCENT_UP", DefPercentUp);
		register("ATK_FLAT_DOWN", AtkFlatDown);
		register("VULNERABILITY", Vulnerability);
		register("ATTACK_LEVEL_UP", AttackLevelUp);
		register("DEFENSE_LEVEL_UP", DefenseLevelUp);
		register("ATTACK_LEVEL_DOWN", AttackLevelDown);
		register("DEFENSE_LEVEL_DOWN", DefenseLevelDown);
		register("SPEED_UP", SpeedUp);
		register("SPEED_DOWN", SpeedDown);
		register("PARALYSIS", Paralysis);
		register("ACTION_POWER_UP", ActionPowerUp);
		register("ACTION_POWER_DOWN", ActionPowerDown);
		register("CRIT_RATE_UP", CritRateUp);
		register("CRIT_RATE_DOWN", CritRateDown);
		register("SHIELD", Shield);
		register("SHIELD_WITH_RETALIATION", ShieldWithRetaliation);
	}

	private static void AtkFlatUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Source))
		{
			return;
		}

		ctx.FlatBonus += inst.Pile;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void AtkPercentUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Source))
		{
			return;
		}

		ctx.PercentBonus += 0.1f * inst.Pile;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void DefPercentUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		ctx.PercentReduction += 0.1f * inst.Pile;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void AtkFlatDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Source))
		{
			return;
		}

		ctx.FlatBonus -= inst.Pile;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void Vulnerability(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		ctx.PercentBonus += 0.2f * inst.Pile;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void AttackLevelUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.TempActionPowerBonus += inst.Pile * 0.05f;
		}
		else if (trigger == EffectTriggerKind.OnAction)
		{
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void DefenseLevelUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.Stats.AddFlat(Stats.StatId.Defense, inst.Pile * 5f);
			owner.Stats.RecalculateDerived();
		}
		else if (trigger == EffectTriggerKind.OnDamaged)
		{
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void AttackLevelDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.TempActionPowerBonus -= inst.Pile * 0.05f;
		}
		else if (trigger == EffectTriggerKind.OnAction)
		{
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void DefenseLevelDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnDamaged)
		{
			owner.Stats.AddFlat(Stats.StatId.Defense, -inst.Pile * 5f);
			owner.Stats.RecalculateDerived();
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void SpeedUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger != EffectTriggerKind.OnTimeElapsed && trigger != EffectTriggerKind.PassiveAlways)
		{
			return;
		}

		owner.Stats.AddIncreased(Stats.StatId.MoveSpeed, inst.Pile * 0.02f);
		owner.Stats.RecalculateDerived();
		inst.TickTimer -= 2f;
		if (inst.TickTimer <= 0f)
		{
			inst.TickTimer = 2f;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void SpeedDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger != EffectTriggerKind.OnTimeElapsed && trigger != EffectTriggerKind.PassiveAlways)
		{
			return;
		}

		owner.Stats.AddIncreased(Stats.StatId.MoveSpeed, -inst.Pile * 0.02f);
		owner.Stats.RecalculateDerived();
		inst.TickTimer -= 2f;
		if (inst.TickTimer <= 0f)
		{
			inst.TickTimer = 2f;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void Paralysis(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger != EffectTriggerKind.OnGaugeFull || !ReferenceEquals(owner, ctx.Source))
		{
			return;
		}

		owner.TempActionPowerBonus = Math.Min(owner.TempActionPowerBonus, -0.5f * inst.Pile);
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void ActionPowerUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.TempActionPowerBonus += 0.1f * inst.Pile;
		}
		else if (trigger == EffectTriggerKind.OnAction)
		{
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void ActionPowerDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnGaugeFull)
		{
			owner.TempActionPowerBonus -= 0.1f * inst.Pile;
		}
		else if (trigger == EffectTriggerKind.OnAction)
		{
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void CritRateUp(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnAction)
		{
			owner.TempCritRateDelta += 0.05f * inst.Pile;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void CritRateDown(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger == EffectTriggerKind.OnAction)
		{
			owner.TempCritRateDelta -= 0.05f * inst.Pile;
			inst.Pile = Math.Max(0, inst.Pile - 1);
		}
	}

	private static void Shield(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		var absorb = Math.Min(inst.Shield, ctx.FinalAmount);
		ctx.FinalAmount -= absorb;
		inst.Shield -= absorb;
		ctx.DisplayTag = absorb > 0f ? "shield" : ctx.DisplayTag;
	}

	private static void ShieldWithRetaliation(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind _, EffectHandlerServices __)
	{
		if (!ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		var absorb = Math.Min(inst.Shield, ctx.FinalAmount);
		ctx.FinalAmount -= absorb;
		inst.Shield -= absorb;
		ctx.DisplayTag = absorb > 0f ? "shield" : ctx.DisplayTag;
		if (inst.Shield <= 0f && inst.Intensity > 0f)
		{
			ctx.RetaliationDamage += inst.Intensity;
			ctx.DisplayTag = "retaliate";
		}
	}
}
