using System.Collections.Generic;

namespace RougeliteIdle.Save;

public sealed class SaveData
{
	public int Version { get; set; } = 3;
	public long LastSessionUnix { get; set; }

	public int StarChartPoints { get; set; }
	public float GlobalStatBonusPercent { get; set; }
	public List<string> StarChartPurchasedNodes { get; set; } = new();

	public int TeamLevel { get; set; } = 1; // Deprecated: legacy fallback for RosterProgress migration
	public float TeamExp { get; set; }
	public int Gold { get; set; }
	public int WonderlandTickets { get; set; }
	public int TrainingBonusChests { get; set; }

	public List<string> DbUnlockedNodes { get; set; } = new();
	public List<string> ActiveRosterIds { get; set; } = new();

	public List<ItemSaveDto> IdentifiedItems { get; set; } = new();
	public List<ChestSaveDto> UnidentifiedChests { get; set; } = new();
	public Dictionary<string, int> PendingChestsByQuality { get; set; } = new();
	public string ActivePendingQuality { get; set; } = "common";
	public Dictionary<string, Dictionary<string, ItemSaveDto>> EquippedByUnit { get; set; } = new();

	public Dictionary<string, List<string>> SkillUnlockedByRoster { get; set; } = new();
	public Dictionary<string, Dictionary<string, string>> SkillEquippedByRoster { get; set; } = new();

	public CombatSaveDto? Combat { get; set; }
	public Dictionary<string, RosterProgressDto> RosterProgress { get; set; } = new();
	public List<string> UnlockedStageIds { get; set; } = new();
	public List<string> ClearedStageIds { get; set; } = new();
}

public sealed class RosterProgressDto
{
	public int Level { get; set; } = 1;
	public float Exp { get; set; }
}

public sealed class ItemSaveDto
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Quality { get; set; } = "common";
	public string Slot { get; set; } = "Weapon";
	public int ItemLevel { get; set; }
	public float RolledBaseStat { get; set; }
	public string EffectId { get; set; } = string.Empty;
	public List<AffixSaveDto> Affixes { get; set; } = new();
}

public sealed class AffixSaveDto
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public float Value { get; set; }
	public bool IsPrimary { get; set; }
}

public sealed class ChestSaveDto
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Quality { get; set; } = "common";
}

public sealed class CombatSaveDto
{
	public string StageId { get; set; } = "chapter_01_level_01";
	public int StageRunState { get; set; }
	public float RunProgress { get; set; }
	public int CurrentWaveIndex { get; set; }
	public int RemainingInWave { get; set; }
	public string ActiveEnemyTemplateId { get; set; } = string.Empty;
	public UnitCombatSaveDto? Enemy { get; set; }
	public List<UnitCombatSaveDto> Allies { get; set; } = new();
}

public sealed class UnitCombatSaveDto
{
	public string Id { get; set; } = string.Empty;
	public float CurrentHp { get; set; }
	public float MaxHp { get; set; }
	public float PositionX { get; set; }
	public float ActionGauge { get; set; }
}
