using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Meta;
using RougeliteIdle.Save;

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
	private RandomNumberGenerator _rng = new();

	private int _currentRoomIndex;
	private RunSessionState _state = RunSessionState.Idle;
	private int _roomsCleared;
	private bool _awaitingRoomAction;

	public RunSessionState State => _state;
	public int CurrentRoomIndex => _currentRoomIndex;
	public int RoomTotal => _roomQueue.Count;
	public bool IsActive => _state != RunSessionState.Idle
		&& _state != RunSessionState.RunComplete
		&& _state != RunSessionState.RunFailed;

	public float GetEnemyStatMultiplier() =>
		_pool.EnemyStatMultiplier > 0f ? _pool.EnemyStatMultiplier : 12f;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		_progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		_rosterProgression = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
		_runCards = GetNode<RunCardManager>("/root/RunCardManager");
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

	public bool StartRun(int? seed = null)
	{
		if (_state != RunSessionState.Idle
			&& _state != RunSessionState.RunComplete
			&& _state != RunSessionState.RunFailed
			&& _state != RunSessionState.RoomCleared)
		{
			return false;
		}

		var debug = DebugSettingsLoader.Get();
		if (!debug.SkipWonderlandTicket && _progression.WonderlandTickets < 1)
		{
			_eventBus.EmitCombatBroadcast("奇境门票不足", "run");
			return false;
		}

		if (!debug.SkipWonderlandTicket)
		{
			_progression.TrySpendWonderlandTicket();
		}

		if (seed.HasValue)
		{
			_rng.Seed = (ulong)seed.Value;
		}

		_roomQueue.Clear();
		BuildRoomQueue();
		if (_roomQueue.Count == 0)
		{
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
		_eventBus.EmitRunVisualModeChanged("wonderland");
		EmitState();
		EnterCurrentRoom();
		_eventBus.EmitCombatBroadcast($"奇境 Run 开始 · {_roomQueue.Count} 房间", "run");
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log("Run", GameLogger.LogLevel.Info,
			$"StartRun rooms={_roomQueue.Count}");
		return true;
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
				if (!string.IsNullOrEmpty(room.StageId))
				{
					_combat.SetStageId(room.StageId);
				}

				break;
			case "rest":
				_awaitingRoomAction = true;
				break;
			case "reward":
				_runCards.BeginRoomRewardPick();
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
		if (room == null || room.Type != "combat" || room.StageId != stageId)
		{
			return;
		}

		CompleteRoom();
	}

	private void SettleRun(bool success)
	{
		_combat.RunRogueliteActive = false;
		_combat.SetRunPaused(false);
		_runCards.Clear();
		_awaitingRoomAction = false;
		_state = success ? RunSessionState.RunComplete : RunSessionState.RunFailed;
		_eventBus.EmitRunVisualModeChanged("training");

		var goldBase = _roomsCleared * 15 + (success ? 40 : 0);
		var goldGrant = success ? goldBase : goldBase / 2;
		_progression.AddGold(goldGrant);

		if (success)
		{
			var exp = _roomsCleared * 12f;
			_rosterProgression.GrantExpToActiveSquad(exp);
			_eventBus.EmitCombatBroadcast($"Run 通关 · 结算 +{goldGrant} 金币 · +{exp:F0} 经验", "run");
		}
		else
		{
			_eventBus.EmitCombatBroadcast($"Run 失败 · 结算 +{goldGrant} 金币", "run");
		}

		GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap")?.RequestSave();
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log("Run", GameLogger.LogLevel.Info,
			$"SettleRun success={success} rooms={_roomsCleared} gold={goldGrant}");
		EmitState();
		_roomQueue.Clear();
		_currentRoomIndex = 0;
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
		var combatTemplates = _pool.Templates.Where(t => t.Type == "combat").ToList();
		var specialTemplates = _pool.Templates.Where(t => t.Type != "combat").ToList();

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
			_roomQueue.Add(_pool.BossRoom);
		}
		else if (combatTemplates.Count > 0)
		{
			_roomQueue.Add(PickWeighted(combatTemplates));
		}
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
