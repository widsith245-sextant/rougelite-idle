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
	private const float AdjacentHitRange = 48f;

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

	public bool IsInAttackRange(CombatUnitData attacker, CombatUnitData target) =>
		GetEdgeDistanceBetween(attacker, target) <= GetAttackRange(attacker);

	public bool IsBehindTarget(CombatUnitData attacker, CombatUnitData target) =>
		attacker.PositionX > target.PositionX;

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

	public CombatUnitData? GetBackAlly(IReadOnlyList<CombatUnitData> allies)
	{
		CombatUnitData? back = null;
		var maxX = float.MinValue;
		foreach (var ally in allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			if (ally.PositionX > maxX)
			{
				maxX = ally.PositionX;
				back = ally;
			}
		}

		return back;
	}

	public static float ApplySeparation(
		float oldX,
		float newX,
		CombatUnitData unit,
		IReadOnlyList<CombatUnitData>? others)
	{
		if (others == null || Math.Abs(newX - oldX) < 0.001f)
		{
			return newX;
		}

		var movingRight = newX > oldX;
		foreach (var other in others)
		{
			if (other.Id == unit.Id || other.CurrentHp <= 0f)
			{
				continue;
			}

			var minGap = unit.HitBoxRadius + other.HitBoxRadius;
			if (movingRight && other.PositionX <= newX + 0.001f)
			{
				newX = Math.Max(newX, other.PositionX + minGap);
			}
			else if (!movingRight && other.PositionX >= newX - 0.001f)
			{
				newX = Math.Min(newX, other.PositionX - minGap);
			}
		}

		return newX;
	}

	public List<CombatUnitData> GetUnitsInHitRange(
		float centerX,
		float radius,
		IReadOnlyList<CombatUnitData> candidates,
		int maxTargets = 0)
	{
		var hits = new List<CombatUnitData>();
		foreach (var unit in candidates)
		{
			if (unit.CurrentHp <= 0f)
			{
				continue;
			}

			var dist = Math.Abs(unit.PositionX - centerX) - unit.HitBoxRadius;
			if (dist > radius)
			{
				continue;
			}

			hits.Add(unit);
			if (maxTargets > 0 && hits.Count >= maxTargets)
			{
				break;
			}
		}

		return hits;
	}

	public List<CombatUnitData> GetAdjacentUnits(
		CombatUnitData origin,
		IReadOnlyList<CombatUnitData> candidates,
		float range = AdjacentHitRange)
	{
		return GetUnitsInHitRange(origin.PositionX, range, candidates);
	}

	public List<PositionChangeEvent> ApplyWorldMove(
		CombatUnitData unit,
		MoveTag tag,
		CombatUnitData? target = null,
		IReadOnlyList<CombatUnitData>? allies = null,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		if (tag.Kind == MoveTagKind.ForceSwap)
		{
			return ApplyForceSwap(unit, allies);
		}

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

		var minX = unit.IsAlly ? MinAllyX : 20f;
		var maxX = unit.IsAlly ? MaxAllyX : EnemyAnchorX;
		movedX = Math.Clamp(movedX, minX, maxX);
		movedX = ApplySeparation(oldX, movedX, unit, obstacles);
		movedX = Math.Clamp(movedX, minX, maxX);
		unit.PositionX = movedX;
		if (Math.Abs(unit.PositionX - oldX) < 0.01f)
		{
			return new List<PositionChangeEvent>();
		}

		return new List<PositionChangeEvent>
		{
			new(unit.Id, oldX, unit.PositionX),
		};
	}

	public List<PositionChangeEvent> ApplyForceSwap(
		CombatUnitData unit,
		IReadOnlyList<CombatUnitData>? allies)
	{
		if (allies == null || allies.Count < 2)
		{
			return new List<PositionChangeEvent>();
		}

		CombatUnitData? partner = null;
		var bestDist = float.MaxValue;
		foreach (var ally in allies)
		{
			if (ally.Id == unit.Id || ally.CurrentHp <= 0f)
			{
				continue;
			}

			var dist = Math.Abs(ally.PositionX - unit.PositionX);
			if (dist < bestDist)
			{
				bestDist = dist;
				partner = ally;
			}
		}

		if (partner == null)
		{
			return new List<PositionChangeEvent>();
		}

		var oldA = unit.PositionX;
		var oldB = partner.PositionX;
		unit.PositionX = oldB;
		partner.PositionX = oldA;
		return new List<PositionChangeEvent>
		{
			new(unit.Id, oldA, unit.PositionX),
			new(partner.Id, oldB, partner.PositionX),
		};
	}

	public void MoveTowardEnemy(
		CombatUnitData unit,
		CombatUnitData enemy,
		float moveSpeed,
		float delta,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		MoveTowardTarget(unit, enemy, moveSpeed * 0.1f * delta, obstacles);
	}

	public void MoveTowardTarget(
		CombatUnitData unit,
		CombatUnitData target,
		float step,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		if (IsInAttackRange(unit, target))
		{
			unit.CombatState = UnitCombatState.InRange;
			return;
		}

		var dist = GetEdgeDistanceBetween(unit, target);
		var range = GetAttackRange(unit);
		if (dist <= range)
		{
			unit.CombatState = UnitCombatState.InRange;
			return;
		}

		var oldX = unit.PositionX;
		var desiredCenterGap = range + unit.HitBoxRadius + target.HitBoxRadius;
		float movedX;
		if (unit.PositionX < target.PositionX)
		{
			var stopX = target.PositionX - desiredCenterGap;
			movedX = Math.Min(unit.PositionX + step, stopX);
		}
		else
		{
			var stopX = target.PositionX + desiredCenterGap;
			movedX = Math.Max(unit.PositionX - step, stopX);
		}

		var minX = unit.IsAlly ? MinAllyX : 20f;
		var maxX = unit.IsAlly ? MaxAllyX : EnemyAnchorX;
		movedX = Math.Clamp(movedX, minX, maxX);
		movedX = ApplySeparation(oldX, movedX, unit, obstacles);
		movedX = Math.Clamp(movedX, minX, maxX);
		unit.PositionX = movedX;
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

	public void MarchRight(
		CombatUnitData unit,
		float moveSpeed,
		float delta,
		IReadOnlyList<CombatUnitData>? obstacles = null)
	{
		var oldX = unit.PositionX;
		var pixelsPerSecond = moveSpeed * 0.1f;
		var step = pixelsPerSecond * delta;
		var movedX = Math.Min(unit.PositionX + step, MaxAllyX);
		movedX = ApplySeparation(oldX, movedX, unit, obstacles);
		movedX = Math.Clamp(movedX, MinAllyX, MaxAllyX);
		unit.PositionX = movedX;
		if (Math.Abs(unit.PositionX - oldX) > 0.01f)
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
