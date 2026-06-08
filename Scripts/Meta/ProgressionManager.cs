using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;
using RougeliteIdle.Run;

namespace RougeliteIdle.Meta;

public partial class ProgressionManager : Node
{
	private const string ProgressionPath = "res://data/tables/progression/player_progression.json";
	private const string CurrenciesPath = "res://data/tables/meta/currencies.json";
	private const string DropTablesPath = "res://data/tables/loot/drop_tables.json";
	private const string RewardDecayPath = "res://data/tables/meta/reward_decay_rules.json";

	private ProgressionTable _progression = new();
	private CurrenciesTable _currencies = new();
	private DropTablesRoot _drops = new();
	private RewardDecayRules _rewardDecay = new();
	private EventBus? _eventBus;

	public int TeamLevel { get; private set; } = 1;
	public float TeamExp { get; private set; }
	public int Gold { get; private set; }
	public int StarChartPoints { get; private set; }
	public int WonderlandTickets { get; private set; }
	public int TrainingBonusChests { get; private set; }

	public override void _Ready()
	{
		_eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
		LoadTables();
		ApplyCurrencyDefaults();
		_rng.Randomize();
	}

	public float GetExpRequiredForNextLevel() =>
		_progression.ExpBase * Mathf.Pow(TeamLevel, _progression.ExpExponent);

	public float GetTeamHpBonusPercent() =>
		(TeamLevel - 1) * _progression.PerLevelMaxHpPercent;

	public float GetTeamDamageBonusPercent() =>
		(TeamLevel - 1) * _progression.PerLevelDamagePercent;

	public void AddExp(float amount)
	{
		if (amount <= 0f)
		{
			return;
		}

		TeamExp += amount;
		while (TeamExp >= GetExpRequiredForNextLevel() && TeamLevel < _progression.MaxTeamLevel)
		{
			TeamExp -= GetExpRequiredForNextLevel();
			TeamLevel++;
		}
	}

	public void RestoreProgression(int gold, int wonderlandTickets, int trainingBonusChests)
	{
		Gold = Mathf.Clamp(gold, 0, _currencies.GoldCap);
		WonderlandTickets = Mathf.Max(0, wonderlandTickets);
		TrainingBonusChests = Mathf.Max(0, trainingBonusChests);
	}

	public void AddGold(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Gold = Math.Min(Gold + amount, _currencies.GoldCap);
	}

	public bool TrySpendGold(int amount)
	{
		if (amount <= 0 || Gold < amount)
		{
			return false;
		}

		Gold -= amount;
		return true;
	}

	public bool TrySpendWonderlandTicket()
	{
		if (WonderlandTickets < 1)
		{
			return false;
		}

		WonderlandTickets--;
		return true;
	}

	public void AddGoldFromSalvage(int itemLevel)
	{
		var perLevel = _currencies.SalvageGoldPerItemLevel > 0
			? _currencies.SalvageGoldPerItemLevel
			: 5;
		var db = GetNodeOrNull<DbManager>("/root/DbManager");
		var salvagePct = db?.SalvagePercent ?? 0f;
		var amount = Mathf.RoundToInt(itemLevel * perLevel * (1f + salvagePct / 100f));
		AddGold(amount);
	}

	public bool TryRollTrainingBonusChest()
	{
		var training = _drops.Training;
		if (training == null)
		{
			return false;
		}

		if (TrainingBonusChests >= training.MaxBonusChests)
		{
			return false;
		}

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		if (rng.Randf() > training.BonusChestChanceAfterIdentify)
		{
			return false;
		}

		TrainingBonusChests++;
		return true;
	}

	public float GetStageClearHealPercent() => Mathf.Clamp(_rewardDecay.HealOnStageClearPercent / 100f, 0f, 1f);

