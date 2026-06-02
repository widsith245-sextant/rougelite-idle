using System.Text.Json;
using Godot;

namespace RougeliteIdle.Core;

public static class DebugSettingsLoader
{
	private const string Path = "res://data/tables/meta/debug_settings.json";
	private static DebugSettings? _cache;

	public static DebugSettings Get()
	{
		if (_cache != null)
		{
			return _cache;
		}

		_cache = new DebugSettings();
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
		var loaded = JsonSerializer.Deserialize<DebugSettings>(file.GetAsText(), options);
		if (loaded != null)
		{
			_cache = loaded;
		}

		return _cache;
	}

	public sealed class DebugSettings
	{
		public bool CombatTraceEnabled { get; set; } = true;
		public bool SkipWonderlandTicket { get; set; } = true;
		public bool UnlockPassiveSkillSlots { get; set; } = true;
		public int MaxActiveSkillSlotsOverride { get; set; }
	}
}
