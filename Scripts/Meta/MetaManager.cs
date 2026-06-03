using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;

namespace RougeliteIdle.Meta;

public partial class MetaManager : Node
{
	private const string StarChartTreePath = "res://data/tables/meta/star_chart_tree.json";

	private readonly Dictionary<string, StarChartNodeEntry> _starNodes = new();
	private readonly HashSet<string> _purchasedStarNodes = new();
	private EventBus _eventBus = null!;

	public int StarChartPoints { get; private set; }
	public float GlobalStatBonusPercent { get; private set; }

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		LoadStarChartTree();
	}

	public void AddStarChartPoints(int amount)
	{
		StarChartPoints += amount;
	}

	public void Restore(int points, float bonusPercent)
	{
		StarChartPoints = points;
		GlobalStatBonusPercent = bonusPercent;
	}

	public List<string> GetPurchasedStarNodeIds() => _purchasedStarNodes.ToList();

	public void RestoreStarNodes(IEnumerable<string> nodeIds)
	{
		_purchasedStarNodes.Clear();
		foreach (var id in nodeIds)
		{
			if (_starNodes.ContainsKey(id))
			{
				_purchasedStarNodes.Add(id);
			}
		}

		RecalculateStarBonus();
	}

	public bool TryUnlockNode(string nodeId, int cost, float statBonusPercent)
	{
		if (StarChartPoints < cost)
		{
			return false;
		}

		StarChartPoints -= cost;
		GlobalStatBonusPercent += statBonusPercent;
		return true;
	}

	public bool TryPurchaseStarNode(string nodeId)
	{
		if (!_starNodes.TryGetValue(nodeId, out var node) || _purchasedStarNodes.Contains(nodeId))
		{
			return false;
		}

		if (StarChartPoints < node.CostStarPoints)
		{
			return false;
		}

		foreach (var prereq in node.Prerequisites)
		{
			if (!_purchasedStarNodes.Contains(prereq))
			{
				return false;
			}
		}

		StarChartPoints -= node.CostStarPoints;
		_purchasedStarNodes.Add(nodeId);
		RecalculateStarBonus();
		_eventBus.EmitStarChartChanged();
		return true;
	}

	public Godot.Collections.Array GetStarChartSnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var pair in _starNodes)
		{
			var node = pair.Value;
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", node.Id },
				{ "display_name", node.DisplayName },
				{ "cost_star_points", node.CostStarPoints },
				{ "purchased", _purchasedStarNodes.Contains(node.Id) },
				{ "can_purchase", CanPurchaseStar(node) },
			});
		}

		return result;
	}

	private bool CanPurchaseStar(StarChartNodeEntry node)
	{
		if (_purchasedStarNodes.Contains(node.Id) || StarChartPoints < node.CostStarPoints)
		{
			return false;
		}

		return node.Prerequisites.All(_purchasedStarNodes.Contains);
	}

	private void RecalculateStarBonus()
	{
		GlobalStatBonusPercent = 0f;
		foreach (var nodeId in _purchasedStarNodes)
		{
			if (_starNodes.TryGetValue(nodeId, out var node))
			{
				GlobalStatBonusPercent += node.StatBonusPercent / 100f;
			}
		}
	}

	private void LoadStarChartTree()
	{
		_starNodes.Clear();
		if (!FileAccess.FileExists(StarChartTreePath))
		{
			return;
		}

		using var file = FileAccess.Open(StarChartTreePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<StarChartRoot>(file.GetAsText(), options);
		if (root?.Nodes == null)
		{
			return;
		}

		foreach (var node in root.Nodes)
		{
			_starNodes[node.Id] = node;
		}
	}

	private sealed class StarChartRoot
	{
		public List<StarChartNodeEntry>? Nodes { get; set; }
	}

	private sealed class StarChartNodeEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public int CostStarPoints { get; set; }
		public List<string> Prerequisites { get; set; } = new();
		public float StatBonusPercent { get; set; }
	}
}
