using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Combat;

public static class DamageProfileLoader
{
	private const string Path = "res://data/tables/combat/damage_profiles.json";
	private static Dictionary<string, DamageProfileEntry>? _cache;

	public static DamageProfileEntry Get(string profileId)
	{
		EnsureLoaded();
		if (_cache != null && _cache.TryGetValue(profileId, out var entry))
		{
			return entry;
		}

		return new DamageProfileEntry { Id = "physical", DamageScale = 1f, CritAllowed = true };
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, DamageProfileEntry>();
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
		var root = JsonSerializer.Deserialize<DamageProfileRoot>(file.GetAsText(), options);
		if (root?.Profiles == null)
		{
			return;
		}

		foreach (var profile in root.Profiles)
		{
			_cache[profile.Id] = profile;
		}
	}

	private sealed class DamageProfileRoot
	{
		public List<DamageProfileEntry>? Profiles { get; set; }
	}
}

public sealed class DamageProfileEntry
{
	public string Id { get; set; } = "physical";
	public string DefenseStat { get; set; } = "Defense";
	public bool CritAllowed { get; set; } = true;
	public float DamageScale { get; set; } = 1f;
}
