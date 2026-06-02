using System.Collections.Generic;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Meta;

namespace RougeliteIdle.Combat;

public class CombatActionExecutor
{
	private readonly EventBus _eventBus;
	private readonly CombatEffectRunner _effects;
	private readonly RandomNumberGenerator _rng = new();

	public CombatActionExecutor(EventBus eventBus)
	{
		_eventBus = eventBus;
		_effects = new CombatEffectRunner(eventBus);
		_rng.Randomize();
	}

	public CombatEffectRunner Effects => _effects;

	public bool ExecuteTurn(
		CombatUnitData actor,
		BattlefieldController battlefield,
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData? enemy,
		float metaPercent)
	{
		var skill = actor.ActiveSkill;
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
				battlefield.MoveTowardTarget(actor, target, actor.Stats.GetFinal(Stats.StatId.MoveSpeed) * 0.2f);
			}
			else
			{
				battlefield.MoveTowardTarget(actor, target, 18f);
			}

			if (System.Math.Abs(oldX - actor.PositionX) >= 0.01f)
			{
				_eventBus.EmitPositionChanged(actor.Id, oldX, actor.PositionX);
			}

			actor.CombatState = UnitCombatState.Moving;
			return false;
		}

		actor.CombatState = UnitCombatState.Casting;
		foreach (var tag in moveTags)
		{
			allPositionEvents.AddRange(battlefield.ApplyWorldMove(actor, tag, target));
		}

		if (allPositionEvents.Count > 0)
		{
			actor.CombatState = UnitCombatState.Reposition;
		}

		EmitPositionChanges(allPositionEvents);

		if (actor.IsAlly)
		{
			var passiveDamage = PassiveSkillResolver.Resolve(allies, target, allPositionEvents, battlefield, metaPercent);
			foreach (var pending in passiveDamage)
			{
				ApplyDamage(pending.Source, pending.Target, pending.Amount, false);
			}

			var multiplier = skill?.SkillMultiplier ?? 1f;
			var activeDamage = DamageFormula.CalculateFromStats(
				actor.Stats,
				metaPercent,
				0f,
				multiplier,
				_rng,
				out var isCrit,
				actor.DamageType);
			ApplyDamage(actor, target, activeDamage, isCrit);
			_effects.TryApplyOnHitEffects(actor, target, actor.OnHitEffects);
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
			ApplyDamage(actor, target, damage, isCrit);
			_effects.TryApplyOnHitEffects(actor, target, actor.OnHitEffects);
		}

		actor.CombatState = UnitCombatState.InRange;
		return true;
	}

	public void ApplyDamage(CombatUnitData source, CombatUnitData target, float amount, bool isCrit)
	{
		if (amount <= 0f || target.CurrentHp <= 0f)
		{
			return;
		}

		var defense = target.Stats.GetFinal(Stats.StatId.Defense);
		amount = DamageFormula.ApplyDefense(amount, defense);

		target.CurrentHp = System.Math.Max(0f, target.CurrentHp - amount);
		_eventBus.EmitUnitHpChanged(target.Id, target.CurrentHp, target.MaxHp);
		_eventBus.EmitDamageDealt(source.Id, target.Id, amount, isCrit);
	}

	private void EmitPositionChanges(IReadOnlyList<PositionChangeEvent> events)
	{
		foreach (var change in events)
		{
			_eventBus.EmitPositionChanged(change.EntityId, change.OldX, change.NewX);
		}
	}
}
