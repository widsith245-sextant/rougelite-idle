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
		if (!_trees.TryGetValue(rosterId, out var nodes))
		{
			return false;
		}

		var node = nodes.FirstOrDefault(n => n.Id == nodeId);
		if (node == null || !IsNodeUnlocked(rosterId, nodeId))
		{
			return false;
		}

		if (node.NodeType == "passive" && !(_db?.PassiveSlotsUnlocked ?? false))
		{
			return false;
		}

		if (node.NodeType == "active")
		{
			var maxSlots = _db?.MaxActiveSkillSlots ?? 1;
			if (slotKey.StartsWith("active_", StringComparison.Ordinal))
			{
				var idx = int.Parse(slotKey["active_".Length..]);
				if (idx >= maxSlots)
				{
					return false;
				}
			}
		}

		EnsureRosterState(rosterId);
		_equippedByRoster[rosterId][slotKey] = node.SkillId;
		_eventBus.EmitSkillsChanged();
		return true;
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

	public void ApplySkillsToUnit(CombatUnitData unit, string rosterId)
	{
		EnsureRosterState(rosterId);
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

		unit.ActiveSkill = unit.ActiveSkills.Count > 0 ? unit.ActiveSkills[0] : null;

		if (_db?.PassiveSlotsUnlocked == true && equipped.TryGetValue("passive_0", out var passiveId))
		{
			var passive = ClassSkillsLoader.BuildPassive(unit.ClassId, passiveId);
			if (passive != null)
			{
				unit.Passives.Add(passive);
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
