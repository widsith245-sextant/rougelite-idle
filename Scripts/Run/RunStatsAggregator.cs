using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;

namespace RougeliteIdle.Run;

public readonly struct RunSettlementResult
{
	public string Grade { get; init; }
	public string ChestQuality { get; init; }
	public int RoomsCleared { get; init; }
	public int TotalKills { get; init; }
	public float DamageTaken { get; init; }
	public float LowestHpPercent { get; init; }
	public float ElapsedSeconds { get; init; }
	public int Deaths { get; init; }
	public bool Success { get; init; }
}

public partial class RunStatsAggregator : Node
{
	private const string ScoreRulesPath = "res://data/tables/run/run_score_rules.json";

	private readonly HashSet<string> _countedDeaths = new();

	private EventBus _eventBus = null!;
	private CombatManager _combat = null!;
	private RunScoreRulesTable _rules = new();

	private int _totalKills;
	private float _damageTaken;
	private float _lowestHpPercent = 1f;
	private float _elapsedSeconds;
	private int _deaths;
	private bool _tracking;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		LoadRules();
		_eventBus.DamageDealt += OnDamageDealt;
		_eventBus.UnitHpChanged += OnUnitHpChanged;
	}

	public override void _ExitTree()
	{
		if (_eventBus != null)
		{
			_eventBus.DamageDealt -= OnDamageDealt;
			_eventBus.UnitHpChanged -= OnUnitHpChanged;
		}
	}

	public override void _Process(double delta)
	{
		if (!_tracking || !_combat.RunRogueliteActive)
		{
			return;
		}

		_elapsedSeconds += (float)delta;
	}

	public void ResetForRun()
	{
		_totalKills = 0;
		_damageTaken = 0f;
		_lowestHpPercent = 1f;
		_elapsedSeconds = 0f;
		_deaths = 0;
		_countedDeaths.Clear();
		_tracking = true;
	}

	public void StopTracking()
	{
		_tracking = false;
	}

	public void RecordKill()
	{
		if (!_tracking)
		{
			return;
		}

		_totalKills++;
	}

	public RunSettlementResult Evaluate(int roomsCleared, bool success)
	{
		StopTracking();
		var grade = ResolveGrade(roomsCleared, success);
		var chestQuality = _rules.Grades
			.FirstOrDefault(g => g.Grade == grade)?.ChestQuality ?? "common";

		return new RunSettlementResult
		{
			Grade = grade,
			ChestQuality = chestQuality,
			RoomsCleared = roomsCleared,
			TotalKills = _totalKills,
			DamageTaken = _damageTaken,
			LowestHpPercent = _lowestHpPercent,
			ElapsedSeconds = _elapsedSeconds,
			Deaths = _deaths,
			Success = success,
		};
	}

	public Godot.Collections.Dictionary ResultToDictionary(RunSettlementResult result)
	{
		return new Godot.Collections.Dictionary
		{
			{ "grade", result.Grade },
			{ "chest_quality", result.ChestQuality },
			{ "rooms_cleared", result.RoomsCleared },
			{ "total_kills", result.TotalKills },
			{ "damage_taken", result.DamageTaken },
			{ "lowest_hp_percent", result.LowestHpPercent },
			{ "elapsed_seconds", result.ElapsedSeconds },
			{ "deaths", result.Deaths },
			{ "success", result.Success },
		};
	}

	public int GetGoldGrant(RunSettlementResult result)
	{
		var perRoom = _rules.GoldPerRoom > 0 ? _rules.GoldPerRoom : 15;
		var bonus = _rules.GoldSuccessBonus > 0 ? _rules.GoldSuccessBonus : 40;
		var goldBase = result.RoomsCleared * perRoom + (result.Success ? bonus : 0);
		return result.Success ? goldBase : goldBase / 2;
	}

	public float GetExpGrant(RunSettlementResult result)
	{
		var perRoom = _rules.ExpPerRoom > 0 ? _rules.ExpPerRoom : 12f;
		return result.Success ? result.RoomsCleared * perRoom : 0f;
	}

	private string ResolveGrade(int roomsCleared, bool success)
	{
		foreach (var rule in _rules.Grades.OrderByDescending(g => GradePriority(g.Grade)))
		{
			if (rule.RequiresSuccess && !success)
			{
				continue;
			}

			if (!rule.RequiresSuccess && success)
			{
				continue;
			}

			if (rule.MinRoomsCleared > 0 && roomsCleared < rule.MinRoomsCleared)
			{
				continue;
			}

			if (rule.MaxDeaths >= 0 && _deaths > rule.MaxDeaths)
			{
				continue;
			}

			return rule.Grade;
		}

		return success ? "B" : "C";
	}

	private static int GradePriority(string grade) => grade switch
	{
		"S" => 4,
		"A" => 3,
		"B" => 2,
		_ => 1,
	};

	private void OnDamageDealt(string sourceId, string targetId, float amount, bool isCrit, string damageType, string displayTag)
	{
		if (!_tracking)
		{
			return;
		}

		foreach (var ally in _combat.Allies)
		{
			if (ally.Id == targetId)
			{
				_damageTaken += amount;
				break;
			}
		}
	}

	private void OnUnitHpChanged(string entityId, float currentHp, float maxHp)
	{
		if (!_tracking || maxHp <= 0f)
		{
			return;
		}

		foreach (var ally in _combat.Allies)
		{
			if (ally.Id != entityId)
			{
				continue;
			}

			var ratio = currentHp / maxHp;
			if (ratio < _lowestHpPercent)
			{
				_lowestHpPercent = ratio;
			}

			if (currentHp <= 0f && _countedDeaths.Add(entityId))
			{
				_deaths++;
			}

			break;
		}
	}

	private void LoadRules()
	{
		if (!FileAccess.FileExists(ScoreRulesPath))
		{
			GD.PushWarning($"Run score rules not found: {ScoreRulesPath}");
			return;
		}

		using var file = FileAccess.Open(ScoreRulesPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_rules = JsonSerializer.Deserialize<RunScoreRulesTable>(file.GetAsText(), options) ?? new RunScoreRulesTable();
	}

	private sealed class RunScoreRulesTable
	{
		public int GoldPerRoom { get; set; } = 15;
		public int GoldSuccessBonus { get; set; } = 40;
		public float ExpPerRoom { get; set; } = 12f;
		public List<RunGradeRule> Grades { get; set; } = new();
	}

	private sealed class RunGradeRule
	{
		public string Grade { get; set; } = "C";
		public string ChestQuality { get; set; } = "common";
		public bool RequiresSuccess { get; set; }
		public int MaxDeaths { get; set; } = -1;
		public int MinRoomsCleared { get; set; }
	}
}
