using System;
using System.Collections.Generic;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public static class PassiveSkillResolver
{
	public static List<PendingDamage> Resolve(
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData? primaryEnemy,
		IReadOnlyList<PositionChangeEvent> positionEvents,
		BattlefieldController battlefield,
		float metaPercent = 0f)
	{
		var results = new List<PendingDamage>();
		if (primaryEnemy == null || primaryEnemy.CurrentHp <= 0f)
		{
			return results;
		}

		ResolveReachFrontPassives(allies, primaryEnemy, positionEvents, results, metaPercent);
		ResolveAllyMovedPassives(allies, primaryEnemy, positionEvents, results, metaPercent);
		ResolvePointBlankPassives(allies, primaryEnemy, battlefield, results, metaPercent);
		return results;
	}

	private static void ResolveReachFrontPassives(
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData enemy,
		IReadOnlyList<PositionChangeEvent> positionEvents,
		List<PendingDamage> results,
		float metaPercent)
	{
		foreach (var change in positionEvents)
		{
			var owner = FindUnit(allies, change.EntityId);
			if (owner == null)
			{
				continue;
			}

			var reachedFront = change.NewX <= StatRegistry.FrontLineThresholdX
				&& change.OldX > StatRegistry.FrontLineThresholdX;

			foreach (var passive in owner.Passives)
			{
				if (passive.TriggerType is not (PassiveTriggerType.OnSelfReachSlot
					or PassiveTriggerType.OnSelfReachFront))
				{
					continue;
				}

				if (!reachedFront)
				{
					continue;
				}

				results.Add(CreateDamage(owner, enemy, passive.SkillMultiplier, metaPercent));
			}
		}
	}

	private static void ResolveAllyMovedPassives(
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData enemy,
		IReadOnlyList<PositionChangeEvent> positionEvents,
		List<PendingDamage> results,
		float metaPercent)
	{
		if (positionEvents.Count == 0)
		{
			return;
		}

		foreach (var ally in allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			foreach (var passive in ally.Passives)
			{
				if (passive.TriggerType != PassiveTriggerType.OnAnyAllyMoved)
				{
					continue;
				}

				var triggeredByTeammate = false;
				foreach (var change in positionEvents)
				{
					if (change.EntityId == ally.Id)
					{
						continue;
					}

					if (Math.Abs(change.Delta) >= StatRegistry.PositionMoveThreshold)
					{
						triggeredByTeammate = true;
						break;
					}
				}

				if (!triggeredByTeammate)
				{
					continue;
				}

				results.Add(CreateDamage(ally, enemy, passive.SkillMultiplier, metaPercent));
			}
		}
	}

	private static void ResolvePointBlankPassives(
		IReadOnlyList<CombatUnitData> allies,
		CombatUnitData enemy,
		BattlefieldController battlefield,
		List<PendingDamage> results,
		float metaPercent)
	{
		foreach (var ally in allies)
		{
			foreach (var passive in ally.Passives)
			{
				if (passive.TriggerType != PassiveTriggerType.OnPointBlank)
				{
					continue;
				}

				var dist = battlefield.GetDistanceToEnemy(ally, enemy);
				if (dist > passive.TargetSlot)
				{
					continue;
				}

				results.Add(CreateDamage(ally, enemy, passive.SkillMultiplier, metaPercent));
			}
		}
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

	private static CombatUnitData? FindUnit(IReadOnlyList<CombatUnitData> allies, string entityId)
	{
		foreach (var ally in allies)
		{
			if (ally.Id == entityId)
			{
				return ally;
			}
		}

		return null;
	}
}
