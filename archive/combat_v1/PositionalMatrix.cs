using System;
using System.Collections.Generic;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Combat;

/// <summary>
/// Manages ally formation. index 0 = front (1号位), index 2 = back (3号位).
/// Charge moves toward 0 via insertion; Retreat moves toward 2.
/// ForceSwap: at index 0 swap with 1, else swap with index - 1.
/// </summary>
public class PositionalMatrix
{
	private readonly CombatUnitData?[] _slots = new CombatUnitData?[GameConstants.AllySlotCount];

	public IReadOnlyList<CombatUnitData?> Slots => _slots;

	public void SetFormation(IReadOnlyList<CombatUnitData> allies)
	{
		Array.Clear(_slots, 0, _slots.Length);
		foreach (var ally in allies)
		{
			var index = Math.Clamp(ally.FormationIndex, 0, _slots.Length - 1);
			_slots[index] = ally;
			ally.FormationIndex = index;
		}
	}

	public List<PositionChangeEvent> TryApplyMoveTag(CombatUnitData unit, MoveTag tag)
	{
		var oldIndex = unit.FormationIndex;
		if (oldIndex < 0 || oldIndex >= _slots.Length || _slots[oldIndex] != unit)
		{
			return new List<PositionChangeEvent>();
		}

		var newIndex = ResolveTargetIndex(oldIndex, tag);
		if (newIndex == oldIndex)
		{
			return new List<PositionChangeEvent>();
		}

		if (tag.Kind == MoveTagKind.ForceSwap)
		{
			return ApplySwap(oldIndex, newIndex);
		}

		return ApplyInsertion(oldIndex, newIndex);
	}

	private static int ResolveTargetIndex(int oldIndex, MoveTag tag) =>
		tag.Kind switch
		{
			MoveTagKind.Charge => Math.Max(0, oldIndex - tag.Distance),
			MoveTagKind.Retreat => Math.Min(GameConstants.AllySlotCount - 1, oldIndex + tag.Distance),
			MoveTagKind.ForceSwap => oldIndex == 0 ? 1 : oldIndex - 1,
			_ => oldIndex,
		};

	private List<PositionChangeEvent> ApplyInsertion(int oldIndex, int newIndex)
	{
		var changes = new List<PositionChangeEvent>();
		var oldIndices = CaptureIndices();

		var unit = _slots[oldIndex];
		if (unit == null)
		{
			return changes;
		}

		if (newIndex < oldIndex)
		{
			for (var i = oldIndex; i > newIndex; i--)
			{
				_slots[i] = _slots[i - 1];
			}
		}
		else
		{
			for (var i = oldIndex; i < newIndex; i++)
			{
				_slots[i] = _slots[i + 1];
			}
		}

		_slots[newIndex] = unit;
		SyncFormationIndices();
		CollectChanges(oldIndices, changes);
		return changes;
	}

	private List<PositionChangeEvent> ApplySwap(int indexA, int indexB)
	{
		var changes = new List<PositionChangeEvent>();
		var oldIndices = CaptureIndices();

		(_slots[indexA], _slots[indexB]) = (_slots[indexB], _slots[indexA]);
		SyncFormationIndices();
		CollectChanges(oldIndices, changes);
		return changes;
	}

	private Dictionary<string, int> CaptureIndices()
	{
		var map = new Dictionary<string, int>();
		for (var i = 0; i < _slots.Length; i++)
		{
			if (_slots[i] != null)
			{
				map[_slots[i]!.Id] = i;
			}
		}

		return map;
	}

	private void SyncFormationIndices()
	{
		for (var i = 0; i < _slots.Length; i++)
		{
			if (_slots[i] != null)
			{
				_slots[i]!.FormationIndex = i;
			}
		}
	}

	private void CollectChanges(Dictionary<string, int> oldIndices, List<PositionChangeEvent> changes)
	{
		for (var i = 0; i < _slots.Length; i++)
		{
			var unit = _slots[i];
			if (unit == null)
			{
				continue;
			}

			if (oldIndices.TryGetValue(unit.Id, out var oldIndex) && oldIndex != i)
			{
				changes.Add(new PositionChangeEvent(unit.Id, oldIndex, i));
			}
		}
	}

	public CombatUnitData? GetFrontAlly()
	{
		return _slots[0];
	}
}
