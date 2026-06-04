using System.Collections.Generic;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Combat.Effects;
using RougeliteIdle.Meta;

namespace RougeliteIdle.Combat;

public class CombatActionExecutor
{
	private const float BasicAttackMultiplier = 0.35f;

	private readonly EventBus _eventBus;
	private readonly CombatEffectRunner _effects;
	private readonly CombatSettlementPipeline _settlement;
	private readonly EffectTriggerBus _triggerBus;
	private readonly RandomNumberGenerator _rng = new();
	private CombatManager? _combat;

	public CombatActionExecutor(EventBus eventBus, EffectTriggerBus triggerBus, CombatManager? combat = null)
	{
		_eventBus = eventBus;
		_triggerBus = triggerBus;
		_combat = combat;
		_effects = new CombatEffectRunner(eventBus);
		_settlement = new CombatSettlementPipeline(eventBus);
		_rng.Randomize();
	}

	public void BindCombat(CombatManager combat)
	{
		_combat = combat;
		ConfigureServices();
	}

	public CombatEffectRunner Effects => _effects;

	private EffectHandlerServices BuildServices() => new()
	{
		Battlefield = _combat?.Battlefield,
		Allies = _combat?.Allies,
		Enemies = _combat?.Enemies,
		Executor = this,
		MetaPercent = _combat?.GetMetaPercent() ?? 0f,
	};

	private void ConfigureServices() => _triggerBus.Configure(BuildServices());

	public bool ExecuteTurn(
		CombatUnitData actor,
		BattlefieldController battlefield,
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData? enemy,
		float metaPercent,
		CombatSkillDefinition? overrideSkill = null,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		ConfigureServices();
		if (actor.StunNextAction)
		{
			actor.StunNextAction = false;
			actor.CombatState = UnitCombatState.Stunned;
			return false;
		}

		var skill = overrideSkill
			?? ClassSkillsLoader.GetGaugeSkill(actor.ActiveSkills)
			?? actor.ActiveSkill;
		var moveTags = skill?.MoveTags ?? new List<MoveTag>();
		var allPositionEvents = new List<PositionChangeEvent>();
		var target = actor.IsAlly ? enemy : battlefield.GetFrontAlly(allies);
		if (target == null || target.CurrentHp <= 0f)
		{
			return false;
		}

		if (!battlefield.IsInAttackRange(actor, target))
		{
			var oldX = actor.PositionX;
			if (actor.IsAlly)
			{
				battlefield.MoveTowardTarget(
					actor,
					target,
					actor.Stats.GetFinal(Stats.StatId.MoveSpeed) * 0.2f,
					obstacles);
			}
			else
			{
				battlefield.MoveTowardTarget(actor, target, 18f, obstacles);
			}

			if (System.Math.Abs(oldX - actor.PositionX) >= 0.01f)
			{
				_eventBus.EmitPositionChanged(actor.Id, oldX, actor.PositionX);
			}

			actor.CombatState = UnitCombatState.Moving;
			return false;
		}

		actor.CombatState = UnitCombatState.Casting;
		actor.ActiveSkill = skill;
		var forceSwapFired = false;
		foreach (var tag in moveTags)
		{
			if (tag.Kind == MoveTagKind.ForceSwap)
			{
				var swapEvents = battlefield.ApplyForceSwap(actor, allies);
				allPositionEvents.AddRange(swapEvents);
				forceSwapFired = swapEvents.Count > 0;
			}
			else
			{
				allPositionEvents.AddRange(battlefield.ApplyWorldMove(actor, tag, target, allies, obstacles));
			}
		}

		if (allPositionEvents.Count > 0)
		{
			actor.CombatState = UnitCombatState.Reposition;
		}

		EmitPositionChanges(allPositionEvents);
		if (allPositionEvents.Count > 0)
		{
			ProcessMoveTriggers(actor, allPositionEvents);
		}

		if (forceSwapFired)
		{
			var swapCtx = CombatHitContext.Create(actor, target, 0f, false, actor.DamageType, skill?.Id ?? string.Empty);
			_triggerBus.Emit(EffectTriggerKind.OnForceSwap, actor, swapCtx);
		}

		if (actor.IsAlly)
		{
			ApplyPendingDamage(PassiveSkillResolver.Resolve(allies, target, allPositionEvents, battlefield, metaPercent));
			if (forceSwapFired)
			{
				ApplyPendingDamage(ActiveSkillTriggerResolver.ResolveForceSwapTriggers(actor, target, metaPercent));
			}

			SkillEffectApplier.ApplySkillEffects(skill, actor, target, allies, _combat?.Enemies ?? new List<CombatUnitData>(), battlefield);

			var multiplier = (skill?.SkillMultiplier ?? 1f) * (1f + actor.TempActionPowerBonus);
			var activeDamage = DamageFormula.CalculateFromStats(
				actor.Stats,
				metaPercent,
				0f,
				multiplier,
				_rng,
				out var isCrit,
				actor.DamageType);
			ApplyDamage(actor, target, activeDamage, isCrit, skill?.Id ?? string.Empty, multiplier);
			_effects.TryApplyOnHitEffects(actor, target, actor.OnHitEffects);
			if (!string.IsNullOrEmpty(skill?.EffectId))
			{
				CombatEffectRegistry.TryApply(skill.EffectId, actor, target);
			}

			if (skill != null)
			{
				_eventBus.EmitCombatActionStarted($"{actor.Id}:{skill.TriggerSlot}");
			}
		}
		else
		{
			var multiplier = skill?.SkillMultiplier ?? 1f;
			var damage = DamageFormula.CalculateFromStats(
				actor.Stats,
				metaPercent,
				0f,
				multiplier,
				_rng,
				out var isCrit,
				actor.DamageType);
			ApplyDamage(actor, target, damage, isCrit, skill?.Id ?? string.Empty, multiplier);
			_effects.TryApplyOnHitEffects(actor, target, actor.OnHitEffects);
		}

		if (actor.ChargeAttackPending && target.CurrentHp > 0f)
		{
			actor.ChargeAttackPending = false;
			ExecuteBasicAttack(actor, target, allies, battlefield, metaPercent);
		}

		var actionCtx = CombatHitContext.Create(actor, target, 0f, false, actor.DamageType, skill?.Id ?? string.Empty);
		_triggerBus.Emit(EffectTriggerKind.OnAction, actor, actionCtx);
		actor.TempActionPowerBonus = 0f;
		actor.TempCritRateDelta = 0f;
		actor.CombatState = UnitCombatState.InRange;
		return true;
	}

