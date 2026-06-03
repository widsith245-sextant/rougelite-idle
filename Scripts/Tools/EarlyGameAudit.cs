#if DEBUG
using Godot;
using RougeliteIdle.Combat;

namespace RougeliteIdle.Tools;

public static class EarlyGameAudit
{
	public static void LogEarlyStageSummary()
	{
		var entries = StageTableLoader.GetOrderedCatalogEntries();
		var count = System.Math.Min(3, entries.Count);
		for (var i = 0; i < count; i++)
		{
			var entry = entries[i];
			var stage = StageTableLoader.LoadByStageId(entry.StageId);
			var waves = stage.Waves?.Count ?? 0;
			var reward = stage.OnStageComplete;
			GD.Print($"[EarlyGameAudit] {entry.StageId}: waves={waves} exp={reward?.Exp ?? 0} gold={reward?.Gold ?? 0} recLv={entry.RecommendedLevel}");
		}
	}
}
#endif
