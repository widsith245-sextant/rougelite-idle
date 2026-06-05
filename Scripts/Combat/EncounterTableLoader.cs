using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Meta;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public static class EncounterTableLoader
{
	private const string DefaultEncounterPath = "res://data/tables/combat/encounter_default.json";

	public static (List<CombatUnitData> Allies, List<CombatUnitData> Enemies) LoadDefaultEncounter()
	{
		return LoadEncounter(DefaultEncounterPath);
	}

	public static (List<CombatUnitData> Allies, List<CombatUnitData> Enemies) LoadEncounter(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"Encounter table not found: {path}");
			return (CreateFallbackAllies(), CreateFallbackEnemies());
		}

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"Failed to open encounter table: {path}");
			return (CreateFallbackAllies(), CreateFallbackEnemies());
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<EncounterRoot>(file.GetAsText(), options);
		if (root == null)
		{
			return (CreateFallbackAllies(), CreateFallbackEnemies());
		}

		return (MapUnits(root.Allies, true), MapUnits(root.Enemies, false));
	}

	public const float FormationBaseX = 40f;
	public const float FormationSpacing = 32f;

	public static List<CombatUnitData> BuildActiveAllies()
	{
		var (allies, _) = LoadDefaultEncounter();
		return FilterActiveAllies(allies);
	}

	public static void ApplyPartyFormation(List<CombatUnitData> allies)
	{
		foreach (var unit in allies)
		{
			var slotIndex = ResolveSlotIndex(unit.Id);
			if (slotIndex < 0)
			{
				continue;
			}

			unit.FormationIndex = slotIndex;
			unit.PositionX = FormationBaseX + slotIndex * FormationSpacing;
		}
	}

	public static List<CombatUnitData> FilterActiveAllies(List<CombatUnitData> allies)
	{
		var party = GetPartyManager();
		if (party == null)
		{
			return allies;
		}

		var filtered = new List<CombatUnitData>();
		foreach (var unit in allies)
		{
			var slotIndex = ResolveSlotIndex(unit.Id);
			if (slotIndex < 0 || !party.IsSlotUnlocked(slotIndex))
			{
				continue;
			}

			if (string.IsNullOrEmpty(party.GetRosterIdForSlot(slotIndex)))
			{
				continue;
			}

			filtered.Add(unit);
		}

		ApplyCharacterSkills(filtered);
		ApplyPartyFormation(filtered);
		return filtered;
	}

	private static void ApplyCharacterSkills(List<CombatUnitData> allies)
	{
		var party = GetPartyManager();
		var skills = GetCharacterSkillManager();
		if (party == null || skills == null)
		{
			return;
		}

		foreach (var unit in allies)
		{
			var slotIndex = ResolveSlotIndex(unit.Id);
			if (slotIndex < 0)
			{
				continue;
			}

			var rosterId = party.GetRosterIdForSlot(slotIndex);
			if (string.IsNullOrEmpty(rosterId))
			{
				continue;
			}

			skills.ApplySkillsToUnit(unit, rosterId);
		}
	}

	private static CharacterSkillManager? GetCharacterSkillManager()
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		return tree?.Root.GetNodeOrNull<CharacterSkillManager>("/root/CharacterSkillManager");
	}

	private static RosterProgressionManager? GetRosterProgressionManager()
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		return tree?.Root.GetNodeOrNull<RosterProgressionManager>("/root/RosterProgressionManager");
	}

	public static int ResolveSlotIndexPublic(string unitId) => ResolveSlotIndex(unitId);

	private static int ResolveSlotIndex(string unitId) => unitId switch
	{
		"ally_a" => 0,
		"ally_b" => 1,
		"ally_c" => 2,
		_ => -1,
	};

	private static List<CombatUnitData> MapUnits(List<UnitEntry>? entries, bool isAlly)
	{
		var units = new List<CombatUnitData>();
		if (entries == null)
		{
			return units;
		}

		foreach (var entry in entries)
		{
			units.Add(MapUnit(entry, isAlly));
		}

		return units;
	}

	private static CombatUnitData MapUnit(UnitEntry entry, bool isAlly)
	{
		var party = GetPartyManager();
		var classId = isAlly
			? FirstNonEmpty(ResolveClassId(entry.Id, party), entry.ClassId)
			: (entry.ClassId ?? string.Empty);
		var displayName = entry.DisplayName;
		var level = entry.Level > 0 ? entry.Level : 1;
		if (isAlly && party != null)
		{
			var slotName = party.GetDisplayNameForUnit(entry.Id);
			if (!string.IsNullOrEmpty(slotName))
			{
				displayName = slotName;
			}

			var rosterId = party.GetRosterIdForUnit(entry.Id);
			var rosterProg = GetRosterProgressionManager();
			if (!string.IsNullOrEmpty(rosterId) && rosterProg != null)
			{
				level = rosterProg.GetLevel(rosterId);
			}
		}

		var unit = new CombatUnitData
		{
			Id = entry.Id,
			DisplayName = displayName,
			ClassId = classId,
			Level = level,
			IsAlly = isAlly,
			MaxHp = entry.MaxHp,
			CurrentHp = entry.MaxHp,
			Speed = entry.Speed,
			BaseAttack = entry.BaseAttack,
			FormationIndex = entry.FormationIndex,
			PositionX = entry.InitialPositionX > 0f
				? entry.InitialPositionX
				: 40f + entry.FormationIndex * 32f,
			ActiveSkill = MapSkill(entry.ActiveSkill),
		};

		if (!isAlly)
		{
			unit.PositionX = BattlefieldController.EnemyAnchorX;
		}

		if (entry.Passives != null)
		{
			foreach (var passive in entry.Passives)
			{
				unit.Passives.Add(new PassiveSkillDefinition
				{
					Id = passive.Id,
					TriggerType = ParseTrigger(passive.TriggerType),
					TargetSlot = passive.TargetSlot,
					SkillMultiplier = passive.SkillMultiplier,
				});
			}
		}

		return unit;
	}

	private static string ResolveClassId(string unitId, PartyManager? party)
	{
		if (party != null)
		{
			var fromParty = party.GetClassIdForUnit(unitId);
			if (!string.IsNullOrEmpty(fromParty))
			{
				return fromParty;
			}
		}

		return unitId switch
		{
			"ally_a" => "Vanguard_01",
			"ally_b" => "Sniper_01",
			"ally_c" => "Mage_01",
			_ => string.Empty,
		};
	}

	private static string FirstNonEmpty(params string?[] values)
	{
		foreach (var value in values)
		{
			if (!string.IsNullOrEmpty(value))
			{
				return value;
			}
		}

		return string.Empty;
	}

	private static PartyManager? GetPartyManager()
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		return tree?.Root.GetNodeOrNull<PartyManager>("/root/PartyManager");
	}

	private static CombatSkillDefinition? MapSkill(SkillEntry? entry)
	{
		if (entry == null)
		{
			return null;
		}

		var skill = new CombatSkillDefinition
		{
			Id = entry.Id,
			SkillMultiplier = entry.SkillMultiplier,
		};

		if (entry.MoveTags != null)
		{
			foreach (var tag in entry.MoveTags)
			{
				skill.MoveTags.Add(new MoveTag(ParseMoveKind(tag.Kind), tag.Distance));
			}
		}

		return skill;
	}

	private static MoveTagKind ParseMoveKind(string kind) =>
		Enum.TryParse<MoveTagKind>(kind, true, out var parsed) ? parsed : MoveTagKind.Charge;

	private static PassiveTriggerType ParseTrigger(string trigger) =>
		Enum.TryParse<PassiveTriggerType>(trigger, true, out var parsed)
			? parsed
			: PassiveTriggerType.OnAnyAllyMoved;

	private static List<CombatUnitData> CreateFallbackAllies()
	{
		return new List<CombatUnitData>
		{
			new() { Id = "ally_a", ClassId = "Vanguard_01", DisplayName = "A", IsAlly = true, MaxHp = 100, CurrentHp = 100, Speed = 12, BaseAttack = 10, FormationIndex = 0, PositionX = 40f },
			new() { Id = "ally_b", ClassId = "Sniper_01", DisplayName = "B", IsAlly = true, MaxHp = 100, CurrentHp = 100, Speed = 10, BaseAttack = 8, FormationIndex = 1, PositionX = 72f },
			new() { Id = "ally_c", ClassId = "Mage_01", DisplayName = "C", IsAlly = true, MaxHp = 100, CurrentHp = 100, Speed = 8, BaseAttack = 12, FormationIndex = 2, PositionX = 104f },
		};
	}

	private static List<CombatUnitData> CreateFallbackEnemies()
	{
		return new List<CombatUnitData>
		{
			new() { Id = "enemy_1", DisplayName = "Enemy", IsAlly = false, MaxHp = 200, CurrentHp = 200, Speed = 6, BaseAttack = 6, PositionX = BattlefieldController.EnemyAnchorX },
		};
	}

	private sealed class EncounterRoot
	{
		public List<UnitEntry>? Allies { get; set; }
		public List<UnitEntry>? Enemies { get; set; }
	}

	private sealed class UnitEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string? ClassId { get; set; }
		public int Level { get; set; } = 1;
		public int FormationIndex { get; set; }
		public float InitialPositionX { get; set; }
		public float Speed { get; set; }
		public float BaseAttack { get; set; }
		public float MaxHp { get; set; }
		public SkillEntry? ActiveSkill { get; set; }
		public List<PassiveEntry>? Passives { get; set; }
	}

	private sealed class SkillEntry
	{
		public string Id { get; set; } = string.Empty;
		public float SkillMultiplier { get; set; } = 1f;
		public List<MoveTagEntry>? MoveTags { get; set; }
	}

	private sealed class MoveTagEntry
	{
		public string Kind { get; set; } = string.Empty;
		public int Distance { get; set; } = 40;
	}

	private sealed class PassiveEntry
	{
		public string Id { get; set; } = string.Empty;
		public string TriggerType { get; set; } = string.Empty;
		public int TargetSlot { get; set; } = -1;
		public float SkillMultiplier { get; set; } = 1f;
	}
}