	public float ComputeRewardMultiplier(int teamLevel, int targetLevel)
	{
		targetLevel = Mathf.Max(1, targetLevel);
		var diff = teamLevel - targetLevel;
		if (diff >= _rewardDecay.StartPenaltyDiff)
		{
			var penaltySteps = diff - _rewardDecay.StartPenaltyDiff + 1;
			var mul = 1f - penaltySteps * _rewardDecay.PerLevelPenalty;
			return Mathf.Max(_rewardDecay.MinMultiplier, mul);
		}

		if (diff < 0)
		{
			var bonus = -diff * _rewardDecay.BonusUnderLevel;
			return Mathf.Min(_rewardDecay.MaxBonusMultiplier, 1f + bonus);
		}

		return 1f;
	}

	public void GrantKillReward(string enemyId, int enemyLevel, LootManager? loot)
	{
		if (_drops.KillRewards == null || !_drops.KillRewards.TryGetValue(enemyId, out var reward))
		{
			return;
		}

		var party = GetNodeOrNull<PartyManager>("/root/PartyManager");
		var rosterProg = GetNodeOrNull<RosterProgressionManager>("/root/RosterProgressionManager");
		var avgLevel = rosterProg?.GetAverageActiveRosterLevel(party) ?? 1;
		var mul = ComputeRewardMultiplier(avgLevel, enemyLevel);
		var db = GetNodeOrNull<DbManager>("/root/DbManager");
		var metaExpPct = db?.KillExpPercent ?? 0f;
		var metaGoldPct = db?.KillGoldPercent ?? 0f;
		var metaGoldFlat = db?.KillGoldFlat ?? 0;
		var expGrant = reward.Exp * mul * (1f + metaExpPct / 100f);
		var goldGrant = Mathf.RoundToInt((reward.Gold + metaGoldFlat) * mul * (1f + metaGoldPct / 100f));
		var combat = GetNodeOrNull<CombatManager>("/root/CombatManager");
		if (combat != null && combat.RunRogueliteActive)
		{
			var runCard = GetNodeOrNull<RunCardManager>("/root/RunCardManager");
			var runRelic = GetNodeOrNull<RunRelicManager>("/root/RunRelicManager");
			var bonus = (runCard?.GetRunGoldBonusPercent() ?? 0f) + (runRelic?.GetRunGoldBonusPercent() ?? 0f);
			if (bonus > 0f)
			{
				goldGrant = Mathf.RoundToInt(goldGrant * (1f + bonus / 100f));
			}
		}

		rosterProg?.GrantExpToActiveSquad(expGrant);
		AddGold(goldGrant);

		if (goldGrant > 0 || expGrant > 0.01f)
		{
			_eventBus?.EmitCombatBroadcast($"+{goldGrant} 金币 · +{expGrant:F0} 经验", "reward");
		}

		if (loot == null || reward.Chest == null)
		{
			return;
		}

		_rng.Randomize();
		var chestChance = reward.Chest.Chance + (db?.ChestDropPercent ?? 0f) / 100f;
		if (_rng.Randf() <= chestChance)
		{
			var rewardTier = EnemyTemplateLoader.GetRewardTier(enemyId);
			var chestQuality = ResolveChestQualityWithRewardTier(reward.Chest.Quality, rewardTier);
			loot.AddPendingChest(chestQuality);
			_eventBus?.EmitCombatBroadcast($"获得 {chestQuality} 宝箱", "reward");
		}
	}

	internal static string ResolveChestQualityWithRewardTier(string baseQuality, int rewardTier)
	{
		var ladder = new[] { "common", "rare", "epic" };
		var baseIdx = 0;
		for (var i = 0; i < ladder.Length; i++)
		{
			if (string.Equals(ladder[i], baseQuality, StringComparison.OrdinalIgnoreCase))
			{
				baseIdx = i;
				break;
			}
		}

		var minIdx = rewardTier switch
		{
			>= 5 => 2,
			>= 3 => 1,
			_ => 0,
		};
		return ladder[Mathf.Max(baseIdx, minIdx)];
	}

