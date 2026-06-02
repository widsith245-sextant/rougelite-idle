using System;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

/// <summary>
/// Per-frame AI: scan, move toward range, hold in range for attacks/skills.
/// </summary>
public static class UnitCombatAI
{
	public static void Tick(
		CombatUnitData unit,
		CombatUnitData? enemy,
		BattlefieldController battlefield,
		float delta)
	{
		if (unit.CurrentHp <= 0f || enemy == null || enemy.CurrentHp <= 0f)
		{
			return;
		}

		if (unit.StunTimer > 0f)
		{
			unit.StunTimer -= delta;
			unit.CombatState = UnitCombatState.Stunned;
			return;
		}

		if (unit.CombatState is UnitCombatState.Stunned or UnitCombatState.Reposition)
		{
			return;
		}

		if (battlefield.IsInAttackRange(unit, enemy))
		{
			unit.CombatState = UnitCombatState.InRange;
			return;
		}

		battlefield.MoveTowardEnemy(unit, enemy, unit.Stats.GetFinal(StatId.MoveSpeed), delta);
		if (unit.CombatState != UnitCombatState.Moving)
		{
			unit.CombatState = UnitCombatState.Moving;
		}
	}

	public static bool TickNormalAttack(
		CombatUnitData unit,
		CombatUnitData enemy,
		BattlefieldController battlefield,
		float delta,
		out bool isCrit)
	{
		isCrit = false;
		if (unit.IsBlockingOutput || unit.CombatState != UnitCombatState.InRange)
		{
			return false;
		}

		if (!battlefield.IsInAttackRange(unit, enemy))
		{
			return false;
		}

		var atkSpeed = unit.Stats.GetFinal(StatId.AtkSpeed);
		unit.NormalAttackTimer += delta * atkSpeed;
		if (unit.NormalAttackTimer < 1f)
		{
			return false;
		}

		unit.NormalAttackTimer = 0f;
		unit.CombatState = UnitCombatState.Attacking;
		return true;
	}
}
