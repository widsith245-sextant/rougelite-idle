using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Meta;

public static class EarlyGameCapsLoader
{
	private const string Path = "res://data/tables/meta/early_game_caps.json";
	private static EarlyGameCaps? _cache;

	public static EarlyGameCaps Get()
	{
		if (_cache != null)
		{
			return _cache;
		}

		_cache = new EarlyGameCaps();
		if (!FileAccess.FileExists(Path))
		{
			return _cache;
		}

		using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return _cache;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var loaded = JsonSerializer.Deserialize<EarlyGameCaps>(file.GetAsText(), options);
		if (loaded != null)
		{
			_cache = loaded;
		}

		return _cache;
	}

	public sealed class EarlyGameCaps
	{
		public int DefaultMaxActiveSlots { get; set; } = 1;
		public int DefaultMaxActiveSkillSlots { get; set; } = 1;
		public bool PassiveSlotsUnlocked { get; set; }
		public Dictionary<string, float> PlayerStatMultipliers { get; set; } = new();
		public int PlayerInitialLevelCap { get; set; } = 10;
		public int BagSlotsVisible { get; set; } = 48;

		public float GetMultiplier(string key, float fallback = 1f) =>
			PlayerStatMultipliers.GetValueOrDefault(key, fallback);
	}
}
