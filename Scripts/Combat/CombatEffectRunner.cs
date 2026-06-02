using System.Collections.Generic;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public sealed class CombatEffectRunner
{
	private readonly EventBus _eventBus;
	private readonly RandomNumberGenerator _rng = new();

	public CombatEffectRunner(EventBus eventBus)
	{
		_eventBus = eventBus;
		_rng.Randomize();
	}

	public void Tick(float dt, IReadOnlyList<CombatUnitData> units, CombatActionExecutor executor)
	{
		foreach (var unit in units)
		{
			if (unit.CurrentHp <= 0f || unit.ActiveEffects.Count == 0)
			{
				continue;
			}

			for (var i = unit.ActiveEffects.Count - 1; i >= 0; i--)
			{
				var effect = unit.ActiveEffects[i];
				effect.RemainingDuration -= dt;
				var def = CombatEffectLoader.Get(effect.EffectId);
				if (def == null)
				{
					unit.ActiveEffects.RemoveAt(i);
					continue;
				}

				if (def.TickInterval > 0f && def.DamagePercentOfSource > 0f)
				{
					effect.TickTimer -= dt;
					if (effect.TickTimer <= 0f)
					{
						effect.TickTimer = def.TickInterval;
						var tickDamage = effect.SourceAttackSnapshot * def.DamagePercentOfSource * effect.Stacks;
						if (tickDamage > 0f)
						{
							var source = new CombatUnitData { Id = effect.SourceId, IsAlly = !unit.IsAlly };
							executor.ApplyDamage(source, unit, tickDamage, false);
						}
					}
				}

				if (effect.RemainingDuration <= 0f)
				{
					unit.ActiveEffects.RemoveAt(i);
				}
			}
		}
	}

	public void TryApplyOnHitEffects(
		CombatUnitData source,
		CombatUnitData target,
		IReadOnlyList<OnHitEffectRoll> rolls)
	{
		if (rolls == null || rolls.Count == 0 || target.CurrentHp <= 0f)
		{
			return;
		}

		foreach (var roll in rolls)
		{
			if (_rng.Randf() > roll.Chance)
			{
				continue;
			}

			ApplyEffect(source, target, roll.Id);
		}
	}

	public void ApplyEffect(CombatUnitData source, CombatUnitData target, string effectId)
	{
		var def = CombatEffectLoader.Get(effectId);
		if (def == null)
		{
			return;
		}

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
			existing.Stacks = System.Math.Min(existing.Stacks + 1, def.MaxStacks);
			existing.RemainingDuration = def.Duration;
			return;
		}

		target.ActiveEffects.Add(new ActiveCombatEffect
		{
			EffectId = effectId,
			SourceId = source.Id,
			RemainingDuration = def.Duration,
			TickTimer = def.TickInterval,
			Stacks = 1,
			SourceAttackSnapshot = source.Stats.GetFinal(StatId.Damage),
		});

		if (def.StatModifier != null)
		{
			var statId = MapStat(def.StatModifier.Stat);
			if (statId != null)
			{
				target.Stats.AddFlat(statId.Value, def.StatModifier.Flat);
				target.Stats.AddIncreased(statId.Value, def.StatModifier.Percent);
				target.Stats.RecalculateDerived();
			}
		}
	}

	private static StatId? MapStat(string stat) => stat switch
	{
		"MoveSpeed" => StatId.MoveSpeed,
		"Defense" => StatId.Defense,
		"Damage" => StatId.Damage,
		_ => null,
	};
}
