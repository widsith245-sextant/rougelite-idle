using System.Collections.Generic;
using System.Linq;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
#if DEBUG
using RougeliteIdle.Tools;
#endif

namespace RougeliteIdle.Meta;

public partial class StageProgressionManager : Node
{
	private readonly HashSet<string> _unlocked = new();
	private readonly HashSet<string> _cleared = new();
	private EventBus _eventBus = null!;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
#if DEBUG
		EarlyGameAudit.LogEarlyStageSummary();
#endif
	}

	public bool IsUnlocked(string stageId) =>
		!string.IsNullOrEmpty(stageId) && _unlocked.Contains(stageId);

	public bool IsCleared(string stageId) =>
		!string.IsNullOrEmpty(stageId) && _cleared.Contains(stageId);

	public string GetHighestUnlockedStageId()
	{
		var ordered = StageTableLoader.GetOrderedCatalogEntries();
		for (var i = ordered.Count - 1; i >= 0; i--)
		{
			if (_unlocked.Contains(ordered[i].StageId))
			{
				return ordered[i].StageId;
			}
		}

		return ordered.Count > 0 ? ordered[0].StageId : "chapter_01_level_01";
	}

	public void InitializeNewGame()
	{
		_unlocked.Clear();
		_cleared.Clear();
		foreach (var stageId in GetInitialUnlockedStages())
		{
			_unlocked.Add(stageId);
		}
	}

	public void Restore(List<string>? unlocked, List<string>? cleared, int saveVersion, string? migrationStageId)
	{
		_unlocked.Clear();
		_cleared.Clear();

		var hasLists = unlocked is { Count: > 0 } || cleared is { Count: > 0 };
		if (hasLists || saveVersion >= 3)
		{
			foreach (var stageId in unlocked ?? new List<string>())
			{
				if (!string.IsNullOrEmpty(stageId))
				{
					_unlocked.Add(stageId);
				}
			}

			foreach (var stageId in cleared ?? new List<string>())
			{
				if (string.IsNullOrEmpty(stageId))
				{
					continue;
				}

				_cleared.Add(stageId);
				_unlocked.Add(stageId);
				var nextStageId = StageTableLoader.GetNextStageId(stageId);
				if (!string.IsNullOrEmpty(nextStageId))
				{
					_unlocked.Add(nextStageId);
				}
			}

			EnsureInitialUnlockedPresent();
			return;
		}

		MigrateFromLegacyStage(migrationStageId);
	}

	public void MarkStageCleared(string stageId)
	{
		if (string.IsNullOrEmpty(stageId) || _cleared.Contains(stageId))
		{
			return;
		}

		_cleared.Add(stageId);
		_unlocked.Add(stageId);
		_eventBus.EmitStageCleared(stageId);

		var nextStageId = StageTableLoader.GetNextStageId(stageId);
		if (string.IsNullOrEmpty(nextStageId) || _unlocked.Contains(nextStageId))
		{
			return;
		}

		_unlocked.Add(nextStageId);
		_eventBus.EmitStageUnlocked(nextStageId);
	}

	public List<string> ExportUnlockedStageIds() => _unlocked.OrderBy(id => id).ToList();

	public List<string> ExportClearedStageIds() => _cleared.OrderBy(id => id).ToList();

	public Godot.Collections.Array GetSnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var entry in StageTableLoader.GetOrderedCatalogEntries())
		{
			var stage = StageTableLoader.LoadByStageId(entry.StageId);
			var reward = stage.OnStageComplete;
			result.Add(new Godot.Collections.Dictionary
			{
				{ "stage_id", entry.StageId },
				{ "recommended_level", entry.RecommendedLevel },
				{ "unlock_from", entry.UnlockFrom },
				{ "unlocked", IsUnlocked(entry.StageId) },
				{ "cleared", IsCleared(entry.StageId) },
				{ "exp", reward?.Exp ?? 0f },
				{ "gold", reward?.Gold ?? 0 },
			});
		}

		return result;
	}

	private static IEnumerable<string> GetInitialUnlockedStages()
	{
		var caps = EarlyGameCapsLoader.Get();
		if (caps.InitialUnlockedStages is { Count: > 0 })
		{
			return caps.InitialUnlockedStages;
		}

		return new[] { "chapter_01_level_01" };
	}

	private void MigrateFromLegacyStage(string? migrationStageId)
	{
		var target = string.IsNullOrEmpty(migrationStageId)
			? "chapter_01_level_01"
			: migrationStageId;

		foreach (var entry in StageTableLoader.GetOrderedCatalogEntries())
		{
			_unlocked.Add(entry.StageId);
			if (entry.StageId == target)
			{
				break;
			}
		}

		EnsureInitialUnlockedPresent();
	}

	private void EnsureInitialUnlockedPresent()
	{
		foreach (var stageId in GetInitialUnlockedStages())
		{
			_unlocked.Add(stageId);
		}

		if (_unlocked.Count == 0)
		{
			_unlocked.Add("chapter_01_level_01");
		}
	}
}
