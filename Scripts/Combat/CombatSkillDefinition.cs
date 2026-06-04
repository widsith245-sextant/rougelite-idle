using System.Collections.Generic;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Combat;

public class CombatSkillDefinition
{
	public string Id { get; set; } = string.Empty;
	public float SkillMultiplier { get; set; } = 1f;
	public string TriggerSlot { get; set; } = "active_0";
	public SkillTriggerKind TriggerKind { get; set; } = SkillTriggerKind.GaugeFull;
	public SkillTriggerParam TriggerParam { get; set; } = new();
	public string EffectId { get; set; } = string.Empty;
	public List<MoveTag> MoveTags { get; } = new();
	public List<AppliedEffectEntry> AppliedEffects { get; } = new();
	public List<string> DescriptionTokens { get; } = new();
}
