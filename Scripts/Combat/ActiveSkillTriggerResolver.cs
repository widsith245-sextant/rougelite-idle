using System.Collections.Generic;
using RougeliteIdle.Combat.Effects;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public static class ActiveSkillTriggerResolver
{
	public static List<PendingDamage> ResolvePositionTriggers(
		CombatUnitData owner,
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData? primaryEnemy,
		IReadOnlyList<PositionChangeEvent> positionEvents,
		float metaPercent)
	{
		var results = new List<PendingDamage>();
		if (primaryEnemy == null || primaryEnemy.CurrentHp <= 0f)
		{
			return results;
		}

		var hasMove = false;
		foreach (var change in positionEvents)
		{
			if (change.EntityId == owner.Id && System.Math.Abs(change.Delta) >= StatRegistry.PositionMoveThreshold)
			{
				hasMove = true;
				break;
			}
		}

		if (!hasMove)
		{
			return results;
		}

		foreach (var skill in owner.ActiveSkills)
		{
			if (skill.TriggerKind != SkillTriggerKind.OnXMove)
			{
				continue;
			}

			results.Add(CreateDamage(owner, primaryEnemy, skill.SkillMultiplier, metaPercent));
			CombatEffectRegistry.TryApply(skill.EffectId, owner, primaryEnemy);
		}

		return results;
	}

	public static List<PendingDamage> ResolveForceSwapTriggers(
		CombatUnitData owner,
		CombatUnitData? primaryEnemy,
		float metaPercent)
	{
		var results = new List<PendingDamage>();
		if (primaryEnemy == null || primaryEnemy.CurrentHp <= 0f)
		{
			return results;
		}

		foreach (var skill in owner.ActiveSkills)
		{
			if (skill.TriggerKind != SkillTriggerKind.OnForceSwap)
			{
				continue;
			}

			results.Add(CreateDamage(owner, primaryEnemy, skill.SkillMultiplier, metaPercent));
			CombatEffectRegistry.TryApply(skill.EffectId, owner, primaryEnemy);
		}

		return results;
	}

	public static List<PendingDamage> ResolveSquadSwapTriggers(
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData? primaryEnemy,
		float metaPercent)
	{
		var results = new List<PendingDamage>();
		if (primaryEnemy == null || primaryEnemy.CurrentHp <= 0f)
		{
			return results;
		}

		foreach (var ally in allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			foreach (var passive in ally.Passives)
			{
				if (passive.TriggerType != PassiveTriggerType.OnSquadSwap)
				{
					continue;
				}

				results.Add(CreateDamage(ally, primaryEnemy, passive.SkillMultiplier, metaPercent));
			}
		}

		return results;
	}

	private static PendingDamage CreateDamage(
		CombatUnitData source,
		CombatUnitData target,
		float skillMultiplier,
		float metaPercent)
	{
		var amount = DamageFormula.CalculateFromStats(
			source.Stats,
			metaPercent,
			0f,
			skillMultiplier,
			null,
			out _);
		return new PendingDamage(source, target, amount);
	}
}
