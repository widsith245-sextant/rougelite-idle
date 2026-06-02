using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Combat;

public static class EnemyTemplateLoader
{
	private const string Path = "res://data/tables/combat/enemy_templates.json";
	private static Dictionary<string, TemplateEntry>? _cache;

	public static CombatUnitData? CreateEnemy(string templateId, float spawnOffsetX = 0f, int instanceIndex = 0, int stageLevel = 1, float statMultiplier = 1f)
	{
		EnsureLoaded();
		if (_cache == null || !_cache.TryGetValue(templateId, out var entry))
		{
			var tree = Engine.GetMainLoop() as SceneTree;
			tree?.Root.GetNodeOrNull<GameLogger>("/root/GameLogger")?.LogCombatWarn($"Enemy template not found: {templateId}");
			return null;
		}

		var instanceId = instanceIndex <= 0 ? templateId : $"{templateId}_{instanceIndex}";
		var levelMul = Mathf.Clamp(stageLevel, 1, 10);
		var totalMul = levelMul * Mathf.Max(1f, statMultiplier);
		var scaledHp = entry.MaxHp * totalMul;
		var scaledAtk = entry.BaseAttack * totalMul;
		var unit = new CombatUnitData
		{
			Id = instanceId,
			TemplateId = entry.Id,
			DisplayName = entry.DisplayName,
			Archetype = entry.Archetype ?? "trash",
			DamageType = entry.DamageProfile?.Type ?? "physical",
			RewardTier = entry.RewardTier > 0 ? entry.RewardTier : 1,
			IsAlly = false,
			Level = entry.Level,
			MaxHp = scaledHp,
			CurrentHp = scaledHp,
			Speed = entry.Speed,
			BaseAttack = scaledAtk,
			HitBoxRadius = entry.HitBoxRadius > 0f ? entry.HitBoxRadius : 12f,
			EnemyTemplateAtkSpeed = entry.AtkSpeed > 0f ? entry.AtkSpeed : 1.2f,
			EnemyTemplateAtkRange = entry.AtkRange > 0f ? entry.AtkRange : 20f,
			EnemyTemplateMoveSpeed = entry.MoveSpeed > 0f ? entry.MoveSpeed : entry.Speed * 20f,
			PositionX = BattlefieldController.EnemyAnchorX + spawnOffsetX,
			ActiveSkill = MapSkill(entry.ActiveSkill, entry.DamageProfile?.SkillMultiplierScale ?? 1f),
		};

		if (entry.OnHitEffects != null)
		{
			unit.OnHitEffects.AddRange(entry.OnHitEffects);
		}

		return unit;
	}

	public static int GetEnemyLevel(string templateId)
	{
		EnsureLoaded();
		if (_cache == null || !_cache.TryGetValue(templateId, out var entry))
		{
			return 1;
		}

		return Mathf.Max(1, entry.Level);
	}

	public static int GetRewardTier(string templateId)
	{
		EnsureLoaded();
		if (_cache == null || !_cache.TryGetValue(templateId, out var entry))
		{
			return 1;
		}

		return entry.RewardTier > 0 ? entry.RewardTier : 1;
	}

	public static string ResolveTemplateId(string unitOrTemplateId)
	{
		var idx = unitOrTemplateId.LastIndexOf('_');
		if (idx > 0 && int.TryParse(unitOrTemplateId[(idx + 1)..], out _))
		{
			return unitOrTemplateId[..idx];
		}

		return unitOrTemplateId;
	}

	private static CombatSkillDefinition? MapSkill(SkillEntry? entry, float scale)
	{
		if (entry == null)
		{
			return null;
		}

		var skill = new CombatSkillDefinition
		{
			Id = entry.Id,
			SkillMultiplier = entry.SkillMultiplier * scale,
		};

		if (entry.MoveTags != null)
		{
			foreach (var tag in entry.MoveTags)
			{
				skill.MoveTags.Add(new MoveTag(
					System.Enum.TryParse<MoveTagKind>(tag.Kind, true, out var kind) ? kind : MoveTagKind.Charge,
					tag.Distance));
			}
		}

		return skill;
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, TemplateEntry>();
		if (!FileAccess.FileExists(Path))
		{
			return;
		}

		using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<TemplateRoot>(file.GetAsText(), options);
		if (root?.Enemies == null)
		{
			return;
		}

		foreach (var entry in root.Enemies)
		{
			_cache[entry.Id] = entry;
		}
	}

	private sealed class TemplateRoot
	{
		public List<TemplateEntry>? Enemies { get; set; }
	}

	private sealed class TemplateEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string? Archetype { get; set; }
		public List<string>? Tags { get; set; }
		public int Level { get; set; } = 1;
		public float MaxHp { get; set; } = 200f;
		public float Speed { get; set; } = 6f;
		public float BaseAttack { get; set; } = 6f;
		public float AtkSpeed { get; set; } = 1.2f;
		public float AtkRange { get; set; } = 20f;
		public float MoveSpeed { get; set; } = 120f;
		public float HitBoxRadius { get; set; } = 12f;
		public int RewardTier { get; set; } = 1;
		public DamageProfileRef? DamageProfile { get; set; }
		public List<OnHitEffectRoll>? OnHitEffects { get; set; }
		public List<object>? OnDeathEffects { get; set; }
		public SkillEntry? ActiveSkill { get; set; }
	}

	private sealed class DamageProfileRef
	{
		public string Type { get; set; } = "physical";
		public float SkillMultiplierScale { get; set; } = 1f;
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
		public int Distance { get; set; }
	}
}
