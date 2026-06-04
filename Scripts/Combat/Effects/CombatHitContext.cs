namespace RougeliteIdle.Combat.Effects;

public sealed class CombatHitContext
{
	public CombatUnitData Source { get; init; } = null!;
	public CombatUnitData Target { get; init; } = null!;
	public string SkillId { get; init; } = string.Empty;
	public string DamageType { get; init; } = "physical";
	public float RawAmount { get; set; }
	public float FinalAmount { get; set; }
	public bool IsCrit { get; set; }
	public float FlatBonus { get; set; }
	public float PercentBonus { get; set; }
	public float PercentReduction { get; set; }
	public float CritRateDelta { get; set; }
	public float ActionPowerBonus { get; set; }
	public float SkillMultiplier { get; set; } = 1f;
	public bool Cancelled { get; set; }
	public float RetaliationDamage { get; set; }
	public string DisplayTag { get; set; } = string.Empty;

	public static CombatHitContext Create(
		CombatUnitData source,
		CombatUnitData target,
		float rawAmount,
		bool isCrit,
		string damageType,
		string skillId = "",
		float skillMultiplier = 1f)
	{
		return new CombatHitContext
		{
			Source = source,
			Target = target,
			RawAmount = rawAmount,
			FinalAmount = rawAmount,
			IsCrit = isCrit,
			DamageType = damageType,
			SkillId = skillId,
			SkillMultiplier = skillMultiplier,
		};
	}
}