	public void GrantStageComplete(string stageId, int stageLevel = 1)
	{
		if (_drops.StageComplete == null)
		{
			return;
		}

		if (!_drops.StageComplete.TryGetValue(stageId, out var reward))
		{
			return;
		}

		var party = GetNodeOrNull<PartyManager>("/root/PartyManager");
		var rosterProg = GetNodeOrNull<RosterProgressionManager>("/root/RosterProgressionManager");
		var avgLevel = rosterProg?.GetAverageActiveRosterLevel(party) ?? 1;
		var mul = ComputeRewardMultiplier(avgLevel, stageLevel);
		var band = EarlyGameCapsLoader.Get().EarlyStageBand;
		if (band != null && stageLevel <= band.MaxStageLevel)
		{
			mul *= band.RewardMultiplier;
		}

		var db = GetNodeOrNull<DbManager>("/root/DbManager");
		var stageGoldPct = db?.StageGoldPercent ?? 0f;
		rosterProg?.GrantExpToActiveSquad(reward.Exp * mul);
		AddGold(Mathf.RoundToInt(reward.Gold * mul * (1f + stageGoldPct / 100f)));
	}

	private readonly RandomNumberGenerator _rng = new();

	public Godot.Collections.Dictionary GetHudSnapshot() => new()
	{
		{ "gold", Gold },
		{ "star_chart_points", StarChartPoints },
		{ "wonderland_tickets", WonderlandTickets },
	};

	private void LoadTables()
	{
		_progression = LoadJson<ProgressionTable>(ProgressionPath) ?? new ProgressionTable();
		_currencies = LoadJson<CurrenciesTable>(CurrenciesPath) ?? new CurrenciesTable();
		_drops = LoadJson<DropTablesRoot>(DropTablesPath) ?? new DropTablesRoot();
		_rewardDecay = LoadJson<RewardDecayRules>(RewardDecayPath) ?? new RewardDecayRules();
	}

	private void ApplyCurrencyDefaults()
	{
		Gold = _currencies.InitialGold;
		StarChartPoints = _currencies.InitialStarChartPoints;
		WonderlandTickets = _currencies.InitialWonderlandTickets;
	}

	private static T? LoadJson<T>(string path) where T : class
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"Table not found: {path}");
			return null;
		}

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		return JsonSerializer.Deserialize<T>(file.GetAsText(), options);
	}

	private sealed class ProgressionTable
	{
		public int MaxTeamLevel { get; set; } = 99;
		public float ExpBase { get; set; } = 100f;
		public float ExpExponent { get; set; } = 1.35f;
		public float PerLevelMaxHpPercent { get; set; } = 0.02f;
		public float PerLevelDamagePercent { get; set; } = 0.015f;
	}

	private sealed class CurrenciesTable
	{
		public int InitialGold { get; set; }
		public int GoldCap { get; set; } = 999999;
		public int InitialStarChartPoints { get; set; }
		public int InitialWonderlandTickets { get; set; }
		public int SalvageGoldPerItemLevel { get; set; } = 5;
		public float OfflineStarChartPerHour { get; set; } = 1f;
	}

	private sealed class DropTablesRoot
	{
		public TrainingDropTable? Training { get; set; }
		public Dictionary<string, KillRewardEntry>? KillRewards { get; set; }
		public Dictionary<string, StageRewardEntry>? StageComplete { get; set; }
	}

	private sealed class KillRewardEntry
	{
		public float Exp { get; set; }
		public int Gold { get; set; }
		public ChestRollEntry? Chest { get; set; }
	}

	private sealed class ChestRollEntry
	{
		public float Chance { get; set; }
		public string Quality { get; set; } = "common";
	}

	private sealed class StageRewardEntry
	{
		public float Exp { get; set; }
		public int Gold { get; set; }
	}

	private sealed class TrainingDropTable
	{
		public float BonusChestChanceAfterIdentify { get; set; } = 0.1f;
		public int MaxBonusChests { get; set; } = 5;
	}

	private sealed class RewardDecayRules
	{
		public int StartPenaltyDiff { get; set; } = 3;
		public float PerLevelPenalty { get; set; } = 0.1f;
		public float MinMultiplier { get; set; } = 0.2f;
		public float BonusUnderLevel { get; set; } = 0.05f;
		public float MaxBonusMultiplier { get; set; } = 1.5f;
		public float HealOnStageClearPercent { get; set; } = 100f;
	}
}
