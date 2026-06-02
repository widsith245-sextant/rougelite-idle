using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Loot;

public static class ChestQualityLoader
{
	private const string Path = "res://data/tables/loot/chest_quality.json";
	private static Dictionary<string, QualityEntry>? _cache;

	public static QualityEntry Get(string qualityId)
	{
		EnsureLoaded();
		return _cache!.GetValueOrDefault(qualityId, new QualityEntry
		{
			Id = qualityId,
			DisplayName = "宝箱",
			MaxAccumulate = 5,
		});
	}

	public static int GetMaxAccumulate(string qualityId) => Get(qualityId).MaxAccumulate;

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, QualityEntry>();
		if (!FileAccess.FileExists(Path))
		{
			return;
		}

		using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<QualityRoot>(file.GetAsText(), options);
		if (root?.Qualities == null)
		{
			return;
		}

		foreach (var entry in root.Qualities)
		{
			_cache[entry.Id] = entry;
		}
	}

	public sealed class QualityEntry
	{
		public string Id { get; set; } = "common";
		public string DisplayName { get; set; } = "宝箱";
		public float[] Color { get; set; } = { 0.55f, 0.55f, 0.6f, 1f };
		public int MaxAccumulate { get; set; } = 5;
		public int ItemLevelBonus { get; set; }
		public int BonusAffixCount { get; set; }
		public string MinQualityTier { get; set; } = "common";
	}

	private sealed class QualityRoot
	{
		public List<QualityEntry>? Qualities { get; set; }
	}
}
