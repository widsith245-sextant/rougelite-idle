using System;
using System.Collections.Generic;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

/// <summary>
/// X-axis battlefield: allies on the left advance toward enemy anchor on the right.
/// </summary>
public class BattlefieldController
{
	public const float EnemyAnchorX = 340f;
	public const float MinAllyX = 8f;
	public const float MaxAllyX = 320f;
	private const float DefaultAttackRange = 20f;
	private const float MaxChargeStrikeDistance = 20f;
	private const float ChargeStopOffset = 20f;

	private readonly Dictionary<string, CombatUnitData> _units = new();

	public void RegisterUnits(IReadOnlyList<CombatUnitData> allies, IReadOnlyList<CombatUnitData> enemies)
	{
		_units.Clear();
		foreach (var u in allies)
		{
			_units[u.Id] = u;
		}

		foreach (var u in enemies)
		{
			_units[u.Id] = u;
			if (u.PositionX <= 0f)
			{
				u.PositionX = EnemyAnchorX;
			}
		}
	}

	public float GetDistanceBetween(CombatUnitData from, CombatUnitData to) =>
		GetEdgeDistanceBetween(from, to);

	public float GetDistanceToEnemy(CombatUnitData ally, CombatUnitData enemy) =>
		GetDistanceBetween(ally, enemy);

	public bool IsInAttackRange(CombatUnitData attacker, CombatUnitData target)
	{
		return GetEdgeDistanceBetween(attacker, target) <= GetAttackRange(attacker);
	}

	public CombatUnitData? GetFrontAlly(IReadOnlyList<CombatUnitData> allies)
	{
		CombatUnitData? front = null;
		var minX = float.MaxValue;
		foreach (var ally in allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			if (ally.PositionX < minX)
			{
				minX = ally.PositionX;
				front = ally;
			}
		}

		return front;
	}

	public bool CanPassThrough(CombatUnitData a, CombatUnitData b) =>
		a.IsAlly && b.IsAlly;

	public bool BlocksEnemy(CombatUnitData ally, CombatUnitData enemy) =>
		ally.IsAlly && !enemy.IsAlly && ally.CombatState == UnitCombatState.InRange
			&& IsInAttackRange(ally, enemy);

	public List<PositionChangeEvent> ApplyWorldMove(CombatUnitData unit, MoveTag tag, CombatUnitData? target = null)
	{
		var oldX = unit.PositionX;
		var distance = tag.Distance;
		if (tag.Kind == MoveTagKind.Charge && unit.ActiveSkill?.Id == "charge_strike")
		{
			distance = Math.Min(distance, (int)MaxChargeStrikeDistance);
		}

		var delta = tag.Kind switch
		{
			MoveTagKind.Charge => distance,
			MoveTagKind.Retreat => -distance,
			MoveTagKind.ForceSwap => 0f,
			_ => 0f,
		};

		if (Math.Abs(delta) < 0.01f)
		{
			return new List<PositionChangeEvent>();
		}

		var movedX = unit.PositionX + delta;
		if (tag.Kind == MoveTagKind.Charge && unit.ActiveSkill?.Id == "charge_strike" && target != null)
		{
			movedX = ClampChargeStrikeX(unit, target, movedX);
		}

		unit.PositionX = Math.Clamp(movedX, MinAllyX, MaxAllyX);
		if (Math.Abs(unit.PositionX - oldX) < 0.01f)
		{
			return new List<PositionChangeEvent>();
		}

		return new List<PositionChangeEvent>
		{
			new(unit.Id, oldX, unit.PositionX),
		};
	}

	public void MoveTowardEnemy(CombatUnitData unit, CombatUnitData enemy, float moveSpeed, float delta)
	{
		MoveTowardTarget(unit, enemy, moveSpeed * 0.1f * delta);
	}

	public void MoveTowardTarget(CombatUnitData unit, CombatUnitData target, float step)
	{
		var dist = GetEdgeDistanceBetween(unit, target);
		var range = GetAttackRange(unit);
		if (dist <= range)
		{
			return;
		}

		var oldX = unit.PositionX;
		var desiredCenterGap = range + unit.HitBoxRadius + target.HitBoxRadius;
		if (unit.PositionX < target.PositionX)
		{
			var stopX = target.PositionX - desiredCenterGap;
			unit.PositionX = Math.Min(unit.PositionX + step, stopX);
		}
		else
		{
			var stopX = target.PositionX + desiredCenterGap;
			unit.PositionX = Math.Max(unit.PositionX - step, stopX);
		}

		var minX = unit.IsAlly ? MinAllyX : 20f;
		var maxX = unit.IsAlly ? MaxAllyX : EnemyAnchorX;
		unit.PositionX = Math.Clamp(unit.PositionX, minX, maxX);
		if (Math.Abs(unit.PositionX - oldX) >= 0.01f)
		{
			unit.CombatState = UnitCombatState.Moving;
		}
	}

	private static float GetAttackRange(CombatUnitData unit)
	{
		var range = unit.Stats.GetFinal(StatId.AtkRange);
		return range > 0f ? range : DefaultAttackRange;
	}

	private static float GetEdgeDistanceBetween(CombatUnitData from, CombatUnitData to)
	{
		var centerDist = Math.Abs(to.PositionX - from.PositionX);
		var edgeDist = centerDist - from.HitBoxRadius - to.HitBoxRadius;
		return Math.Max(0f, edgeDist);
	}

	private static float ClampChargeStrikeX(CombatUnitData unit, CombatUnitData target, float targetX)
	{
		if (unit.PositionX <= target.PositionX)
		{
			var maxX = target.PositionX - ChargeStopOffset;
			return Math.Min(targetX, maxX);
		}

		var minX = target.PositionX + ChargeStopOffset;
		return Math.Max(targetX, minX);
	}

	public void MarchRight(CombatUnitData unit, float moveSpeed, float delta)
	{
		var pixelsPerSecond = moveSpeed * 0.1f;
		var step = pixelsPerSecond * delta;
		unit.PositionX = Math.Min(unit.PositionX + step, MaxAllyX);
		if (step > 0.01f)
		{
			unit.CombatState = UnitCombatState.Moving;
		}
	}

	public void RestoreFormation(
		IReadOnlyList<CombatUnitData> allies,
		IReadOnlyDictionary<string, float> initialX)
	{
		foreach (var ally in allies)
		{
			if (initialX.TryGetValue(ally.Id, out var x))
			{
				ally.PositionX = x;
			}
		}
	}
}
