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
			EffectId = skill.EffectId ?? string.Empty,
		};
	}

	public static CombatSkillDefinition? GetGaugeSkill(IReadOnlyList<CombatSkillDefinition> actives) =>
		actives.FirstOrDefault(s => s.TriggerKind == SkillTriggerKind.GaugeFull)
		?? (actives.Count > 0 ? actives[0] : null);

	public static CombatSkillDefinition? GetBasicAttackTriggeredSkill(
		IReadOnlyList<CombatSkillDefinition> actives,
		int counter)
	{
		foreach (var skill in actives)
		{
			if (skill.TriggerKind != SkillTriggerKind.BasicAttackCount)
			{
				continue;
			}

			if (counter >= skill.TriggerParam.BasicAttackThreshold && skill.TriggerParam.BasicAttackThreshold > 0)
			{
				return skill;
			}
		}

		return null;
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
			TriggerSlot = entry.TriggerSlot ?? "active_0",
			TriggerKind = ParseTriggerKind(entry.TriggerKind),
			EffectId = entry.EffectId ?? string.Empty,
			TriggerParam = new SkillTriggerParam
			{
				BasicAttackThreshold = entry.TriggerParam?.BasicAttackThreshold ?? 3,
				CooldownOverride = entry.TriggerParam?.CooldownOverride ?? 0f,
			},
		};

		if (entry.MoveTags != null)
		{
			foreach (var tag in entry.MoveTags)
			{
				skill.MoveTags.Add(new MoveTag(ParseMoveKind(tag.Kind), tag.Distance));
			}
		}

		if (entry.AppliedEffects != null)
		{
			foreach (var applied in entry.AppliedEffects)
			{
				skill.AppliedEffects.Add(new AppliedEffectEntry
				{
					EffectId = applied.EffectId ?? string.Empty,
					Pile = applied.Pile,
					Intensity = applied.Intensity,
					Target = applied.Target ?? "primary",
				});
			}
		}

		if (entry.DescriptionTokens != null)
		{
			skill.DescriptionTokens.AddRange(entry.DescriptionTokens);
		}

		return skill;
	}

	public static SkillDisplayEntry? GetSkillDisplayEntry(string classId, string skillId)
	{
		EnsureLoaded();
		if (!_cache!.TryGetValue(classId, out var entry) || entry.Actives == null)
		{
			return null;
		}

		var skill = entry.Actives.FirstOrDefault(s => s.Id == skillId);
		if (skill == null)
		{
			return null;
		}

		return new SkillDisplayEntry
		{
			Id = skill.Id,
			DisplayName = skill.DisplayName ?? skill.Id,
			DescriptionTokens = skill.DescriptionTokens ?? new List<string>(),
			AppliedEffects = skill.AppliedEffects ?? new List<AppliedEffectJson>(),
		};
	}

	private static SkillTriggerKind ParseTriggerKind(string? kind) =>
		Enum.TryParse<SkillTriggerKind>(kind, true, out var parsed)
			? parsed
			: SkillTriggerKind.GaugeFull;

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
		public string? DisplayName { get; set; }
		public float SkillMultiplier { get; set; } = 1f;
		public string? TriggerSlot { get; set; }
		public string? TriggerKind { get; set; }
		public TriggerParamEntry? TriggerParam { get; set; }
		public string? EffectId { get; set; }
		public List<MoveTagEntry>? MoveTags { get; set; }
		public List<AppliedEffectJson>? AppliedEffects { get; set; }
		public List<string>? DescriptionTokens { get; set; }
	}

	public sealed class AppliedEffectJson
	{
		public string? EffectId { get; set; }
		public int Pile { get; set; } = 1;
		public float Intensity { get; set; }
		public string? Target { get; set; }
	}

	public sealed class SkillDisplayEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public List<string> DescriptionTokens { get; set; } = new();
		public List<AppliedEffectJson> AppliedEffects { get; set; } = new();
	}

	private sealed class TriggerParamEntry
	{
		public int BasicAttackThreshold { get; set; } = 3;
		public float CooldownOverride { get; set; }
	}

	private sealed class PassiveEntry
	{
		public string Id { get; set; } = string.Empty;
		public string TriggerType { get; set; } = string.Empty;
		public int TargetSlot { get; set; } = -1;
		public float SkillMultiplier { get; set; } = 1f;
		public string? EffectId { get; set; }
	}

	private sealed class MoveTagEntry
	{
		public string Kind { get; set; } = string.Empty;
		public int Distance { get; set; } = 40;
	}
}
