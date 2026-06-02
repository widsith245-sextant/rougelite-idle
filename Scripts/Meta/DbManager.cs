using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;

namespace RougeliteIdle.Meta;

public partial class DbManager : Node
{
	private const string DbTreePath = "res://data/tables/meta/db_tree.json";

	private readonly HashSet<string> _purchasedNodes = new();
	private readonly Dictionary<string, DbNodeEntry> _nodes = new();

	private ProgressionManager _progression = null!;
	private PartyManager _party = null!;
	private EventBus _eventBus = null!;

	[Signal]
	public delegate void DbNodeUnlockedEventHandler(string nodeId);

	public int MaxActiveSlots { get; private set; } = 1;
	public int MaxActiveSkillSlots { get; private set; } = 1;
	public bool PassiveSlotsUnlocked { get; private set; }
	public float GlobalMaxHpPercent { get; private set; }
	public float GlobalDamagePercent { get; private set; }

	public override void _Ready()
	{
		_progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		_party = GetNode<PartyManager>("/root/PartyManager");
		_eventBus = GetNode<EventBus>("/root/EventBus");
		LoadTree();
		ApplyDefaultsFromCaps();
		RecalculateEffects();
	}

	public bool IsNodePurchased(string nodeId) => _purchasedNodes.Contains(nodeId);

	public bool CanPurchase(string nodeId)
	{
		if (!_nodes.TryGetValue(nodeId, out var node) || _purchasedNodes.Contains(nodeId))
		{
			return false;
		}

		if (_progression.Gold < node.CostGold)
		{
			return false;
		}

		foreach (var prereq in node.Prerequisites)
		{
			if (!_purchasedNodes.Contains(prereq))
			{
				return false;
			}
		}

		return true;
	}

	public bool TryPurchaseNode(string nodeId)
	{
		if (!CanPurchase(nodeId))
		{
			return false;
		}

		var node = _nodes[nodeId];
		if (!_progression.TrySpendGold(node.CostGold))
		{
			return false;
		}

		_purchasedNodes.Add(nodeId);
		RecalculateEffects();
		_party.RefreshMaxActiveSlots();
		EmitSignal(SignalName.DbNodeUnlocked, nodeId);
		_eventBus.EmitDbNodeUnlocked(nodeId);
		return true;
	}

	public Godot.Collections.Array GetNodeSnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var pair in _nodes)
		{
			var node = pair.Value;
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", node.Id },
				{ "display_name", node.DisplayName },
				{ "cost_gold", node.CostGold },
				{ "purchased", _purchasedNodes.Contains(node.Id) },
				{ "can_purchase", CanPurchase(node.Id) },
				{ "prerequisites", ToVariantArray(node.Prerequisites) },
			});
		}

		return result;
	}

	public void RestorePurchasedNodes(IEnumerable<string> nodeIds)
	{
		_purchasedNodes.Clear();
		foreach (var id in nodeIds)
		{
			if (_nodes.ContainsKey(id))
			{
				_purchasedNodes.Add(id);
			}
		}

		RecalculateEffects();
		_party.RefreshMaxActiveSlots();
	}

	public IReadOnlyCollection<string> GetPurchasedNodeIds() => _purchasedNodes;

	private void ApplyDefaultsFromCaps()
	{
		var caps = EarlyGameCapsLoader.Get();
		MaxActiveSlots = caps.DefaultMaxActiveSlots;
		MaxActiveSkillSlots = caps.DefaultMaxActiveSkillSlots;
		PassiveSlotsUnlocked = caps.PassiveSlotsUnlocked;

		var debug = DebugSettingsLoader.Get();
		if (debug.UnlockPassiveSkillSlots)
		{
			PassiveSlotsUnlocked = true;
		}

		if (debug.MaxActiveSkillSlotsOverride > 0)
		{
			MaxActiveSkillSlots = Math.Max(MaxActiveSkillSlots, debug.MaxActiveSkillSlotsOverride);
		}
	}

	private void RecalculateEffects()
	{
		ApplyDefaultsFromCaps();
		GlobalMaxHpPercent = 0f;
		GlobalDamagePercent = 0f;

		foreach (var nodeId in _purchasedNodes)
		{
			if (!_nodes.TryGetValue(nodeId, out var node) || node.Effects == null)
			{
				continue;
			}

			foreach (var effect in node.Effects)
			{
				ApplyEffect(effect);
			}
		}
	}

	private void ApplyEffect(DbEffectEntry effect)
	{
		switch (effect.Type)
		{
			case "SquadSlot":
				MaxActiveSlots = Math.Max(MaxActiveSlots, effect.Value);
				break;
			case "ActiveSkillSlot":
				MaxActiveSkillSlots = Math.Max(MaxActiveSkillSlots, effect.Value);
				break;
			case "PassiveSlot":
				if (effect.Value > 0)
				{
					PassiveSlotsUnlocked = true;
				}

				break;
			case "GlobalStat":
				if (string.Equals(effect.Stat, "MaxHp", StringComparison.OrdinalIgnoreCase))
				{
					GlobalMaxHpPercent += effect.Percent;
				}
				else if (string.Equals(effect.Stat, "Damage", StringComparison.OrdinalIgnoreCase)
				         || string.Equals(effect.Stat, "BaseAttack", StringComparison.OrdinalIgnoreCase))
				{
					GlobalDamagePercent += effect.Percent;
				}

				break;
			case "RosterUnlock":
				break;
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

	private void LoadTree()
	{
		_nodes.Clear();
		if (!FileAccess.FileExists(DbTreePath))
		{
			return;
		}

		using var file = FileAccess.Open(DbTreePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<DbTreeRoot>(file.GetAsText(), options);
		if (root?.Nodes == null)
		{
			return;
		}

		foreach (var node in root.Nodes)
		{
			_nodes[node.Id] = node;
		}
	}

	private sealed class DbTreeRoot
	{
		public List<DbNodeEntry>? Nodes { get; set; }
	}

	private sealed class DbNodeEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public int CostGold { get; set; }
		public List<string> Prerequisites { get; set; } = new();
		public List<DbEffectEntry>? Effects { get; set; }
	}

	private sealed class DbEffectEntry
	{
		public string Type { get; set; } = string.Empty;
		public int Value { get; set; }
		public string Stat { get; set; } = string.Empty;
		public float Percent { get; set; }
		public string RosterId { get; set; } = string.Empty;
	}
}
