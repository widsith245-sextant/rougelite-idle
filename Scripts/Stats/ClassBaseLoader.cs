using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Stats;

public static class ClassBaseLoader
{
	private const string Path = "res://data/tables/character/char_class_base.json";
	private static Dictionary<string, ClassBaseDefinition>? _cache;

	public static ClassBaseDefinition? Get(string classId)
	{
		EnsureLoaded();
		return _cache != null && _cache.TryGetValue(classId, out var def) ? def : null;
	}

	public static IReadOnlyDictionary<string, ClassBaseDefinition> GetAll()
	{
		EnsureLoaded();
		return _cache ?? new Dictionary<string, ClassBaseDefinition>();
	}

	private static void EnsureLoaded()
	{
		if (_cache != null)
		{
			return;
		}

		_cache = new Dictionary<string, ClassBaseDefinition>();
		if (!FileAccess.FileExists(Path))
		{
			GD.PushWarning($"Class base table missing: {Path}");
			return;
		}

		using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<ClassBaseRoot>(file.GetAsText(), options);
		if (root?.Classes == null)
		{
			return;
		}

		foreach (var entry in root.Classes)
		{
			_cache[entry.ClassId] = entry;
		}
	}

	private sealed class ClassBaseRoot
	{
		public List<ClassBaseDefinition>? Classes { get; set; }
	}
}

public class ClassBaseDefinition
{
	public string ClassId { get; set; } = string.Empty;
	public string NameCn { get; set; } = string.Empty;
	public string AllyUnitId { get; set; } = string.Empty;
	public float BaseHp { get; set; }
	public float HpPerLv { get; set; }
	public float BaseAtk { get; set; }
	public float AtkPerLv { get; set; }
	public float BaseRange { get; set; }
	public float MoveSpeed { get; set; }
	public float BaseCrit { get; set; }
	public float HitBoxRadius { get; set; }
	public float BaseAtkSpeed { get; set; } = 1f;
}
