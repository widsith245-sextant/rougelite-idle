using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Combat;

public static class ClassSkillsLoader
{
	private const string SkillsPath = "res://data/tables/character/class_skills.json";
	private static Dictionary<string, ClassEntry>? _cache;

	public static CombatSkillDefinition? BuildActiveSkill(string classId, string skillId)
	{
		var entry = GetClass(classId);
		if (entry?.Actives == null)
		{
			return null;
		}

		var skill = entry.Actives.FirstOrDefault(s => s.Id == skillId);
		return skill == null ? null : MapActive(skill);
	}

	public static PassiveSkillDefinition? BuildPassive(string classId, string skillId)
	{
		var entry = GetClass(classId);
		if (entry?.Passives == null)
		{
			return null;
		}

		var skill = entry.Passives.FirstOrDefault(s => s.Id == skillId);
		if (skill == null)
		{
			return null;
		}

		return new PassiveSkillDefinition
		{
			Id = skill.Id,
			TriggerType = ParseTrigger(skill.TriggerType),
			TargetSlot = skill.TargetSlot,
			SkillMultiplier = skill.SkillMultiplier,
		};
	}

	private static ClassEntry? GetClass(string classId)
	{
		EnsureLoaded();
		return _cache!.GetValueOrDefault(classId);
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, ClassEntry>();
		if (!FileAccess.FileExists(SkillsPath))
		{
			return;
		}

		using var file = FileAccess.Open(SkillsPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<SkillsRoot>(file.GetAsText(), options);
		if (root?.Classes == null)
		{
			return;
		}

		foreach (var entry in root.Classes)
		{
			_cache[entry.ClassId] = entry;
		}
	}

	private static CombatSkillDefinition MapActive(SkillEntry entry)
	{
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

	private sealed class SkillsRoot
	{
		public List<ClassEntry>? Classes { get; set; }
	}

	private sealed class ClassEntry
	{
		public string ClassId { get; set; } = string.Empty;
		public List<SkillEntry>? Actives { get; set; }
		public List<PassiveEntry>? Passives { get; set; }
	}

	private sealed class SkillEntry
	{
		public string Id { get; set; } = string.Empty;
		public float SkillMultiplier { get; set; } = 1f;
		public List<MoveTagEntry>? MoveTags { get; set; }
	}

	private sealed class PassiveEntry
	{
		public string Id { get; set; } = string.Empty;
		public string TriggerType { get; set; } = string.Empty;
		public int TargetSlot { get; set; } = -1;
		public float SkillMultiplier { get; set; } = 1f;
	}

	private sealed class MoveTagEntry
	{
		public string Kind { get; set; } = string.Empty;
		public int Distance { get; set; } = 40;
	}
}
