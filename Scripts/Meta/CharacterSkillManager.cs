using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Meta;

public partial class CharacterSkillManager : Node
{
	private const string TreesPath = "res://data/tables/character/character_skill_trees.json";

	private readonly Dictionary<string, List<SkillTreeNode>> _trees = new();
	private readonly Dictionary<string, HashSet<string>> _unlockedNodes = new();
	private readonly Dictionary<string, Dictionary<string, string>> _equippedByRoster = new();
	private string _lastEquipError = string.Empty;

	private DbManager? _db;
	private EventBus _eventBus = null!;
	private RosterProgressionManager? _rosterProgress;

	public override void _Ready()
	{
		_db = GetNodeOrNull<DbManager>("/root/DbManager");
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_rosterProgress = GetNodeOrNull<RosterProgressionManager>("/root/RosterProgressionManager");
		LoadTrees();
		InitializeDefaults();
	}

	public bool TryUnlockNode(string rosterId, string nodeId)
	{
		if (!_trees.TryGetValue(rosterId, out var nodes))
		{
			return false;
		}

		var node = nodes.FirstOrDefault(n => n.Id == nodeId);
		if (node == null || IsNodeUnlocked(rosterId, nodeId) || !CanUnlock(rosterId, node))
		{
			return false;
		}

		EnsureRosterState(rosterId);
		_unlockedNodes[rosterId].Add(nodeId);
		_eventBus.EmitSkillsChanged();
		return true;
	}

	public bool TryEquipSkill(string rosterId, string nodeId, string slotKey)
	{
		_lastEquipError = string.Empty;
		if (!_trees.TryGetValue(rosterId, out var nodes))
		{
			_lastEquipError = "未找到技能树";
			return false;
		}

		var node = nodes.FirstOrDefault(n => n.Id == nodeId);
		if (node == null || !IsNodeUnlocked(rosterId, nodeId))
		{
			_lastEquipError = "节点未解锁";
			return false;
		}

		if (node.NodeType == "passive")
		{
			if (!slotKey.StartsWith("passive_", StringComparison.Ordinal))
			{
				_lastEquipError = "被动技能只能装入被动槽";
				return false;
			}

			var passIdx = int.Parse(slotKey["passive_".Length..]);
			var maxPassive = _db?.MaxPassiveSkillSlots ?? 2;
			if (passIdx >= maxPassive)
			{
				_lastEquipError = "被动槽未解锁";
				return false;
			}

			if (!(_db?.PassiveSlotsUnlocked ?? false))
			{
				_lastEquipError = "被动槽未解锁";
				return false;
			}
		}
		else if (node.NodeType == "active")
		{
			if (!slotKey.StartsWith("active_", StringComparison.Ordinal))
			{
				_lastEquipError = "主动技能只能装入主动槽";
				return false;
			}

			var maxSlots = _db?.MaxActiveSkillSlots ?? 1;
			var idx = int.Parse(slotKey["active_".Length..]);
			if (idx >= maxSlots)
			{
				_lastEquipError = "主动槽未解锁";
				return false;
			}
		}

		EnsureRosterState(rosterId);
		_equippedByRoster[rosterId][slotKey] = node.SkillId;
		_eventBus.EmitSkillsChanged();
		return true;
	}

	public string GetLastEquipError() => _lastEquipError;

	public bool TrySwapEquipped(string rosterId, string slotKeyA, string slotKeyB)
	{
		EnsureRosterState(rosterId);
		var equipped = _equippedByRoster[rosterId];
		equipped.TryGetValue(slotKeyA, out var skillA);
		equipped.TryGetValue(slotKeyB, out var skillB);
		if (string.IsNullOrEmpty(skillA) && string.IsNullOrEmpty(skillB))
		{
			_lastEquipError = "空槽无法交换";
			return false;
		}

		if (string.IsNullOrEmpty(skillB))
		{
			equipped.Remove(slotKeyA);
		}
		else
		{
			equipped[slotKeyA] = skillB;
		}

		if (string.IsNullOrEmpty(skillA))
		{
			equipped.Remove(slotKeyB);
		}
		else
		{
			equipped[slotKeyB] = skillA;
		}

		_eventBus.EmitSkillsChanged();
		return true;
	}

	public string? FindNodeIdForSkill(string rosterId, string skillId)
	{
		if (!_trees.TryGetValue(rosterId, out var nodes) || string.IsNullOrEmpty(skillId))
		{
			return null;
		}

		foreach (var node in nodes)
		{
			if (node.SkillId == skillId)
			{
				return node.Id;
			}
		}

		return null;
	}

