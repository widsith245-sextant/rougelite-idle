using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Meta;
using RougeliteIdle.Save;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Run;

public enum RunSessionState
{
	Idle,
	InRoom,
	RoomCleared,
	RunComplete,
	RunFailed,
}

public partial class RunSessionManager : Node
{
	private const string RoomPoolPath = "res://data/tables/run/run_room_pool.json";

	private readonly List<RunRoomDefinition> _roomQueue = new();
	private RunRoomPoolTable _pool = new();
	private EventBus _eventBus = null!;
	private CombatManager _combat = null!;
	private ProgressionManager _progression = null!;
	private RosterProgressionManager _rosterProgression = null!;
	private RunCardManager _runCards = null!;
	private RunRelicManager _runRelics = null!;
	private RunStatsAggregator _runStats = null!;
	private RandomNumberGenerator _rng = new();

	private int _currentRoomIndex;
	private RunSessionState _state = RunSessionState.Idle;
	private int _roomsCleared;
	private bool _awaitingRoomAction;
	private string _lastStartFailureReason = string.Empty;

	public RunSessionState State => _state;
	public int CurrentRoomIndex => _currentRoomIndex;
	public int RoomTotal => _roomQueue.Count;
	public bool IsActive => _state != RunSessionState.Idle
		&& _state != RunSessionState.RunComplete
		&& _state != RunSessionState.RunFailed;

	public string GetLastStartFailureReason() => _lastStartFailureReason;

