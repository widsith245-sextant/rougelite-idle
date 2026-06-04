using System.Collections.Generic;
using RougeliteIdle.Combat.Effects;

namespace RougeliteIdle.Combat;

public sealed class AppliedEffectEntry
{
	public string EffectId { get; set; } = string.Empty;
	public int Pile { get; set; } = 1;
	public float Intensity { get; set; }
	public string Target { get; set; } = "primary";
}

public static class SkillEffectApplier
{
	public static void ApplySkillEffects(
		CombatSkillDefinition? skill,
		CombatUnitData actor,
		CombatUnitData? primaryTarget,
		IReadOnlyList<CombatUnitData> allies,
		IReadOnlyList<CombatUnitData> enemies,
		BattlefieldController battlefield)
	{
		if (skill?.AppliedEffects == null || skill.AppliedEffects.Count == 0)
		{
			return;
		}

		foreach (var entry in skill.AppliedEffects)
		{
			var target = ResolveTarget(entry.Target, actor, primaryTarget, allies, enemies);
			if (target == null)
			{
				continue;
			}

			CombatEffectRegistry.TryApply(entry.EffectId, actor, target, entry.Pile, entry.Intensity);
		}
	}

	private static CombatUnitData? ResolveTarget(
		string targetKind,
		CombatUnitData actor,
		CombatUnitData? primary,
		IReadOnlyList<CombatUnitData> allies,
		IReadOnlyList<CombatUnitData> enemies)
	{
		return targetKind switch
		{
			"self" => actor,
			"primary" or "single_enemy" => primary,
			"attacker" => actor,
			_ => primary,
		};
	}
}