	public bool IsNodeUnlocked(string rosterId, string nodeId) =>
		_unlockedNodes.TryGetValue(rosterId, out var set) && set.Contains(nodeId);

	public Godot.Collections.Array GetTreeSnapshot(string rosterId)
	{
		var result = new Godot.Collections.Array();
		if (!_trees.TryGetValue(rosterId, out var nodes))
		{
			return result;
		}

		var rosterLevel = _rosterProgress?.GetLevel(rosterId) ?? 1;
		foreach (var node in nodes)
		{
			var levelOk = rosterLevel >= node.RequiredLevel;
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", node.Id },
				{ "display_name", node.DisplayName },
				{ "node_type", node.NodeType },
				{ "skill_id", node.SkillId },
				{ "required_level", node.RequiredLevel },
				{ "level_ok", levelOk },
				{ "unlocked", IsNodeUnlocked(rosterId, node.Id) },
				{ "can_unlock", CanUnlock(rosterId, node) },
				{ "prerequisites", ToVariantArray(node.Prerequisites) },
			});
		}

		return result;
	}

	public Godot.Collections.Dictionary GetEquippedSnapshot(string rosterId)
	{
		EnsureRosterState(rosterId);
		var dict = new Godot.Collections.Dictionary();
		foreach (var pair in _equippedByRoster[rosterId])
		{
			dict[pair.Key] = pair.Value;
		}

		return dict;
	}

	public Godot.Collections.Dictionary GetSkillDisplaySnapshot(string rosterId, string nodeId)
	{
		var result = new Godot.Collections.Dictionary();
		if (!_trees.TryGetValue(rosterId, out var nodes))
		{
			return result;
		}

		var node = nodes.FirstOrDefault(n => n.Id == nodeId);
		if (node == null)
		{
			return result;
		}

		var party = GetNodeOrNull<PartyManager>("/root/PartyManager");
		var classId = party?.GetClassIdForRoster(rosterId) ?? string.Empty;
		var display = ClassSkillsLoader.GetSkillDisplayEntry(classId, node.SkillId);
		var tokens = display?.DescriptionTokens ?? new List<string>();
		var effectsArr = new Godot.Collections.Array();
		if (display?.AppliedEffects != null)
		{
			foreach (var ef in display.AppliedEffects)
			{
				effectsArr.Add(new Godot.Collections.Dictionary
				{
					{ "effect_id", ef.EffectId ?? string.Empty },
					{ "pile", ef.Pile },
					{ "intensity", ef.Intensity },
					{ "target", ef.Target ?? "enemy" },
				});
			}
		}

		result["display_name"] = node.DisplayName;
		result["node_type"] = node.NodeType;
		result["skill_id"] = node.SkillId;
		result["description_tokens"] = ToVariantArray(tokens);
		result["applied_effects"] = effectsArr;
		return result;
	}

	public void ApplySkillsToUnit(CombatUnitData unit, string rosterId)
	{
		EnsureRosterState(rosterId);
		PruneEquippedSlots(rosterId);
		if (!_equippedByRoster.TryGetValue(rosterId, out var equipped))
		{
			return;
		}

		unit.ActiveSkills.Clear();
		unit.Passives.Clear();
		var maxSlots = _db?.MaxActiveSkillSlots ?? 1;
		for (var i = 0; i < maxSlots; i++)
		{
			if (!equipped.TryGetValue($"active_{i}", out var activeId) || string.IsNullOrEmpty(activeId))
			{
				continue;
			}

			var skill = ClassSkillsLoader.BuildActiveSkill(unit.ClassId, activeId);
			if (skill != null)
			{
				unit.ActiveSkills.Add(skill);
			}
		}

		unit.ActiveSkill = ClassSkillsLoader.GetGaugeSkill(unit.ActiveSkills) ?? (unit.ActiveSkills.Count > 0 ? unit.ActiveSkills[0] : null);

		if (_db?.PassiveSlotsUnlocked == true)
		{
			var maxPassive = _db.MaxPassiveSkillSlots;
			for (var p = 0; p < maxPassive; p++)
			{
				if (!equipped.TryGetValue($"passive_{p}", out var passiveId) || string.IsNullOrEmpty(passiveId))
				{
					continue;
				}

				var passive = ClassSkillsLoader.BuildPassive(unit.ClassId, passiveId);
				if (passive != null)
				{
					unit.Passives.Add(passive);
				}
			}
		}
	}

	public Dictionary<string, List<string>> ExportUnlockedNodes()
	{
		var result = new Dictionary<string, List<string>>();
		foreach (var pair in _unlockedNodes)
		{
			result[pair.Key] = pair.Value.ToList();
		}

		return result;
	}

	public Dictionary<string, Dictionary<string, string>> ExportEquippedSkills()
	{
		var result = new Dictionary<string, Dictionary<string, string>>();
		foreach (var pair in _equippedByRoster)
		{
			result[pair.Key] = new Dictionary<string, string>(pair.Value);
		}

		return result;
	}

	public void RestoreState(Dictionary<string, List<string>> unlocked, Dictionary<string, Dictionary<string, string>> equipped)
	{
		_unlockedNodes.Clear();
		_equippedByRoster.Clear();
		foreach (var pair in unlocked)
		{
			_unlockedNodes[pair.Key] = new HashSet<string>(pair.Value);
		}

		foreach (var pair in equipped)
		{
			_equippedByRoster[pair.Key] = new Dictionary<string, string>(pair.Value);
			PruneEquippedSlots(pair.Key);
		}

		if (_unlockedNodes.Count == 0)
		{
			InitializeDefaults();
		}
	}

	private static Godot.Collections.Array ToVariantArray(IEnumerable<string> items)
	{
		var arr = new Godot.Collections.Array();
		foreach (var item in items)
		{
			arr.Add(item);
		}

		return arr;
	}

	private bool CanUnlock(string rosterId, SkillTreeNode node)
	{
		if (IsNodeUnlocked(rosterId, node.Id))
		{
			return false;
		}

		if (!node.Prerequisites.All(p => IsNodeUnlocked(rosterId, p)))
		{
			return false;
		}

		var rosterLevel = _rosterProgress?.GetLevel(rosterId) ?? 1;
		return rosterLevel >= node.RequiredLevel;
	}

	private void InitializeDefaults()
	{
		foreach (var rosterId in _trees.Keys)
		{
			EnsureRosterState(rosterId);
			if (_unlockedNodes[rosterId].Count > 0)
			{
				continue;
			}

			var firstActive = _trees[rosterId].FirstOrDefault(n => n.NodeType == "active");
			if (firstActive != null)
			{
				_unlockedNodes[rosterId].Add(firstActive.Id);
				_equippedByRoster[rosterId]["active_0"] = firstActive.SkillId;
			}
		}
	}

	private void EnsureRosterState(string rosterId)
	{
		if (!_unlockedNodes.ContainsKey(rosterId))
		{
			_unlockedNodes[rosterId] = new HashSet<string>();
		}

		if (!_equippedByRoster.ContainsKey(rosterId))
		{
			_equippedByRoster[rosterId] = new Dictionary<string, string>();
		}
	}

	private void PruneEquippedSlots(string rosterId)
	{
		EnsureRosterState(rosterId);
		var maxActive = _db?.MaxActiveSkillSlots ?? 2;
		var maxPassive = _db?.MaxPassiveSkillSlots ?? 1;
		var equipped = _equippedByRoster[rosterId];
		var toRemove = new List<string>();
		foreach (var key in equipped.Keys)
		{
			if (key.StartsWith("active_", StringComparison.Ordinal)
				&& int.TryParse(key["active_".Length..], out var activeIndex)
				&& activeIndex >= maxActive)
			{
				toRemove.Add(key);
			}

			if (key.StartsWith("passive_", StringComparison.Ordinal)
				&& int.TryParse(key["passive_".Length..], out var passiveIndex)
				&& passiveIndex >= maxPassive)
			{
				toRemove.Add(key);
			}
		}

		foreach (var key in toRemove)
		{
			equipped.Remove(key);
		}
	}

	private void LoadTrees()
	{
		_trees.Clear();
		if (!FileAccess.FileExists(TreesPath))
		{
			return;
		}

		using var file = FileAccess.Open(TreesPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<SkillTreesRoot>(file.GetAsText(), options);
		if (root?.Trees == null)
		{
			return;
		}

		foreach (var tree in root.Trees)
		{
			_trees[tree.RosterId] = tree.Nodes ?? new List<SkillTreeNode>();
		}
	}

	private sealed class SkillTreesRoot
	{
		public List<SkillTreeEntry>? Trees { get; set; }
	}

	private sealed class SkillTreeEntry
	{
		public string RosterId { get; set; } = string.Empty;
		public List<SkillTreeNode>? Nodes { get; set; }
	}

	private sealed class SkillTreeNode
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string NodeType { get; set; } = "active";
		public string SkillId { get; set; } = string.Empty;
		public List<string> Prerequisites { get; set; } = new();
		public int RequiredLevel { get; set; } = 1;
		public int Cost { get; set; }
	}
}
