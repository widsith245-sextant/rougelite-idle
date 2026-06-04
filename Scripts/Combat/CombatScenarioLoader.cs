using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Combat;

public static class CombatScenarioLoader
{
	private const string Path = "res://data/tables/debug/combat_scenarios.json";
	private static List<CombatScenarioEntry>? _cache;

	public static Godot.Collections.Array GetScenarioList()
	{
		EnsureLoaded();
		var arr = new Godot.Collections.Array();
		foreach (var scenario in _cache ?? new List<CombatScenarioEntry>())
		{
			arr.Add(new Godot.Collections.Dictionary
			{
				{ "id", scenario.Id },
				{ "label", scenario.Label },
				{ "note", scenario.Note },
			});
		}

		return arr;
	}

	public static void Apply(string scenarioId, CombatManager combat)
	{
		EnsureLoaded();
		var scenario = _cache?.Find(s => s.Id == scenarioId);
		if (scenario == null)
		{
			GD.Print($"[CombatScenario] unknown {scenarioId}");
			return;
		}

		combat.DebugRestartEncounter();
		foreach (var apply in scenario.ApplyEffects ?? new List<ScenarioEffectApply>())
		{
			combat.DebugApplyEffect(apply.TargetId, apply.EffectId, apply.Pile, apply.Intensity);
		}
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new List<CombatScenarioEntry>();
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
		var root = JsonSerializer.Deserialize<CombatScenarioRoot>(file.GetAsText(), options);
		if (root?.Scenarios != null)
		{
			_cache = root.Scenarios;
		}
	}

	private sealed class CombatScenarioRoot
	{
		public List<CombatScenarioEntry>? Scenarios { get; set; }
	}

	public sealed class CombatScenarioEntry
	{
		public string Id { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
		public string Note { get; set; } = string.Empty;
		public List<ScenarioEffectApply>? ApplyEffects { get; set; }
	}

	public sealed class ScenarioEffectApply
	{
		public string TargetId { get; set; } = "enemy_1";
		public string EffectId { get; set; } = string.Empty;
		public int Pile { get; set; } = 1;
		public float Intensity { get; set; }
	}
}