	public bool ExecuteBasicAttack(
		CombatUnitData actor,
		CombatUnitData target,
		IReadOnlyList<CombatUnitData> allies,
		BattlefieldController battlefield,
		float metaPercent)
	{
		if (target.CurrentHp <= 0f || !battlefield.IsInAttackRange(actor, target))
		{
			return false;
		}

		var damage = DamageFormula.CalculateFromStats(
			actor.Stats,
			metaPercent,
			0f,
			BasicAttackMultiplier,
			_rng,
			out var isCrit,
			actor.DamageType);
		ApplyDamage(actor, target, damage, isCrit, "basic_attack", BasicAttackMultiplier);
		_eventBus.EmitCombatActionStarted($"{actor.Id}:basic_attack");
		return true;
	}

	public void ApplyPendingDamage(IEnumerable<PendingDamage> pending)
	{
		foreach (var hit in pending)
		{
			ApplyDamage(hit.Source, hit.Target, hit.Amount, false);
		}
	}

	public void ApplyDamage(
		CombatUnitData source,
		CombatUnitData target,
		float amount,
		bool isCrit,
		string skillId = "",
		float skillMultiplier = 1f,
		string displayTag = "")
	{
		if (amount <= 0f || target.CurrentHp <= 0f)
		{
			return;
		}

		var services = BuildServices();
		var context = CombatHitContext.Create(
			source,
			target,
			amount,
			isCrit,
			source.DamageType,
			skillId,
			skillMultiplier);
		context.DisplayTag = displayTag;
		_settlement.ApplyOutgoingModifiers(source, context, services);
		var defense = target.Stats.GetFinal(Stats.StatId.Defense);
		context.FinalAmount = DamageFormula.ApplyDefense(context.FinalAmount, defense);
		_settlement.ApplyIncomingModifiers(target, context, services);
		if (context.RetaliationDamage > 0f)
		{
			ApplyDamage(target, source, context.RetaliationDamage, false, "retaliate", 1f, "retaliate");
		}

		if (context.Cancelled || context.FinalAmount <= 0f)
		{
			return;
		}

		target.CurrentHp = System.Math.Max(0f, target.CurrentHp - context.FinalAmount);
		_eventBus.EmitUnitHpChanged(target.Id, target.CurrentHp, target.MaxHp);
		_settlement.EmitDamage(context);
	}

	public void ProcessMoveTriggers(CombatUnitData actor, IReadOnlyList<PositionChangeEvent> events)
	{
		foreach (var change in events)
		{
			if (change.EntityId != actor.Id || System.Math.Abs(change.Delta) < 0.01f)
			{
				continue;
			}

			ConfigureServices();
			_triggerBus.Emit(
				EffectTriggerKind.OnMoveEnd,
				actor,
				CombatHitContext.Create(actor, actor, 0f, false, actor.DamageType));
		}
	}

	private void EmitPositionChanges(IReadOnlyList<PositionChangeEvent> events)
	{
		foreach (var change in events)
		{
			_eventBus.EmitPositionChanged(change.EntityId, change.OldX, change.NewX);
		}
	}
}
