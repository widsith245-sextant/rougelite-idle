using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;

namespace RougeliteIdle.Meta;

public partial class PartyManager : Node
{
	private const string RosterPath = "res://data/tables/character/roster.json";

	private readonly Dictionary<string, RosterEntry> _roster = new();
	private readonly string[] _activeRosterIds = { "vanguard_a", "", "" };
	private static readonly string[] SlotUnitIds = { "ally_a", "ally_b", "ally_c" };

	private EventBus _eventBus = null!;
	private DbManager? _db;

	public int MaxActiveSlots { get; private set; } = 1;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_db = GetNodeOrNull<DbManager>("/root/DbManager");
		LoadRoster();
		RefreshMaxActiveSlots();
		if (_db != null)
		{
			_db.DbNodeUnlocked += OnDbNodeUnlocked;
		}
	}

	public string GetUnitIdForSlot(int slotIndex) =>
		slotIndex >= 0 && slotIndex < SlotUnitIds.Length ? SlotUnitIds[slotIndex] : string.Empty;

	public string GetRosterIdForSlot(int slotIndex) =>
		slotIndex >= 0 && slotIndex < _activeRosterIds.Length ? _activeRosterIds[slotIndex] : string.Empty;

	public bool IsSlotUnlocked(int slotIndex) => slotIndex >= 0 && slotIndex < MaxActiveSlots;

	public bool IsRosterUnlocked(string rosterId)
	{
		if (!_roster.TryGetValue(rosterId, out var entry))
		{
			return false;
		}

		if (string.IsNullOrEmpty(entry.UnlockDbNodeId))
		{
			return true;
		}

		return _db?.IsNodePurchased(entry.UnlockDbNodeId) ?? false;
	}

	public string GetClassIdForSlot(int slotIndex)
	{
		var rosterId = GetRosterIdForSlot(slotIndex);
		return GetClassIdForRoster(rosterId);
	}

	public string GetClassIdForRoster(string rosterId) =>
		_roster.TryGetValue(rosterId, out var entry) ? entry.ClassId : string.Empty;

	public string GetClassIdForUnit(string unitId)
	{
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			if (SlotUnitIds[i] == unitId)
			{
				return GetClassIdForSlot(i);
			}
		}

		return string.Empty;
	}

	public string GetDisplayNameForSlot(int slotIndex)
	{
		var rosterId = GetRosterIdForSlot(slotIndex);
		if (string.IsNullOrEmpty(rosterId))
		{
			return slotIndex < SlotUnitIds.Length ? SlotUnitIds[slotIndex] : string.Empty;
		}

		return _roster.TryGetValue(rosterId, out var entry) ? entry.DisplayName : rosterId;
	}

	public string GetDisplayNameForUnit(string unitId)
	{
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			if (SlotUnitIds[i] == unitId)
			{
				return GetDisplayNameForSlot(i);
			}
		}

		return unitId;
	}

	public string GetRosterIdForUnit(string unitId)
	{
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			if (SlotUnitIds[i] == unitId)
			{
				return GetRosterIdForSlot(i);
			}
		}

		return string.Empty;
	}

	public void SwapSquadSlots(int slotA, int slotB)
	{
		if (slotA < 0 || slotB < 0 || slotA >= _activeRosterIds.Length || slotB >= _activeRosterIds.Length)
		{
			return;
		}

		if (!IsSlotUnlocked(slotA) || !IsSlotUnlocked(slotB))
		{
			return;
		}

		(_activeRosterIds[slotA], _activeRosterIds[slotB]) = (_activeRosterIds[slotB], _activeRosterIds[slotA]);
		_eventBus.EmitSquadSwapped(slotA, slotB);
		ApplySquadToCombat();
	}

	public void SetSquadSlot(int slotIndex, string rosterId)
	{
		if (slotIndex < 0 || slotIndex >= _activeRosterIds.Length || !_roster.ContainsKey(rosterId))
		{
			return;
		}

		if (!IsSlotUnlocked(slotIndex) || !IsRosterUnlocked(rosterId))
		{
			return;
		}

		for (var i = 0; i < _activeRosterIds.Length; i++)
		{
			if (i != slotIndex && _activeRosterIds[i] == rosterId)
			{
				_activeRosterIds[i] = string.Empty;
			}
		}

		_activeRosterIds[slotIndex] = rosterId;
	}

	public void CycleSquadSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _activeRosterIds.Length || _roster.Count == 0)
		{
			return;
		}

		if (!IsSlotUnlocked(slotIndex))
		{
			return;
		}

		var unlocked = _roster.Keys.Where(IsRosterUnlocked).ToList();
		if (unlocked.Count == 0)
		{
			return;
		}

		var current = GetRosterIdForSlot(slotIndex);
		var idx = unlocked.IndexOf(current);
		var next = idx < 0 ? 0 : (idx + 1) % unlocked.Count;
		SetSquadSlot(slotIndex, unlocked[next]);
	}

	public void AssignRosterToSlot(int slotIndex, string rosterId) => SetSquadSlot(slotIndex, rosterId);

	public List<string> ExportActiveRosterIds()
	{
		var result = new List<string>(_activeRosterIds.Length);
		result.AddRange(_activeRosterIds);
		return result;
	}

	public void RestoreActiveRosterIds(List<string> rosterIds)
	{
		for (var i = 0; i < _activeRosterIds.Length; i++)
		{
			_activeRosterIds[i] = i < rosterIds.Count ? rosterIds[i] ?? string.Empty : string.Empty;
		}

		RefreshMaxActiveSlots();
	}

	public void ApplySquadToCombat()
	{
		_eventBus.EmitSquadChanged();
	}

	public Godot.Collections.Array GetActiveUnitIdsSnapshot()
	{
		var result = new Godot.Collections.Array();
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			if (!IsSlotUnlocked(i) || string.IsNullOrEmpty(GetRosterIdForSlot(i)))
			{
				continue;
			}

			result.Add(SlotUnitIds[i]);
		}

		return result;
	}

	public Godot.Collections.Array GetRosterSnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var pair in _roster)
		{
			result.Add(new Godot.Collections.Dictionary
			{
				{ "roster_id", pair.Key },
				{ "class_id", pair.Value.ClassId },
				{ "display_name", pair.Value.DisplayName },
				{ "unlocked", IsRosterUnlocked(pair.Key) },
				{ "unlock_db_node_id", pair.Value.UnlockDbNodeId ?? string.Empty },
			});
		}

		return result;
	}

	public Godot.Collections.Array GetActiveSquadSnapshot()
	{
		var result = new Godot.Collections.Array();
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			result.Add(new Godot.Collections.Dictionary
			{
				{ "slot_index", i },
				{ "unit_id", SlotUnitIds[i] },
				{ "roster_id", GetRosterIdForSlot(i) },
				{ "class_id", GetClassIdForSlot(i) },
				{ "display_name", GetDisplayNameForSlot(i) },
				{ "slot_unlocked", IsSlotUnlocked(i) },
				{ "filled", !string.IsNullOrEmpty(GetRosterIdForSlot(i)) },
			});
		}

		return result;
	}

	public Godot.Collections.Array GetBenchSnapshot()
	{
		var active = new HashSet<string>(_activeRosterIds.Where(id => !string.IsNullOrEmpty(id)));
		var result = new Godot.Collections.Array();
		foreach (var pair in _roster)
		{
			var rosterId = pair.Key;
			var state = "locked";
			if (IsRosterUnlocked(rosterId))
			{
				state = active.Contains(rosterId) ? "active" : "bench";
			}

			result.Add(new Godot.Collections.Dictionary
			{
				{ "roster_id", rosterId },
				{ "class_id", pair.Value.ClassId },
				{ "display_name", pair.Value.DisplayName },
				{ "state", state },
			});
		}

		return result;
	}

	public void RefreshMaxActiveSlots()
	{
		var caps = EarlyGameCapsLoader.Get();
		MaxActiveSlots = _db?.MaxActiveSlots ?? caps.DefaultMaxActiveSlots;
		MaxActiveSlots = Mathf.Clamp(MaxActiveSlots, 1, SlotUnitIds.Length);
	}

	/// <summary>
	/// Backpack direct unlock: purchase roster/squad DB nodes or assign roster to slot.
	/// </summary>
	public Godot.Collections.Dictionary TryDirectUnlockForUnit(string unitId)
	{
		var result = new Godot.Collections.Dictionary
		{
			{ "success", false },
			{ "message", "无效角色" },
		};

		var slotIndex = ResolveSlotIndex(unitId);
		if (slotIndex < 0)
		{
			return result;
		}

		var rosterId = RosterIdForUnit(unitId);
		if (string.IsNullOrEmpty(rosterId) || !_roster.ContainsKey(rosterId))
		{
			result["message"] = "未找到角色数据";
			return result;
		}

		if (!IsRosterUnlocked(rosterId))
		{
			var unlockNode = _roster[rosterId].UnlockDbNodeId;
			if (string.IsNullOrEmpty(unlockNode))
			{
				result["message"] = "该角色无需解锁";
			}
			else if (_db != null && _db.TryPurchaseNode(unlockNode))
			{
				result["success"] = true;
				result["message"] = $"已解锁角色：{GetDisplayNameForUnit(unitId)}";
				_eventBus.EmitSquadChanged();
				return result;
			}
			else
			{
				result["message"] = "无法解锁角色（金币或前置不足）";
				return result;
			}
		}

		if (!IsSlotUnlocked(slotIndex))
		{
			var squadNode = NextPurchasableSquadSlotNode();
			if (squadNode != null && _db != null && _db.TryPurchaseNode(squadNode))
			{
				RefreshMaxActiveSlots();
				result["success"] = true;
				result["message"] = "已解锁出战位";
				_eventBus.EmitSquadChanged();
				return result;
			}

			result["message"] = "无法解锁出战位（金币或前置不足）";
			return result;
		}

		if (string.IsNullOrEmpty(GetRosterIdForSlot(slotIndex)))
		{
			SetSquadSlot(slotIndex, rosterId);
			ApplySquadToCombat();
			result["success"] = true;
			result["message"] = $"已编入队伍：{GetDisplayNameForUnit(unitId)}";
			return result;
		}

		result["success"] = true;
		result["message"] = $"{GetDisplayNameForUnit(unitId)} 已解锁并在出战位 {slotIndex + 1}";
		return result;
	}

	private static int ResolveSlotIndex(string unitId)
	{
		for (var i = 0; i < SlotUnitIds.Length; i++)
		{
			if (SlotUnitIds[i] == unitId)
			{
				return i;
			}
		}

		return -1;
	}

	private static string RosterIdForUnit(string unitId) => unitId switch
	{
		"ally_a" => "vanguard_a",
		"ally_b" => "sniper_b",
		"ally_c" => "mage_c",
		_ => string.Empty,
	};

	private string? NextPurchasableSquadSlotNode()
	{
		if (_db == null)
		{
			return null;
		}

		if (_db.CanPurchase("db_squad_2"))
		{
			return "db_squad_2";
		}

		if (_db.CanPurchase("db_squad_3"))
		{
			return "db_squad_3";
		}

		return null;
	}

	private void OnDbNodeUnlocked(string nodeId)
	{
		RefreshMaxActiveSlots();
	}

	private void LoadRoster()
	{
		_roster.Clear();
		if (!FileAccess.FileExists(RosterPath))
		{
			return;
		}

		using var file = FileAccess.Open(RosterPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<RosterRoot>(file.GetAsText(), options);
		if (root?.Roster == null)
		{
			return;
		}

		foreach (var entry in root.Roster)
		{
			_roster[entry.RosterId] = entry;
			if (entry.DefaultSlot >= 0 && entry.DefaultSlot < _activeRosterIds.Length)
			{
				_activeRosterIds[entry.DefaultSlot] = entry.RosterId;
			}
		}
	}

	private sealed class RosterRoot
	{
		public List<RosterEntry>? Roster { get; set; }
	}

	private sealed class RosterEntry
	{
		public string RosterId { get; set; } = string.Empty;
		public string ClassId { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public int DefaultSlot { get; set; } = -1;
		public string? UnlockDbNodeId { get; set; }
	}
}
