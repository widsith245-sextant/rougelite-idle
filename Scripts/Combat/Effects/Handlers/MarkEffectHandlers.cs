using System;
using System.Collections.Generic;

namespace RougeliteIdle.Combat.Effects.Handlers;

internal static class MarkEffectHandlers
{
	private const int StackBurstThreshold = 5;

	public static void Register(Action<string, EffectHandler> register)
	{
		register("MARK_TAKEDAMAGE", MarkTakeDamage);
		register("MARK_TIMER", MarkTimer);
		register("MARK_STACK_BURST", MarkStackBurst);
		register("MARK_SPREAD", MarkSpread);
		register("RESOURCE_STACK", ResourceStack);
	}

	private static void MarkTakeDamage(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger != EffectTriggerKind.OnDamaged || !ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		ctx.FinalAmount += inst.Intensity > 0f ? inst.Intensity : inst.Pile;
		ctx.DisplayTag = "mark";
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void MarkTimer(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices services)
	{
		if (trigger != EffectTriggerKind.OnTimeElapsed)
		{
			return;
		}

		inst.TickTimer -= 2f;
		if (inst.TickTimer > 0f)
		{
			return;
		}

		inst.TickTimer = 2f;
		if (services.Executor == null)
		{
			owner.CurrentHp = Math.Max(0f, owner.CurrentHp - inst.Intensity);
			inst.Pile = Math.Max(0, inst.Pile - 1);
			return;
		}

		var dummySource = new CombatUnitData { Id = inst.SourceId, IsAlly = !owner.IsAlly };
		services.Executor.ApplyDamage(dummySource, owner, inst.Intensity, false, string.Empty, 1f, "mark");
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}

	private static void MarkStackBurst(CombatUnitData owner, ActiveCombatEffect inst, EffectHandlerServices services)
	{
		if (inst.Pile < StackBurstThreshold)
		{
			return;
		}

		var burst = inst.Intensity * inst.Pile;
		if (services.Executor != null)
		{
			var dummySource = new CombatUnitData { Id = inst.SourceId, IsAlly = !owner.IsAlly };
			services.Executor.ApplyDamage(dummySource, owner, burst, false, "MARK_STACK_BURST", 1f, "mark");
		}
		else
		{
			owner.CurrentHp = Math.Max(0f, owner.CurrentHp - burst);
		}

		inst.Pile = 1;
	}

	private static void MarkStackBurst(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices services)
	{
		if (trigger == EffectTriggerKind.OnPileThreshold || trigger == EffectTriggerKind.OnDamaged)
		{
			MarkStackBurst(owner, inst, services);
		}
	}

	private static void MarkSpread(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices services)
	{
		if (trigger != EffectTriggerKind.OnDamaged || !ReferenceEquals(owner, ctx.Target))
		{
			return;
		}

		if (services.Battlefield == null || services.Enemies == null)
		{
			return;
		}

		var adjacent = services.Battlefield.GetAdjacentUnits(owner, services.Enemies);
		var spreadIntensity = inst.Intensity > 0f ? inst.Intensity * 0.5f : 1f;
		foreach (var enemy in adjacent)
		{
			if (enemy.Id == owner.Id)
			{
				continue;
			}

			CombatEffectRegistry.TryApplyInternal("MARK_TAKEDAMAGE", ctx.Source, enemy, 1, spreadIntensity);
		}
	}

	private static void ResourceStack(CombatUnitData owner, CombatHitContext ctx, ActiveCombatEffect inst, EffectTriggerKind trigger, EffectHandlerServices __)
	{
		if (trigger != EffectTriggerKind.OnTimeElapsed && trigger != EffectTriggerKind.PassiveAlways)
		{
			return;
		}

		inst.TickTimer -= 2f;
		if (inst.TickTimer > 0f)
		{
			return;
		}

		inst.TickTimer = 2f;
		inst.Pile = Math.Max(0, inst.Pile - 1);
	}
}
