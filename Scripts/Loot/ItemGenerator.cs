using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Loot;

/// <summary>
/// iLvl + weight-based affix RNG from equipment template tables.
/// </summary>
public static class ItemGenerator
{
	private const string EquipmentTemplatesPath = "res://data/tables/loot/equipment_templates.json";

	public static IReadOnlyList<ItemData> LoadEquipmentTemplates()
	{
		if (!FileAccess.FileExists(EquipmentTemplatesPath))
		{
			GD.PushWarning($"Equipment templates not found: {EquipmentTemplatesPath}");
			return new List<ItemData>();
		}

		using var file = FileAccess.Open(EquipmentTemplatesPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError($"Failed to open equipment templates: {EquipmentTemplatesPath}");
			return new List<ItemData>();
		}

		var json = file.GetAsText();
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<EquipmentTemplateRoot>(json, options);
		if (root?.Items == null)
		{
			return new List<ItemData>();
		}

		var results = new List<ItemData>();
		foreach (var entry in root.Items)
		{
			results.Add(MapTemplateEntry(entry));
		}

		return results;
	}

	public static ItemData IdentifyFromTemplate(ItemData template, int itemLevel, RandomNumberGenerator rng, string quality = "common")
	{
		var qualityMeta = ChestQualityLoader.Get(quality);
		itemLevel += qualityMeta.ItemLevelBonus;

		var item = new ItemData
		{
			Id = System.Guid.NewGuid().ToString("N"),
			DisplayName = template.DisplayName,
			Quality = qualityMeta.MinQualityTier,
			Slot = template.Slot,
			ItemLevel = itemLevel,
			BaseStatMin = template.BaseStatMin,
			BaseStatMax = template.BaseStatMax,
			EffectId = template.EffectId,
			ClassId = template.ClassId,
		};

		item.RolledBaseStat = rng.RandfRange(item.BaseStatMin, item.BaseStatMax);

		foreach (var affix in template.AffixDefinitions)
		{
			item.Affixes.Add(new AffixRoll
			{
				Id = affix.Id,
				DisplayName = affix.DisplayName,
				Value = rng.RandfRange(affix.Min, affix.Max),
				IsPrimary = affix.IsPrimary,
			});
		}

		for (var i = 0; i < qualityMeta.BonusAffixCount; i++)
		{
			var rolled = AffixPoolLoader.RollForSlot(template.Slot, rng);
			if (rolled != null)
			{
				item.Affixes.Add(rolled);
				continue;
			}

			if (template.AffixDefinitions.Count == 0)
			{
				break;
			}

			var fallback = template.AffixDefinitions[rng.RandiRange(0, template.AffixDefinitions.Count - 1)];
			item.Affixes.Add(new AffixRoll
			{
				Id = fallback.Id,
				DisplayName = fallback.DisplayName,
				Value = rng.RandfRange(fallback.Min, fallback.Max),
				IsPrimary = fallback.IsPrimary,
			});
		}

		return item;
	}

	private static ItemData MapTemplateEntry(TemplateItemEntry entry)
	{
		var item = new ItemData
		{
			Id = entry.Id,
			DisplayName = entry.DisplayName,
			Slot = ParseSlot(entry.Slot),
			ItemLevel = entry.ItemLevel,
			BaseStatMin = entry.BaseStatMin,
			BaseStatMax = entry.BaseStatMax,
			EffectId = entry.EffectId,
			ClassId = entry.ClassId ?? string.Empty,
		};

		if (entry.Affixes == null)
		{
			return item;
		}

		foreach (var affix in entry.Affixes)
		{
			item.AffixDefinitions.Add(new AffixDefinition
			{
				Id = affix.Id,
				DisplayName = affix.DisplayName,
				Min = affix.Min,
				Max = affix.Max,
				IsPrimary = affix.IsPrimary,
			});
		}

		return item;
	}

	private static SlotType ParseSlot(string slot) =>
		System.Enum.TryParse<SlotType>(slot, true, out var parsed) ? parsed : SlotType.Weapon;

	private sealed class EquipmentTemplateRoot
	{
		public List<TemplateItemEntry>? Items { get; set; }
	}

	private sealed class TemplateItemEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string Slot { get; set; } = string.Empty;
		public string? ClassId { get; set; }
		public int ItemLevel { get; set; }
		public float BaseStatMin { get; set; }
		public float BaseStatMax { get; set; }
		public string? EffectId { get; set; }
		public List<TemplateAffixEntry>? Affixes { get; set; }
	}

	private sealed class TemplateAffixEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public float Min { get; set; }
		public float Max { get; set; }
		public float DefaultValue { get; set; }
		public bool IsPrimary { get; set; }
	}
}
