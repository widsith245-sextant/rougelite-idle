using System.Collections.Generic;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

/// <summary>
/// Per-frame AI: scan, move toward range, hold in range for attacks/skills.
/// </summary>
public static class UnitCombatAI
{
	public static void Tick(
		CombatUnitData unit,
		CombatUnitData? target,
		BattlefieldController battlefield,
		float delta,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		if (unit.CurrentHp <= 0f || target == null || target.CurrentHp <= 0f)
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

		if (battlefield.IsInAttackRange(unit, target))
		{
			unit.CombatState = UnitCombatState.InRange;
			return;
		}

		battlefield.MoveTowardEnemy(unit, target, unit.Stats.GetFinal(StatId.MoveSpeed), delta, obstacles);
		if (unit.CombatState != UnitCombatState.Moving && unit.CombatState != UnitCombatState.InRange)
		{
			unit.CombatState = UnitCombatState.Moving;
		}
	}

	public static bool TickNormalAttack(
		CombatUnitData unit,
		CombatUnitData target,
		BattlefieldController battlefield,
		float delta,
		out bool isCrit)
	{
		isCrit = false;
		if (unit.IsBlockingOutput || unit.CombatState != UnitCombatState.InRange)
		{
			return false;
		}

		if (!battlefield.IsInAttackRange(unit, target))
		{
			return false;
		}

		var atkSpeed = unit.Stats.GetFinal(StatId.AtkSpeed) * CombatManager.GlobalNormalAttackSpeedMultiplier;
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
