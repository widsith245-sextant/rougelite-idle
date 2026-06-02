using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Combat;

public static class CombatEffectLoader
{
	private const string Path = "res://data/tables/combat/combat_effects.json";
	private static Dictionary<string, CombatEffectDefinition>? _cache;

	public static CombatEffectDefinition? Get(string effectId)
	{
		EnsureLoaded();
		return _cache != null && _cache.TryGetValue(effectId, out var def) ? def : null;
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, CombatEffectDefinition>();
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
		var root = JsonSerializer.Deserialize<CombatEffectRoot>(file.GetAsText(), options);
		if (root?.Effects == null)
		{
			return;
		}

		foreach (var effect in root.Effects)
		{
			_cache[effect.Id] = effect;
		}
	}

	private sealed class CombatEffectRoot
	{
		public List<CombatEffectDefinition>? Effects { get; set; }
	}
}

public sealed class CombatEffectDefinition
{
	public string Id { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public float TickInterval { get; set; }
	public float Duration { get; set; }
	public int MaxStacks { get; set; } = 1;
	public float DamagePercentOfSource { get; set; }
	public EffectStatModifier? StatModifier { get; set; }
}

public sealed class EffectStatModifier
{
	public string Stat { get; set; } = string.Empty;
	public float Flat { get; set; }
	public float Percent { get; set; }
}

public sealed class ActiveCombatEffect
{
	public string EffectId { get; set; } = string.Empty;
	public string SourceId { get; set; } = string.Empty;
	public float RemainingDuration { get; set; }
	public float TickTimer { get; set; }
	public int Stacks { get; set; } = 1;
	public float SourceAttackSnapshot { get; set; }
}

public sealed class OnHitEffectRoll
{
	public string Id { get; set; } = string.Empty;
	public float Chance { get; set; }
}
