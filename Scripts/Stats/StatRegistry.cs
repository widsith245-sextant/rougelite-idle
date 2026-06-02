using System;
using System.Collections.Generic;

namespace RougeliteIdle.Stats;

public static class StatRegistry
{
	public const float MaxAtkSpeed = 5f;
	public const float MaxCdr = 0.75f;
	public const float MaxResist = 0.75f;
	public const float DefaultCritDamage = 1.5f;
	public const float FrontLineThresholdX = 120f;
	public const float PositionMoveThreshold = 8f;

	private static readonly Dictionary<StatId, (float Min, float Max)> Limits = new()
	{
		{ StatId.AtkSpeed, (0.1f, MaxAtkSpeed) },
		{ StatId.CritRate, (0f, 1f) },
		{ StatId.CritDamage, (1f, 5f) },
		{ StatId.Cdr, (0f, MaxCdr) },
		{ StatId.FireResist, (0f, MaxResist) },
		{ StatId.ColdResist, (0f, MaxResist) },
		{ StatId.LightningResist, (0f, MaxResist) },
		{ StatId.ChaosResist, (0f, MaxResist) },
		{ StatId.Dodge, (0f, 0.95f) },
		{ StatId.Block, (0f, 0.95f) },
	};

	public static float Clamp(StatId id, float value)
	{
		if (Limits.TryGetValue(id, out var range))
		{
			return Math.Clamp(value, range.Min, range.Max);
		}

		return Math.Max(0f, value);
	}

	public static float CombineIncreased(IReadOnlyList<float> increasedValues)
	{
		var sum = 0f;
		foreach (var v in increasedValues)
		{
			sum += v;
		}

		return sum;
	}

	public static float ApplyIncreased(float baseValue, float increasedSum) =>
		baseValue * (1f + increasedSum);

	public static float ComputeDamage(UnitStats stats)
	{
		var phys = stats.GetFinal(StatId.Damage);
		var fire = stats.GetFinal(StatId.FireIncrease);
		var cold = stats.GetFinal(StatId.ColdIncrease);
		var lightning = stats.GetFinal(StatId.LightningIncrease);
		var chaos = stats.GetFinal(StatId.ChaosIncrease);
		return phys + fire + cold + lightning + chaos;
	}

	public static float ComputeDps(UnitStats stats)
	{
		var damage = stats.GetFinal(StatId.Damage);
		if (damage <= 0f)
		{
			damage = ComputeDamage(stats);
		}

		var atkSpeed = stats.GetFinal(StatId.AtkSpeed);
		var critRate = stats.GetFinal(StatId.CritRate);
		var critDamage = stats.GetFinal(StatId.CritDamage);
		if (critDamage < 1f)
		{
			critDamage = DefaultCritDamage;
		}

		return damage * atkSpeed * (1f + critRate * (critDamage - 1f));
	}

	public static string ToKey(StatId id) => id.ToString();
}
