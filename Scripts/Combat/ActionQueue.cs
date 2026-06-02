using System.Collections.Generic;
using System.Linq;

namespace RougeliteIdle.Combat;

/// <summary>
/// FIFO queue for ready combat actions, ordered by speed. Prevents duplicate entries per unit id.
/// </summary>
public class ActionQueue
{
	private readonly Queue<CombatUnitData> _queue = new();
	private readonly HashSet<string> _queuedIds = new();

	public int Count => _queue.Count;

	public bool TryEnqueue(CombatUnitData unit)
	{
		if (!_queuedIds.Add(unit.Id))
		{
			return false;
		}

		_queue.Enqueue(unit);
		return true;
	}

	public CombatUnitData? Dequeue()
	{
		if (_queue.Count == 0)
		{
			return null;
		}

		var unit = _queue.Dequeue();
		_queuedIds.Remove(unit.Id);
		return unit;
	}

	public void Clear()
	{
		_queue.Clear();
		_queuedIds.Clear();
	}

	public void EnqueueReadyUnits(IEnumerable<CombatUnitData> units)
	{
		foreach (var unit in units.Where(u => u.CurrentHp > 0f && ActionGauge.IsReady(u)).OrderByDescending(u => u.Speed))
		{
			TryEnqueue(unit);
		}
	}
}
