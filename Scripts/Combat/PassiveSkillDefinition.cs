namespace RougeliteIdle.Combat;

public enum PassiveTriggerType
{
	OnSelfReachSlot,
	OnSelfReachFront,
	OnAnyAllyMoved,
	OnPointBlank,
	OnFrontLine,
	OnSquadSwap,
	OnXMove,
}

public class PassiveSkillDefinition
{
	public string Id { get; set; } = string.Empty;
	public PassiveTriggerType TriggerType { get; set; }
	public int TargetSlot { get; set; } = -1;
	public float SkillMultiplier { get; set; } = 1f;
	public string EffectId { get; set; } = string.Empty;
}
