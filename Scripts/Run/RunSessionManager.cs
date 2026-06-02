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

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		_progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		_rosterProgression = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
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

	public bool StartRun(int? seed = null)
	{
		if (_state != RunSessionState.Idle && _state != RunSessionState.RunComplete && _state != RunSessionState.RunFailed)
		{
			return false;
		}

		if (seed.HasValue)
		{
			_rng.Seed = (ulong)seed.Value;
		}

		_roomQueue.Clear();
		BuildRoomQueue();
		_currentRoomIndex = 0;
		_roomsCleared = 0;
		_awaitingRoomAction = false;
		_state = RunSessionState.InRoom;
		_combat.RunRogueliteActive = true;
		EmitState();
		EnterCurrentRoom();
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
				_awaitingRoomAction = true;
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
		_awaitingRoomAction = false;
		CompleteRoom();
	}

	public void ApplyRewardChoice(int choiceIndex)
	{
		var room = GetCurrentRoom();
		if (_state != RunSessionState.InRoom || room == null || room.Type != "reward" || !_awaitingRoomAction)
		{
			return;
		}

		var gold = room.GoldBonus > 0 ? room.GoldBonus : 25;
		if (choiceIndex == 0)
		{
			_progression.AddGold(gold);
		}
		else if (choiceIndex == 1)
		{
			HealActiveParty(0.15f);
		}
		else
		{
			_rosterProgression.GrantExpToActiveSquad(20f);
		}

		_awaitingRoomAction = false;
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
		_awaitingRoomAction = false;
		_state = success ? RunSessionState.RunComplete : RunSessionState.RunFailed;

		var goldBase = _roomsCleared * 15 + (success ? 40 : 0);
		var goldGrant = success ? goldBase : goldBase / 2;
		_progression.AddGold(goldGrant);

		if (success)
		{
			var exp = _roomsCleared * 12f;
			_rosterProgression.GrantExpToActiveSquad(exp);
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
