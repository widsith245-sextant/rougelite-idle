using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Stats;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Run;

public readonly struct RunCardBuffTotals
{
	public float DamagePercent { get; init; }
	public float MaxHpPercent { get; init; }
	public float MoveSpeedPercent { get; init; }
	public float CritPercent { get; init; }
	public float RunGoldBonusPercent { get; init; }
}

public partial class RunCardManager : Node
{
	private const string CardPoolPath = "res://data/tables/run/run_card_pool.json";

	private readonly List<string> _activeCardIds = new();
	private readonly List<string> _pendingOffers = new();

	private RunCardPoolTable _pool = new();
	private EventBus _eventBus = null!;
	private CombatManager _combat = null!;
	private RunSessionManager _runSession = null!;
	private StatsService _stats = null!;
	private RandomNumberGenerator _rng = new();

	private int _wavesClearedInRun;
	private bool _pendingCardPick;
	private bool _roomRewardPick;

	public bool HasActiveBuffs => _activeCardIds.Count > 0;
	public bool IsPendingPick => _pendingCardPick;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		_runSession = GetNode<RunSessionManager>("/root/RunSessionManager");
		_stats = GetNode<StatsService>("/root/StatsService");
		_rng.Randomize();
		LoadPool();
		_eventBus.WaveCleared += OnWaveCleared;
	}

	public override void _ExitTree()
	{
		if (_eventBus != null)
		{
			_eventBus.WaveCleared -= OnWaveCleared;
		}
	}

	public void ResetForRun()
	{
		_activeCardIds.Clear();
		_pendingOffers.Clear();
		_wavesClearedInRun = 0;
		_pendingCardPick = false;
		_roomRewardPick = false;
	}

	public void Clear()
	{
		ResetForRun();
		_combat.SetRunPaused(false);
	}

	public Godot.Collections.Array GetActiveCardsSnapshot()
	{
		var arr = new Godot.Collections.Array();
		foreach (var cardId in _activeCardIds)
		{
			var def = FindCard(cardId);
			if (def == null)
			{
				continue;
			}

			arr.Add(new Godot.Collections.Dictionary
			{
				{ "id", def.Id },
				{ "name", def.Name },
				{ "desc", def.Desc },
			});
		}

		return arr;
	}

	public Godot.Collections.Array GetPendingOffersSnapshot()
	{
		var arr = new Godot.Collections.Array();
		foreach (var cardId in _pendingOffers)
		{
			var def = FindCard(cardId);
			if (def == null)
			{
				continue;
			}

			arr.Add(new Godot.Collections.Dictionary
			{
				{ "id", def.Id },
				{ "name", def.Name },
				{ "desc", def.Desc },
			});
		}

		return arr;
	}

	public RunCardBuffTotals GetAggregatedBuffs()
	{
		var damage = 0f;
		var maxHp = 0f;
		var moveSpeed = 0f;
		var crit = 0f;
		var gold = 0f;
		foreach (var cardId in _activeCardIds)
		{
			var def = FindCard(cardId);
			if (def?.Effect == null)
			{
				continue;
			}

			switch (def.Effect.Type)
			{
				case "TeamDamagePercent":
					damage += def.Effect.Value;
					break;
				case "TeamMaxHpPercent":
					maxHp += def.Effect.Value;
					break;
				case "TeamMoveSpeedPercent":
					moveSpeed += def.Effect.Value;
					break;
				case "TeamCritPercent":
					crit += def.Effect.Value;
					break;
				case "RunGoldBonusPercent":
					gold += def.Effect.Value;
					break;
			}
		}

		return new RunCardBuffTotals
		{
			DamagePercent = damage,
			MaxHpPercent = maxHp,
			MoveSpeedPercent = moveSpeed,
			CritPercent = crit,
			RunGoldBonusPercent = gold,
		};
	}

	public float GetRunGoldBonusPercent() => GetAggregatedBuffs().RunGoldBonusPercent;

	public void BeginRoomRewardPick()
	{
		if (_pendingCardPick)
		{
			return;
		}

		BeginCardPick(isRoomReward: true);
	}

	public void ApplyCard(string cardId)
	{
		if (!_pendingCardPick || string.IsNullOrEmpty(cardId))
		{
			return;
		}

		var def = FindCard(cardId);
		if (def == null)
		{
			return;
		}

		_activeCardIds.Add(cardId);
		ApplyCardEffect(def);
		_stats.RebuildAll(_combat.Allies);
		foreach (var ally in _combat.Allies)
		{
			ally.Stats = _stats.GetOrCreate(ally);
			ally.MaxHp = ally.Stats.GetFinal(StatId.MaxHp);
			ally.CurrentHp = Mathf.Min(ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitStatsChanged(ally.Id);
		}
		_eventBus.EmitCombatBroadcast($"获得卡牌：{def.Name}", "run");

		_pendingCardPick = false;
		_pendingOffers.Clear();
		_combat.SetRunPaused(false);

		if (_roomRewardPick)
		{
			_roomRewardPick = false;
			_runSession.CompleteRoomAfterCardReward();
		}
	}

	private void OnWaveCleared(int waveIndex)
	{
		if (!_combat.RunRogueliteActive || _pendingCardPick)
		{
			return;
		}

		_wavesClearedInRun++;
		var interval = _pool.PickIntervalWaves > 0 ? _pool.PickIntervalWaves : 3;
		if (_wavesClearedInRun % interval != 0)
		{
			return;
		}

		BeginCardPick(isRoomReward: false);
	}

	private void BeginCardPick(bool isRoomReward)
	{
		if (_pool.Cards.Count == 0)
		{
			if (isRoomReward)
			{
				_runSession.CompleteRoomAfterCardReward();
			}

			return;
		}

		_pendingCardPick = true;
		_roomRewardPick = isRoomReward;
		_pendingOffers.Clear();
		_pendingOffers.AddRange(PickRandomCards(_pool.ChoicesPerPick > 0 ? _pool.ChoicesPerPick : 3));
		_combat.SetRunPaused(true);
		_eventBus.EmitRunCardPickOffered();
		OpenCardPickUi();
	}

	private void OpenCardPickUi()
	{
		var tree = GetTree();
		if (tree == null)
		{
			return;
		}

		var popup = tree.Root.GetNodeOrNull<Node>("GameRoot/PopupManager");
		popup?.Call("open_run_card_pick");
	}

	private List<string> PickRandomCards(int count)
	{
		var picks = new List<string>();
		var pool = _pool.Cards.ToList();
		if (pool.Count == 0)
		{
			return picks;
		}

		for (var i = 0; i < count; i++)
		{
			var idx = _rng.RandiRange(0, pool.Count - 1);
			picks.Add(pool[idx].Id);
		}

		return picks;
	}

	private void ApplyCardEffect(RunCardDefinition def)
	{
		if (def.Effect == null)
		{
			return;
		}

		if (def.Effect.Type == "InstantHealPercent" && def.Effect.Value > 0f)
		{
			var ratio = def.Effect.Value / 100f;
			foreach (var ally in _combat.Allies)
			{
				if (ally.CurrentHp <= 0f)
				{
					continue;
				}

				ally.CurrentHp = Mathf.Min(ally.MaxHp, ally.CurrentHp + ally.MaxHp * ratio);
				_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			}
		}
	}

	private RunCardDefinition? FindCard(string cardId) =>
		_pool.Cards.FirstOrDefault(c => c.Id == cardId);

	private void LoadPool()
	{
		if (!FileAccess.FileExists(CardPoolPath))
		{
			GD.PushWarning($"Run card pool not found: {CardPoolPath}");
			return;
		}

		using var file = FileAccess.Open(CardPoolPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_pool = JsonSerializer.Deserialize<RunCardPoolTable>(file.GetAsText(), options) ?? new RunCardPoolTable();
	}

	private sealed class RunCardPoolTable
	{
		public int PickIntervalWaves { get; set; } = 3;
		public int ChoicesPerPick { get; set; } = 3;
		public List<RunCardDefinition> Cards { get; set; } = new();
	}

	private sealed class RunCardDefinition
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Desc { get; set; } = string.Empty;
		public RunCardEffect? Effect { get; set; }
	}

	private sealed class RunCardEffect
	{
		public string Type { get; set; } = string.Empty;
		public float Value { get; set; }
	}
}
