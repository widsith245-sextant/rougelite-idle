using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Combat;

public static class StageTableLoader
{
	private const string DefaultPath = "res://data/tables/combat/stage_training.json";
	private const string CatalogPath = "res://data/tables/combat/stage_catalog.json";
	private static StageCatalogRoot? _catalogCache;

	public static StageDefinition LoadDefault()
	{
		var fallback = Load(DefaultPath);
		var first = GetFirstCatalogStagePath();
		return string.IsNullOrEmpty(first) ? fallback : Load(first);
	}

	public static StageDefinition LoadByStageId(string stageId)
	{
		if (string.IsNullOrEmpty(stageId))
		{
			return LoadDefault();
		}

		var path = ResolvePathByStageId(stageId);
		return string.IsNullOrEmpty(path) ? LoadDefault() : Load(path);
	}

	public static StageDefinition Load(string path)
	{
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"Stage table not found: {path}");
			return CreateFallback();
		}

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return CreateFallback();
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		return JsonSerializer.Deserialize<StageDefinition>(file.GetAsText(), options) ?? CreateFallback();
	}

	private static StageDefinition CreateFallback() => new()
	{
		StageId = "chapter_01_level_01",
		StageLevel = 1,
		StageLength = 1200f,
		MarchSpeed = 45f,
		Waves = new List<WaveDefinition>
		{
			new() { Progress = 0.25f, EnemyId = "training_dummy", Count = 1 },
		},
		OnStageComplete = new StageReward { Exp = 50, Gold = 30 },
	};

	public static Godot.Collections.Array GetWaveProgressArray(string stageId)
	{
		var stage = LoadByStageId(stageId);
		var result = new Godot.Collections.Array();
		foreach (var wave in stage.Waves)
		{
			result.Add(wave.Progress);
		}

		return result;
	}

	private static string GetFirstCatalogStagePath()
	{
		EnsureCatalogLoaded();
		if (_catalogCache?.Chapters == null)
		{
			return string.Empty;
		}

		foreach (var chapter in _catalogCache.Chapters)
		{
			if (chapter.Levels == null)
			{
				continue;
			}

			foreach (var level in chapter.Levels)
			{
				if (!string.IsNullOrEmpty(level.Path))
				{
					return level.Path;
				}
			}
		}

		return string.Empty;
	}

	private static string ResolvePathByStageId(string stageId)
	{
		EnsureCatalogLoaded();
		if (_catalogCache?.Chapters == null)
		{
			return string.Empty;
		}

		foreach (var chapter in _catalogCache.Chapters)
		{
			if (chapter.Levels == null)
			{
				continue;
			}

			var hit = chapter.Levels.FirstOrDefault(l => l.StageId == stageId);
			if (hit != null && !string.IsNullOrEmpty(hit.Path))
			{
				return hit.Path;
			}
		}

		return string.Empty;
	}

	private static void EnsureCatalogLoaded()
	{
		if (_catalogCache != null)
		{
			return;
		}

		_catalogCache = new StageCatalogRoot();
		if (!FileAccess.FileExists(CatalogPath))
		{
			return;
		}

		using var file = FileAccess.Open(CatalogPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_catalogCache = JsonSerializer.Deserialize<StageCatalogRoot>(file.GetAsText(), options) ?? new StageCatalogRoot();
	}

	public static IReadOnlyList<StageLevelEntry> GetOrderedCatalogEntries()
	{
		EnsureCatalogLoaded();
		var result = new List<StageLevelEntry>();
		if (_catalogCache?.Chapters == null)
		{
			return result;
		}

		foreach (var chapter in _catalogCache.Chapters)
		{
			if (chapter.Levels == null)
			{
				continue;
			}

			result.AddRange(chapter.Levels.Where(l => !string.IsNullOrEmpty(l.StageId)));
		}

		return result;
	}

	public static string GetNextStageId(string stageId)
	{
		if (string.IsNullOrEmpty(stageId))
		{
			return string.Empty;
		}

		var entries = GetOrderedCatalogEntries();
		for (var i = 0; i < entries.Count; i++)
		{
			if (!entries[i].StageId.Equals(stageId, System.StringComparison.Ordinal))
			{
				continue;
			}

			return i + 1 < entries.Count ? entries[i + 1].StageId : string.Empty;
		}

		return string.Empty;
	}

	public static string GetUnlockRequirement(string stageId)
	{
		if (string.IsNullOrEmpty(stageId))
		{
			return string.Empty;
		}

		var hit = GetOrderedCatalogEntries().FirstOrDefault(e => e.StageId == stageId);
		return hit?.UnlockFrom ?? string.Empty;
	}
}

public sealed class StageDefinition
{
	public string StageId { get; set; } = "training_01";
	public int StageLevel { get; set; } = 1;
	public float StageLength { get; set; } = 1200f;
	public float MarchSpeed { get; set; } = 45f;
	public List<WaveDefinition> Waves { get; set; } = new();
	public StageReward? OnStageComplete { get; set; }
}

public sealed class WaveDefinition
{
	public float Progress { get; set; }
	public string SpawnMode { get; set; } = "sequential";
	public string EnemyId { get; set; } = string.Empty;
	public float SpawnOffsetX { get; set; }
	public int Count { get; set; } = 1;
	public float RewardWeight { get; set; } = 1f;
	public List<WaveSpawnEntry>? Entries { get; set; }

	public bool IsCluster => SpawnMode.Equals("cluster", System.StringComparison.OrdinalIgnoreCase)
		|| (Entries != null && Entries.Count > 0);

	public IEnumerable<(string EnemyId, float OffsetX, int InstanceIndex)> EnumerateSpawns()
	{
		if (IsCluster && Entries != null && Entries.Count > 0)
		{
			foreach (var entry in Entries)
			{
				for (var i = 0; i < System.Math.Max(1, entry.Count); i++)
				{
					var offset = entry.SpawnOffsetX + entry.OffsetStep * i;
					yield return (entry.EnemyId, offset, i + 1);
				}
			}

			yield break;
		}

		for (var i = 0; i < System.Math.Max(1, Count); i++)
		{
			yield return (EnemyId, SpawnOffsetX, i + 1);
		}
	}
}

public sealed class WaveSpawnEntry
{
	public string EnemyId { get; set; } = string.Empty;
	public int Count { get; set; } = 1;
	public float SpawnOffsetX { get; set; }
	public float OffsetStep { get; set; } = 18f;
}

public sealed class StageReward
{
	public float Exp { get; set; }
	public int Gold { get; set; }
}

public enum StageRunState
{
	Marching,
	Engaging,
	WaveClearing,
}

public sealed class StageCatalogRoot
{
	public List<StageChapterEntry>? Chapters { get; set; } = new();
}

public sealed class StageChapterEntry
{
	public string ChapterId { get; set; } = string.Empty;
	public List<StageLevelEntry>? Levels { get; set; } = new();
}

public sealed class StageLevelEntry
{
	public string StageId { get; set; } = string.Empty;
	public int RecommendedLevel { get; set; } = 1;
	public string Path { get; set; } = string.Empty;
	public string UnlockFrom { get; set; } = string.Empty;
}
