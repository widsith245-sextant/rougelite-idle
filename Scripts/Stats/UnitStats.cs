using System.Collections.Generic;
using Godot;

namespace RougeliteIdle.Stats;

public class UnitStats
{
	private readonly Dictionary<StatId, float> _base = new();
	private readonly Dictionary<StatId, float> _flat = new();
	private readonly Dictionary<StatId, float> _increased = new();
	private readonly Dictionary<StatId, float> _final = new();

	public void SetBase(StatId id, float value)
	{
		_base[id] = value;
		_final.Clear();
	}

	public void AddFlat(StatId id, float value)
	{
		_flat.TryGetValue(id, out var current);
		_flat[id] = current + value;
		_final.Clear();
	}

	public void AddIncreased(StatId id, float value)
	{
		_increased.TryGetValue(id, out var current);
		_increased[id] = current + value;
		_final.Clear();
	}

	public void ClearModifiers()
	{
		_flat.Clear();
		_increased.Clear();
		_final.Clear();
	}

	public float GetBase(StatId id) => _base.GetValueOrDefault(id);

	public float GetFinal(StatId id)
	{
		if (_final.TryGetValue(id, out var cached))
		{
			return cached;
		}

		var baseVal = GetBase(id);
		_flat.TryGetValue(id, out var flat);
		_increased.TryGetValue(id, out var inc);
		var value = StatRegistry.ApplyIncreased(baseVal + flat, inc);
		value = StatRegistry.Clamp(id, value);
		_final[id] = value;
		return value;
	}

	public void RecalculateDerived()
	{
		var damage = StatRegistry.ComputeDamage(this);
		_final[StatId.Damage] = damage;
		_base[StatId.Damage] = damage;

		var dps = StatRegistry.ComputeDps(this);
		_final[StatId.Dps] = dps;
		_base[StatId.Dps] = dps;
	}

	public Godot.Collections.Dictionary ToDictionary()
	{
		RecalculateDerived();
		var dict = new Godot.Collections.Dictionary();
		foreach (StatId id in System.Enum.GetValues(typeof(StatId)))
		{
			dict[StatRegistry.ToKey(id)] = GetFinal(id);
		}

		return dict;
	}
}
