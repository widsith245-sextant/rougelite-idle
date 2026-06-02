using System;
using Godot;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

/// <summary>
/// Final = (Base + Flat) * (1 + Increased) * More * SkillMultiplier; optional crit roll.
/// </summary>
public static class DamageFormula
{
	public static float Calculate(
		float baseStat,
		float equipmentFlat,
		float equipmentPercent,
		float metaPercent,
		float bdModifier,
		float skillMultiplier)
	{
		var baseValue = baseStat + equipmentFlat;
		var multiplier = 1f + equipmentPercent + metaPercent;
		var bd = 1f + bdModifier;
		return baseValue * multiplier * bd * skillMultiplier;
	}

	public static float CalculateFromStats(
		UnitStats stats,
		float metaPercent,
		float bdModifier,
		float skillMultiplier,
		RandomNumberGenerator? rng,
		out bool isCrit,
		string damageType = "physical")
	{
		isCrit = false;
		var profile = DamageProfileLoader.Get(damageType);
		var damage = stats.GetFinal(StatId.Damage);
		if (damage <= 0f)
		{
			damage = StatRegistry.ComputeDamage(stats);
		}

		damage *= profile.DamageScale;

		var critRate = stats.GetFinal(StatId.CritRate);
		var critDamage = stats.GetFinal(StatId.CritDamage);
		if (critDamage < 1f)
		{
			critDamage = StatRegistry.DefaultCritDamage;
		}

		if (profile.CritAllowed && rng != null && rng.Randf() < critRate)
		{
			isCrit = true;
			damage *= critDamage;
		}

		var increased = metaPercent;
		var bd = 1f + bdModifier;
		return damage * (1f + increased) * bd * skillMultiplier;
	}

	public static float ApplyDefense(float rawDamage, float defensePercent) =>
		rawDamage * Math.Max(0.05f, 1f - defensePercent);
}
