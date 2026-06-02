using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Stats;

public static class AffixPoolLoader
{
	private const string Path = "res://data/tables/loot/item_affix_pool.json";
	private static List<AffixPoolEntry>? _cache;

	public static IReadOnlyList<AffixPoolEntry> GetAll()
	{
		if (_cache != null)
		{
			return _cache;
		}

		_cache = new List<AffixPoolEntry>();
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
		var root = JsonSerializer.Deserialize<AffixPoolRoot>(file.GetAsText(), options);
		_cache = root?.Affixes ?? new List<AffixPoolEntry>();
		return _cache;
	}

	public static StatId? MapAffixId(string affixId)
	{
		if (string.IsNullOrEmpty(affixId))
		{
			return null;
		}

		foreach (var entry in GetAll())
		{
			if (entry.AffixId == affixId)
			{
				return MapTarget(entry.AttributeTarget);
			}
		}

		if (affixId.StartsWith("suffix_spd_"))
		{
			return StatId.MoveSpeed;
		}

		return null;
	}

	public static StatId? MapTarget(string attributeTarget)
	{
		return attributeTarget switch
		{
			"Base_Damage_Percent" or "Damage" or "primary_atk" => StatId.Damage,
			"MaxHp" or "sub_hp" => StatId.MaxHp,
			"Move_Speed" or "primary_speed" => StatId.MoveSpeed,
			"Crit_Rate" or "sub_crit" => StatId.CritRate,
			"Crit_Damage" => StatId.CritDamage,
			"Defense" or "primary_armor" => StatId.Defense,
			"Atk_Speed" => StatId.AtkSpeed,
			"Normal_Attack_Range" => StatId.AtkRange,
			"Atk_Range" => StatId.AtkRange,
			_ => null,
		};
	}

	private sealed class AffixPoolRoot
	{
		public List<AffixPoolEntry>? Affixes { get; set; }
	}
}

public class AffixPoolEntry
{
	public string AffixId { get; set; } = string.Empty;
	public string PoolType { get; set; } = string.Empty;
	public string AttributeTarget { get; set; } = string.Empty;
	public float MinValue { get; set; }
	public float MaxValue { get; set; }
	public int Weight { get; set; } = 100;
	public string? SlotCondition { get; set; }
}