	public float GetEnemyStatMultiplier() =>
		_pool.EnemyStatMultiplier > 0f ? _pool.EnemyStatMultiplier : 12f;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		_progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		_rosterProgression = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
		_runCards = GetNode<RunCardManager>("/root/RunCardManager");
		_runRelics = GetNode<RunRelicManager>("/root/RunRelicManager");
		_runStats = GetNode<RunStatsAggregator>("/root/RunStatsAggregator");
		_rng.Randomize();
		LoadPool();
		_eventBus.StageRunCompleted += OnStageRunCompleted;
	}

	public override void _ExitTree()
	{
		if (_eventBus != null)
		{
			_eventBus.StageRunCompleted -= OnStageRunCompleted;
		}
	}

	public Godot.Collections.Dictionary GetSnapshot()
	{
		var current = GetCurrentRoom();
		return new Godot.Collections.Dictionary
		{
			{ "state", _state.ToString() },
			{ "room_index", _currentRoomIndex },
			{ "room_total", _roomQueue.Count },
			{ "rooms_cleared", _roomsCleared },
			{ "room_type", current?.Type ?? string.Empty },
			{ "room_id", current?.Id ?? string.Empty },
			{ "awaiting_action", _awaitingRoomAction },
		};
	}

	public Godot.Collections.Array GetRoomQueueSnapshot()
	{
		var arr = new Godot.Collections.Array();
		for (var i = 0; i < _roomQueue.Count; i++)
		{
			var room = _roomQueue[i];
			arr.Add(new Godot.Collections.Dictionary
			{
				{ "index", i },
				{ "type", room.Type },
				{ "id", room.Id },
				{ "current", i == _currentRoomIndex },
			});
		}

		return arr;
	}

	public Godot.Collections.Array GetActiveCardsSnapshot() => _runCards.GetActiveCardsSnapshot();

	public Godot.Collections.Array GetActiveRelicsSnapshot() => _runRelics.GetActiveRelicsSnapshot();

	public bool StartRun(int? seed = null) => TryStartRun(seed);

	public bool TryStartRun(int? seed = null)
	{
		_lastStartFailureReason = string.Empty;

		if (_state == RunSessionState.InRoom)
		{
			_lastStartFailureReason = "已有进行中的 Run";
			return false;
		}

		if (_state != RunSessionState.Idle
			&& _state != RunSessionState.RunComplete
			&& _state != RunSessionState.RunFailed
			&& _state != RunSessionState.RoomCleared)
		{
			_lastStartFailureReason = "当前状态无法开始 Run";
			return false;
		}

		ResetToIdleIfTerminal();

		var debug = DebugSettingsLoader.Get();
		if (!debug.SkipWonderlandTicket && _progression.WonderlandTickets < 1)
		{
			_lastStartFailureReason = "奇境门票不足";
			_eventBus.EmitCombatBroadcast("奇境门票不足", "run");
			return false;
		}

		if (!debug.SkipWonderlandTicket)
		{
			_progression.TrySpendWonderlandTicket();
			GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap")?.RequestSave();
		}

		if (seed.HasValue)
		{
			_rng.Seed = (ulong)seed.Value;
		}

		_roomQueue.Clear();
		BuildRoomQueue();
		if (_roomQueue.Count == 0)
		{
			_lastStartFailureReason = "房间队列生成失败";
			_eventBus.EmitCombatBroadcast("房间队列生成失败", "run");
			return false;
		}

		_currentRoomIndex = 0;
		_roomsCleared = 0;
		_awaitingRoomAction = false;
		_state = RunSessionState.InRoom;
		_combat.RunRogueliteActive = true;
		_combat.SetRunPaused(false);
		_runCards.ResetForRun();
		_runRelics.ResetForRun();
		_runStats.ResetForRun();
		RebuildPartyStatsForRun();
		_eventBus.EmitRunVisualModeChanged("wonderland");
		InterruptTrainingForRun();
		EmitState();
		EnterCurrentRoom();
		_eventBus.EmitCombatBroadcast($"奇境 Run 开始 · {_roomQueue.Count} 房间", "run");
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log("Run", GameLogger.LogLevel.Info,
			$"StartRun rooms={_roomQueue.Count}");
		return true;
	}

	private void ResetToIdleIfTerminal()
	{
		if (_state != RunSessionState.RunComplete
			&& _state != RunSessionState.RunFailed
			&& _state != RunSessionState.RoomCleared)
		{
			return;
		}

		_roomQueue.Clear();
		_currentRoomIndex = 0;
		_roomsCleared = 0;
		_awaitingRoomAction = false;
		_state = RunSessionState.Idle;
		_combat.RunRogueliteActive = false;
		EmitState();
	}

	public void EnterCurrentRoom()
	{
		var room = GetCurrentRoom();
		if (room == null)
		{
			SettleRun(true);
			return;
		}

		_awaitingRoomAction = false;
		_state = RunSessionState.InRoom;
		EmitState();

		switch (room.Type)
		{
			case "combat":
				_combat.SetRunPaused(false);
				if (!string.IsNullOrEmpty(room.StageId))
				{
					var stageId = ResolveCombatStageId(room.StageId);
					room.StageId = stageId;
					_combat.SetStageId(stageId);
				}

				break;
			case "rest":
				_combat.SetRunPaused(true);
				_combat.ClearEngagementForRunPause();
				_awaitingRoomAction = true;
				break;
			case "reward":
				_combat.SetRunPaused(true);
				_combat.ClearEngagementForRunPause();
				_runRelics.BeginRoomRewardPick();
				break;
		}
	}

	public void ApplyRestHeal()
	{
		var room = GetCurrentRoom();
		if (_state != RunSessionState.InRoom || room == null || room.Type != "rest" || !_awaitingRoomAction)
		{
			return;
		}

		var percent = room.HealPercent > 0f ? room.HealPercent : 30f;
		HealActiveParty(percent / 100f);
		_eventBus.EmitCombatBroadcast($"休息恢复 {percent:F0}% HP", "run");
		_awaitingRoomAction = false;
		CompleteRoom();
	}

	public void CompleteRoomAfterCardReward()
	{
		if (_state != RunSessionState.InRoom)
		{
			return;
		}

		var room = GetCurrentRoom();
		if (room == null || room.Type != "reward")
		{
			return;
		}

		CompleteRoom();
	}

	public void CompleteRoomAfterRelicReward()
	{
		if (_state != RunSessionState.InRoom)
		{
			return;
		}

		var room = GetCurrentRoom();
		if (room == null || room.Type != "reward")
		{
			return;
		}

		CompleteRoom();
	}

	public void CompleteRoom()
	{
		if (_state != RunSessionState.InRoom)
		{
			return;
		}

		_roomsCleared++;
		_currentRoomIndex++;
		_state = RunSessionState.RoomCleared;
		EmitState();

		if (_currentRoomIndex >= _roomQueue.Count)
		{
			SettleRun(true);
			return;
		}

		_state = RunSessionState.InRoom;
		EnterCurrentRoom();
	}

	public void FailRun()
	{
		if (_state == RunSessionState.Idle || _state == RunSessionState.RunFailed)
		{
			return;
		}

		SettleRun(false);
	}

	public void AbandonRun()
	{
		if (_state == RunSessionState.Idle)
		{
			return;
		}

		SettleRun(false);
	}

	private void OnStageRunCompleted(string stageId)
	{
		if (!_combat.RunRogueliteActive || _state != RunSessionState.InRoom)
		{
			return;
		}

		var room = GetCurrentRoom();
		if (room == null || room.Type != "combat")
		{
			return;
		}

		if (_combat.GetCurrentStageId() != stageId)
		{
			return;
		}

		CompleteRoom();
	}

	private void SettleRun(bool success)
	{
		var settlement = _runStats.Evaluate(_roomsCleared, success);
		var goldGrant = _runStats.GetGoldGrant(settlement);
		var expGrant = _runStats.GetExpGrant(settlement);

		_combat.RunRogueliteActive = false;
		_combat.SetRunPaused(false);
		_runCards.Clear();
		_runRelics.Clear();
		_awaitingRoomAction = false;
		_state = success ? RunSessionState.RunComplete : RunSessionState.RunFailed;
		_eventBus.EmitRunVisualModeChanged("training");
		RebuildPartyStatsForRun();

		_progression.AddGold(goldGrant);
		if (expGrant > 0f)
		{
			_rosterProgression.GrantExpToActiveSquad(expGrant);
		}

		if (success)
		{
			_eventBus.EmitCombatBroadcast(
				$"Run 通关 · 评级 {settlement.Grade} · +{goldGrant} 金币 · +{expGrant:F0} 经验",
				"run");
		}
		else
		{
			_eventBus.EmitCombatBroadcast(
				$"Run 失败 · 评级 {settlement.Grade} · +{goldGrant} 金币",
				"run");
		}

		GetNodeOrNull<RunSettlementWindowManager>("/root/RunSettlementWindowManager")
			?.ShowSettlement(settlement, goldGrant, expGrant, settlement.ChestQuality);
		_eventBus.EmitRunSettlementReady();

		GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap")?.RequestSave();
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log("Run", GameLogger.LogLevel.Info,
			$"SettleRun success={success} grade={settlement.Grade} rooms={_roomsCleared} gold={goldGrant}");
		EmitState();
		_roomQueue.Clear();
		_currentRoomIndex = 0;
	}

	private void RebuildPartyStatsForRun()
	{
		var stats = GetNodeOrNull<Stats.StatsService>("/root/StatsService");
		if (stats == null)
		{
			return;
		}

		stats.RebuildAll(_combat.Allies);
		foreach (var ally in _combat.Allies)
		{
			ally.Stats = stats.GetOrCreate(ally);
			ally.MaxHp = ally.Stats.GetFinal(StatId.MaxHp);
			ally.CurrentHp = Mathf.Min(ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitStatsChanged(ally.Id);
		}
	}

	private void HealActiveParty(float ratio)
	{
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

	private RunRoomDefinition? GetCurrentRoom() =>
		_currentRoomIndex >= 0 && _currentRoomIndex < _roomQueue.Count
			? _roomQueue[_currentRoomIndex]
			: null;

	private void EmitState()
	{
		var room = GetCurrentRoom();
		_eventBus.EmitRunSessionChanged(
			_state.ToString(),
			_currentRoomIndex,
			_roomQueue.Count,
			room?.Type ?? string.Empty);
	}

	private void LoadPool()
	{
		if (!FileAccess.FileExists(RoomPoolPath))
		{
			GD.PushWarning($"Run room pool not found: {RoomPoolPath}");
			return;
		}

		using var file = FileAccess.Open(RoomPoolPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_pool = JsonSerializer.Deserialize<RunRoomPoolTable>(file.GetAsText(), options) ?? new RunRoomPoolTable();
	}

	private void BuildRoomQueue()
	{
		var length = _rng.RandiRange(_pool.RunLength.Min, _pool.RunLength.Max);
		var stageProg = GetNodeOrNull<StageProgressionManager>("/root/StageProgressionManager");
		var combatTemplates = _pool.Templates
			.Where(t => t.Type == "combat")
			.Where(t => string.IsNullOrEmpty(t.StageId) || stageProg == null || stageProg.IsUnlocked(t.StageId))
			.ToList();
		var specialTemplates = _pool.Templates.Where(t => t.Type != "combat").ToList();

		if (combatTemplates.Count == 0 && stageProg != null)
		{
			combatTemplates.Add(new RunRoomDefinition
			{
				Id = "combat_fallback",
				Type = "combat",
				StageId = stageProg.GetHighestUnlockedStageId(),
				Weight = 1,
			});
		}

		for (var i = 0; i < length - 1; i++)
		{
			if (specialTemplates.Count > 0 && i > 0 && i % 3 == 2)
			{
				_roomQueue.Add(PickWeighted(specialTemplates));
			}
			else if (combatTemplates.Count > 0)
			{
				_roomQueue.Add(PickWeighted(combatTemplates));
			}
		}

		if (_pool.BossRoom != null)
		{
			var boss = _pool.BossRoom;
			if (stageProg != null
				&& !string.IsNullOrEmpty(boss.StageId)
				&& !stageProg.IsUnlocked(boss.StageId))
			{
				GD.PushWarning($"Boss stage {boss.StageId} locked; using highest unlocked stage.");
				_eventBus.EmitCombatBroadcast("Boss 关未解锁，已降级", "run");
				boss = new RunRoomDefinition
				{
					Id = boss.Id,
					Type = boss.Type,
					StageId = stageProg.GetHighestUnlockedStageId(),
					Weight = boss.Weight,
				};
			}

			_roomQueue.Add(boss);
		}
		else if (combatTemplates.Count > 0)
		{
			_roomQueue.Add(PickWeighted(combatTemplates));
		}
	}

	private string ResolveCombatStageId(string requestedStageId)
	{
		if (string.IsNullOrEmpty(requestedStageId))
		{
			return requestedStageId;
		}

		var stageProg = GetNodeOrNull<StageProgressionManager>("/root/StageProgressionManager");
		if (stageProg == null || stageProg.IsUnlocked(requestedStageId))
		{
			return requestedStageId;
		}

		var fallback = stageProg.GetHighestUnlockedStageId();
		GD.PushWarning($"Run room stage {requestedStageId} locked; using {fallback}");
		_eventBus.EmitCombatBroadcast($"奇境战斗关降级为 {fallback}", "run");
		return fallback;
	}

	private void InterruptTrainingForRun()
	{
		var stageId = ResolveFirstCombatStageId();
		if (!string.IsNullOrEmpty(stageId))
		{
			_combat.SetStageId(stageId);
			return;
		}

		_combat.ClearEngagementForRunPause();
	}

	private string ResolveFirstCombatStageId()
	{
		foreach (var room in _roomQueue)
		{
			if (room.Type != "combat" || string.IsNullOrEmpty(room.StageId))
			{
				continue;
			}

			return ResolveCombatStageId(room.StageId);
		}

		return string.Empty;
	}

	private RunRoomDefinition PickWeighted(IReadOnlyList<RunRoomDefinition> candidates)
	{
		var total = candidates.Sum(c => Math.Max(1, c.Weight));
		var roll = _rng.RandiRange(1, total);
		var acc = 0;
		foreach (var candidate in candidates)
		{
			acc += Math.Max(1, candidate.Weight);
			if (roll <= acc)
			{
				return candidate;
			}
		}

		return candidates[0];
	}

	private sealed class RunRoomPoolTable
	{
		public List<RunRoomDefinition> Templates { get; set; } = new();
		public RunRoomDefinition? BossRoom { get; set; }
		public RunLengthRange RunLength { get; set; } = new();
		public float EnemyStatMultiplier { get; set; } = 12f;
	}

	private sealed class RunLengthRange
	{
		public int Min { get; set; } = 5;
		public int Max { get; set; } = 8;
	}

	private sealed class RunRoomDefinition
	{
		public string Id { get; set; } = string.Empty;
		public string Type { get; set; } = "combat";
		public string StageId { get; set; } = string.Empty;
		public int Weight { get; set; } = 1;
		public float HealPercent { get; set; } = 30f;
		public int Choices { get; set; } = 3;
		public int GoldBonus { get; set; } = 25;
	}
}
