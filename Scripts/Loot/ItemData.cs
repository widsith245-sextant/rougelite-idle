using System.Collections.Generic;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Loot;

public class AffixRoll
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public float Value { get; set; }
	public bool IsPrimary { get; set; }
}

public class AffixDefinition
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public float Min { get; set; }
	public float Max { get; set; }
	public bool IsPrimary { get; set; }
}

public class ItemData
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Quality { get; set; } = "common";
	public SlotType Slot { get; set; }
	public int ItemLevel { get; set; }

	public float BaseStatMin { get; set; }
	public float BaseStatMax { get; set; }
	public float RolledBaseStat { get; set; }

	public string? EffectId { get; set; }
	public string ClassId { get; set; } = string.Empty;
	public List<AffixDefinition> AffixDefinitions { get; } = new();
	public List<AffixRoll> Affixes { get; } = new();
}
