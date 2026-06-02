using System.Collections.Generic;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Combat;

public class CombatSkillDefinition
{
	public string Id { get; set; } = string.Empty;
	public float SkillMultiplier { get; set; } = 1f;
	public List<MoveTag> MoveTags { get; } = new();
}
