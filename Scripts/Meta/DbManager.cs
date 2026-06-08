using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;

namespace RougeliteIdle.Meta;

public partial class DbManager : Node
{
	private const string MetaTreePath = "res://data/tables/meta/meta_growth_tree.json";

	private readonly HashSet<string> _purchasedNodes = new();
	private readonly Dictionary<string, DbNodeEntry> _nodes = new();
	private readonly Dictionary<string, float> _globalStatPercents = new(StringComparer.OrdinalIgnoreCase);

	private ProgressionManager _progression = null!;
	private PartyManager _party = null!;
	private EventBus _eventBus = null!;

	[Signal]
	public delegate void DbNodeUnlockedEventHandler(string nodeId);

	public int MaxActiveSlots { get; private set; } = 1;
	public int MaxActiveSkillSlots { get; private set; } = 1;
	public int MaxPassiveSkillSlots { get; private set; } = 2;
	public bool PassiveSlotsUnlocked { get; private set; }
	public float GlobalMaxHpPercent { get; private set; }
	public float GlobalDamagePercent { get; private set; }

	public float KillExpPercent { get; private set; }
	public float OfflineExpPercent { get; private set; }
	public float SalvagePercent { get; private set; }
	public int KillGoldFlat { get; private set; }
	public float KillGoldPercent { get; private set; }
	public float StageGoldPercent { get; private set; }
	public int BagSlotBonus { get; private set; }
	public bool WarehouseUnlocked { get; private set; }
	public int WarehouseCapacity { get; private set; }
	public float ChestDropPercent { get; private set; }
	public int ChestMaxAccumulateBonus { get; private set; }
	public bool AutoOpenChest { get; private set; }
	public float AutoOpenIntervalReductionPercent { get; private set; }

	public int BagCapacity
	{
		get
		{
			var caps = EarlyGameCapsLoader.Get();
			return caps.BagSlotsVisible + BagSlotBonus;
		}
	}

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

		if (node.CostGold > 0 && _progression.Gold < node.CostGold)
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
		if (node.CostGold > 0 && !_progression.TrySpendGold(node.CostGold))
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

	public Godot.Collections.Array GetNodeSnapshot(string? branchFilter = null)
	{
		var result = new Godot.Collections.Array();
		var ordered = _nodes.Values
			.OrderBy(n => BranchOrder(n.Branch))
			.ThenBy(n => n.Tier)
			.ThenBy(n => n.Id, StringComparer.Ordinal);

		foreach (var node in ordered)
		{
			if (branchFilter != null && !string.Equals(node.Branch, branchFilter, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", node.Id },
				{ "branch", node.Branch },
				{ "tier", node.Tier },
				{ "display_name", node.DisplayName },
				{ "cost_gold", node.CostGold },
				{ "purchased", _purchasedNodes.Contains(node.Id) },
				{ "can_purchase", CanPurchase(node.Id) },
				{ "prerequisites", ToVariantArray(node.Prerequisites) },
			});
		}

		return result;
	}

	public float GetGlobalStatPercent(string stat) =>
		_globalStatPercents.GetValueOrDefault(stat, 0f);

	public int GetChestMaxAccumulate(string qualityId)
	{
		var baseMax = ChestQualityLoader.GetMaxAccumulate(qualityId);
		var caps = EarlyGameCapsLoader.Get();
		var initial = caps.DefaultChestMaxAccumulate > 0 ? caps.DefaultChestMaxAccumulate : 6;
		return Math.Max(baseMax, initial) + ChestMaxAccumulateBonus;
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

	public void GrantStarterNodes()
	{
		foreach (var pair in _nodes)
		{
			if (pair.Value.CostGold <= 0)
			{
				_purchasedNodes.Add(pair.Key);
			}
		}

		RecalculateEffects();
		_party.RefreshMaxActiveSlots();
	}

	private static int BranchOrder(string branch) => branch switch
	{
		"root" => 0,
		"squad" => 1,
		"exp" => 2,
		"gold" => 3,
		"bag" => 4,
		"chest" => 5,
		"stats" => 6,
		_ => 99,
	};

	private void ApplyDefaultsFromCaps()
	{
		var caps = EarlyGameCapsLoader.Get();
		MaxActiveSlots = caps.DefaultMaxActiveSlots;
		MaxActiveSkillSlots = caps.DefaultMaxActiveSkillSlots;
		MaxPassiveSkillSlots = caps.DefaultMaxPassiveSkillSlots;
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
		_globalStatPercents.Clear();
		KillExpPercent = 0f;
		OfflineExpPercent = 0f;
		SalvagePercent = 0f;
		KillGoldFlat = 0;
		KillGoldPercent = 0f;
		StageGoldPercent = 0f;
		BagSlotBonus = 0;
		WarehouseUnlocked = false;
		WarehouseCapacity = 0;
		ChestDropPercent = 0f;
		ChestMaxAccumulateBonus = 0;
		AutoOpenChest = false;
		AutoOpenIntervalReductionPercent = 0f;

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

		GlobalMaxHpPercent = GetGlobalStatPercent("MaxHp");
		GlobalDamagePercent = GetGlobalStatPercent("Damage") + GetGlobalStatPercent("BaseAttack");
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
					MaxPassiveSkillSlots = Math.Max(MaxPassiveSkillSlots, effect.Value);
				}

				break;
			case "GlobalStat":
				AddGlobalStat(effect.Stat, effect.Percent);
				break;
			case "GlobalStatAll":
				foreach (var stat in new[] { "MaxHp", "Damage", "Defense", "AtkSpeed", "CritRate", "Dodge" })
				{
					AddGlobalStat(stat, effect.Percent);
				}

				break;
			case "KillExpPercent":
				KillExpPercent += effect.Percent;
				break;
			case "OfflineExpPercent":
				OfflineExpPercent += effect.Percent;
				break;
			case "SalvagePercent":
				SalvagePercent += effect.Percent;
				break;
			case "KillGoldFlat":
				KillGoldFlat += effect.Value;
				break;
			case "KillGoldPercent":
				KillGoldPercent += effect.Percent;
				break;
			case "StageGoldPercent":
				StageGoldPercent += effect.Percent;
				break;
			case "BagSlot":
				BagSlotBonus += effect.Value;
				break;
			case "WarehouseUnlock":
				if (effect.Value > 0)
				{
					WarehouseUnlocked = true;
				}

				break;
			case "WarehouseCapacity":
				WarehouseCapacity = Math.Max(WarehouseCapacity, effect.Value);
				break;
			case "ChestDropPercent":
				ChestDropPercent += effect.Percent;
				break;
			case "ChestMaxAccumulate":
				ChestMaxAccumulateBonus += effect.Value;
				break;
			case "AutoOpenChest":
				if (effect.Value > 0)
				{
					AutoOpenChest = true;
				}

				break;
			case "AutoOpenInterval":
				AutoOpenIntervalReductionPercent += effect.Percent;
				break;
			case "RosterUnlock":
				break;
		}
	}

	private void AddGlobalStat(string stat, float percent)
	{
		if (string.IsNullOrEmpty(stat) || percent == 0f)
		{
			return;
		}

		_globalStatPercents[stat] = _globalStatPercents.GetValueOrDefault(stat) + percent;
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
		if (!FileAccess.FileExists(MetaTreePath))
		{
			return;
		}

		using var file = FileAccess.Open(MetaTreePath, FileAccess.ModeFlags.Read);
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
		public string Branch { get; set; } = string.Empty;
		public int Tier { get; set; }
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
