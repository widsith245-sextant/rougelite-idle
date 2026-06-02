using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Save;

namespace RougeliteIdle.Meta;

public partial class RosterProgressionManager : Node
{
	private const string ProgressionPath = "res://data/tables/progression/player_progression.json";

	private readonly Dictionary<string, RosterProgressState> _progress = new();
	private ProgressionTable _table = new();
	private EventBus _eventBus = null!;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		LoadTable();
	}

	public int GetLevel(string rosterId)
	{
		EnsureRoster(rosterId);
		return _progress[rosterId].Level;
	}

	public float GetExpRequiredForNextLevel(string rosterId)
	{
		EnsureRoster(rosterId);
		return _table.ExpBase * Mathf.Pow(_progress[rosterId].Level, _table.ExpExponent);
	}

	public Godot.Collections.Dictionary GetExpSnapshot(string rosterId)
	{
		EnsureRoster(rosterId);
		var state = _progress[rosterId];
		return new Godot.Collections.Dictionary
		{
			{ "roster_id", rosterId },
			{ "level", state.Level },
			{ "exp", state.Exp },
			{ "exp_required", GetExpRequiredForNextLevel(rosterId) },
			{ "max_level", _table.MaxTeamLevel },
		};
	}

	public void AddExp(string rosterId, float amount)
	{
		if (amount <= 0f || string.IsNullOrEmpty(rosterId))
		{
			return;
		}

		EnsureRoster(rosterId);
		var state = _progress[rosterId];
		state.Exp += amount;
		var leveled = false;
		while (state.Exp >= GetExpRequiredForNextLevel(rosterId) && state.Level < _table.MaxTeamLevel)
		{
			state.Exp -= GetExpRequiredForNextLevel(rosterId);
			state.Level++;
			leveled = true;
		}

		if (leveled)
		{
			_eventBus.EmitRosterLevelChanged(rosterId);
		}
	}

	public void GrantExpToActiveSquad(float totalAmount)
	{
		if (totalAmount <= 0f)
		{
			return;
		}

		var party = GetNodeOrNull<PartyManager>("/root/PartyManager");
		if (party == null)
		{
			return;
		}

		var recipients = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			if (!party.IsSlotUnlocked(i))
			{
				continue;
			}

			var rosterId = party.GetRosterIdForSlot(i);
			if (string.IsNullOrEmpty(rosterId) || !party.IsRosterUnlocked(rosterId))
			{
				continue;
			}

			recipients.Add(rosterId);
		}

		if (recipients.Count == 0)
		{
			return;
		}

		var share = totalAmount / recipients.Count;
		foreach (var rosterId in recipients)
		{
			AddExp(rosterId, share);
		}
	}

	public int GetAverageActiveRosterLevel(PartyManager? party)
	{
		if (party == null)
		{
			return 1;
		}

		var total = 0;
		var count = 0;
		for (var i = 0; i < 3; i++)
		{
			if (!party.IsSlotUnlocked(i))
			{
				continue;
			}

			var rosterId = party.GetRosterIdForSlot(i);
			if (string.IsNullOrEmpty(rosterId))
			{
				continue;
			}

			total += GetLevel(rosterId);
			count++;
		}

		return count > 0 ? Mathf.Max(1, total / count) : 1;
	}

	public Dictionary<string, RosterProgressDto> ExportProgress()
	{
		var result = new Dictionary<string, RosterProgressDto>();
		foreach (var pair in _progress)
		{
			result[pair.Key] = new RosterProgressDto
			{
				Level = pair.Value.Level,
				Exp = pair.Value.Exp,
			};
		}

		return result;
	}

	public void RestoreProgress(Dictionary<string, RosterProgressDto>? saved, int legacyTeamLevel = 1, float legacyTeamExp = 0f)
	{
		_progress.Clear();
		if (saved != null && saved.Count > 0)
		{
			foreach (var pair in saved)
			{
				_progress[pair.Key] = new RosterProgressState
				{
					Level = Mathf.Clamp(pair.Value.Level, 1, _table.MaxTeamLevel),
					Exp = Mathf.Max(0f, pair.Value.Exp),
				};
			}

			return;
		}

		_progress["vanguard_a"] = new RosterProgressState
		{
			Level = Mathf.Clamp(legacyTeamLevel, 1, _table.MaxTeamLevel),
			Exp = Mathf.Max(0f, legacyTeamExp),
		};
	}

	public void EnsureRoster(string rosterId)
	{
		if (string.IsNullOrEmpty(rosterId))
		{
			return;
		}

		if (!_progress.ContainsKey(rosterId))
		{
			_progress[rosterId] = new RosterProgressState { Level = 1, Exp = 0f };
		}
	}

	private void LoadTable()
	{
		if (!FileAccess.FileExists(ProgressionPath))
		{
			return;
		}

		using var file = FileAccess.Open(ProgressionPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_table = JsonSerializer.Deserialize<ProgressionTable>(file.GetAsText(), options) ?? new ProgressionTable();
	}

	private sealed class RosterProgressState
	{
		public int Level { get; set; } = 1;
		public float Exp { get; set; }
	}

	private sealed class ProgressionTable
	{
		public int MaxTeamLevel { get; set; } = 99;
		public float ExpBase { get; set; } = 100f;
		public float ExpExponent { get; set; } = 1.35f;
	}
}
