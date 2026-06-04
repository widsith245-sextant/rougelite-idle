using System.Collections.Generic;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

/// <summary>
/// Pure data representation of a combat unit. Does not inherit Node.
/// </summary>
public class CombatUnitData
{
	public string Id { get; set; } = string.Empty;
	public string TemplateId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string ClassId { get; set; } = string.Empty;
	public string Archetype { get; set; } = "trash";
	public string DamageType { get; set; } = "physical";
	public int RewardTier { get; set; } = 1;
	public bool IsAlly { get; set; }

	public int Level { get; set; } = 1;
	public float MaxHp { get; set; }
	public float CurrentHp { get; set; }
	public float Speed { get; set; }
	public float BaseAttack { get; set; }
	public float EnemyTemplateAtkSpeed { get; set; }
	public float EnemyTemplateAtkRange { get; set; }
	public float EnemyTemplateMoveSpeed { get; set; }

	public int FormationIndex { get; set; }
	public float PositionX { get; set; }
	public float HitBoxRadius { get; set; } = 10f;
	public float ActionGauge { get; set; }
	public int BasicAttackCounter { get; set; }

	public UnitCombatState CombatState { get; set; } = UnitCombatState.Idle;
	public float NormalAttackTimer { get; set; }
	public float StunTimer { get; set; }
	public UnitStats Stats { get; set; } = new();

	public CombatSkillDefinition? ActiveSkill { get; set; }
	public List<CombatSkillDefinition> ActiveSkills { get; } = new();
	public List<PassiveSkillDefinition> Passives { get; } = new();
	public List<MoveTag> PendingMoveTags { get; } = new();
	public List<ActiveCombatEffect> ActiveEffects { get; } = new();
	public List<OnHitEffectRoll> OnHitEffects { get; } = new();

	public float HpRatio => MaxHp > 0f ? CurrentHp / MaxHp : 0f;
	public bool IsEcchiDamaged => HpRatio < Core.GameConstants.EcchiDamagedHpThreshold;
	public bool IsBlockingOutput =>
		CombatState is UnitCombatState.Stunned
			or UnitCombatState.Casting
			or UnitCombatState.Reposition
		|| StunNextAction;

	public bool StunNextAction { get; set; }
	public bool ChargeAttackPending { get; set; }
	public string EchoRepeatSkillId { get; set; } = string.Empty;
	public string RecursionSkillId { get; set; } = string.Empty;
	public float TempActionPowerBonus { get; set; }
	public float TempCritRateDelta { get; set; }
	public int ResourceStackCap { get; set; } = 20;
}
