using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Meta;
using RougeliteIdle.Save;

namespace RougeliteIdle.Loot;

public partial class LootManager : Node
{
	private const string NewGameTablePath = "res://data/tables/loot/new_game.json";
	private const string DefaultEquipUnitId = "ally_a";

	private static readonly string[] DefaultUnitIds = { "ally_a", "ally_b", "ally_c" };

	private readonly List<ItemData> _unidentifiedChests = new();
	private readonly List<ItemData> _identifiedItems = new();
	private readonly List<ItemData> _equipmentTemplates = new();
	private readonly Dictionary<string, Dictionary<SlotType, ItemData>> _equippedByUnit = new();
	private readonly Dictionary<string, int> _pendingChestsByQuality = new();
	private readonly RandomNumberGenerator _rng = new();

	private EventBus _eventBus = null!;
	private string _activePendingQuality = "common";

	public IReadOnlyList<ItemData> UnidentifiedChests => _unidentifiedChests;
	public IReadOnlyList<ItemData> IdentifiedItems => _identifiedItems;
	public string ActivePendingQuality => _activePendingQuality;

	public override void _Ready()
	{
		_rng.Randomize();
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_equipmentTemplates.AddRange(ItemGenerator.LoadEquipmentTemplates());
		EnsureDefaultUnits();
	}

	private void EnsureDefaultUnits()
	{
		foreach (var unitId in DefaultUnitIds)
		{
			if (!_equippedByUnit.ContainsKey(unitId))
			{
				_equippedByUnit[unitId] = new Dictionary<SlotType, ItemData>();
			}
		}
	}

	public void InitializeNewGame()
	{
		_unidentifiedChests.Clear();
		_identifiedItems.Clear();
		_pendingChestsByQuality.Clear();
		_activePendingQuality = "common";
		foreach (var unitId in DefaultUnitIds)
		{
			_equippedByUnit[unitId] = new Dictionary<SlotType, ItemData>();
		}

		if (!FileAccess.FileExists(NewGameTablePath))
		{
			GD.PushWarning($"New game table not found: {NewGameTablePath}");
			return;
		}

		using var file = FileAccess.Open(NewGameTablePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var root = JsonSerializer.Deserialize<NewGameRoot>(file.GetAsText(), options);
		if (root?.StartingChests == null)
		{
			return;
		}

		foreach (var chest in root.StartingChests)
		{
			_unidentifiedChests.Add(new ItemData
			{
				Id = chest.Id,
				DisplayName = chest.DisplayName,
			});
		}

		EmitLootChanged();
	}

	public bool AddPendingChest(string quality)
	{
		if (string.IsNullOrEmpty(quality))
		{
			quality = "common";
		}

		_activePendingQuality = quality;
		var max = ChestQualityLoader.GetMaxAccumulate(quality);
		var count = _pendingChestsByQuality.GetValueOrDefault(quality) + 1;
		_pendingChestsByQuality[quality] = count;
		_eventBus.EmitPendingChestChanged(quality, count);

		if (count >= max)
		{
			OpenOnePendingChest(quality);
			return true;
		}

		return false;
	}

	public bool OpenOnePendingChest() => OpenOnePendingChest(_activePendingQuality);

	public bool OpenOnePendingChest(string quality)
	{
		if (string.IsNullOrEmpty(quality))
		{
			quality = _activePendingQuality;
		}

		if (!_pendingChestsByQuality.TryGetValue(quality, out var count) || count <= 0)
		{
			return false;
		}

		var meta = ChestQualityLoader.Get(quality);
		var chestId = $"pending_{quality}_{Guid.NewGuid():N}";
		_unidentifiedChests.Add(new ItemData
		{
			Id = chestId,
			DisplayName = meta.DisplayName,
			Quality = quality,
		});

		_pendingChestsByQuality[quality] = count - 1;
		var remaining = count - 1;
		_eventBus.EmitChestBubbleOpening(quality);
		_eventBus.EmitChestOpened(quality, chestId);
		_eventBus.EmitPendingChestChanged(quality, remaining);
		EmitLootChanged();
		return true;
	}

	public void FlushPendingToInventory() => FlushPendingToInventory(_activePendingQuality);

	public void FlushPendingToInventory(string quality)
	{
		if (string.IsNullOrEmpty(quality))
		{
			quality = _activePendingQuality;
		}

		if (!_pendingChestsByQuality.TryGetValue(quality, out var count) || count <= 0)
		{
			return;
		}

		var meta = ChestQualityLoader.Get(quality);
		for (var i = 0; i < count; i++)
		{
			_unidentifiedChests.Add(new ItemData
			{
				Id = $"pending_{quality}_{Guid.NewGuid():N}",
				DisplayName = meta.DisplayName,
				Quality = quality,
			});
		}

		_pendingChestsByQuality[quality] = 0;
		_eventBus.EmitPendingChestChanged(quality, 0);
		EmitLootChanged();
	}

	public void FlushAllPendingToInventory()
	{
		foreach (var quality in _pendingChestsByQuality.Keys.ToList())
		{
			FlushPendingToInventory(quality);
		}
	}

	public int GetPendingCount() => GetPendingCount(_activePendingQuality);

	public int GetPendingCount(string quality)
	{
		if (string.IsNullOrEmpty(quality))
		{
			quality = _activePendingQuality;
		}

		return _pendingChestsByQuality.GetValueOrDefault(quality);
	}

	public Godot.Collections.Dictionary GetPendingChestSnapshot() =>
		GetPendingChestSnapshot(_activePendingQuality);

	public Godot.Collections.Dictionary GetPendingChestSnapshot(string quality)
	{
		if (string.IsNullOrEmpty(quality))
		{
			quality = _activePendingQuality;
		}

		var meta = ChestQualityLoader.Get(quality);
		return new Godot.Collections.Dictionary
		{
			{ "quality", quality },
			{ "count", GetPendingCount(quality) },
			{ "max", meta.MaxAccumulate },
			{ "display_name", meta.DisplayName },
		};
	}

	public IReadOnlyList<ItemData> GetEquippedForUnit(string unitId)
	{
		EnsureUnit(unitId);
		return _equippedByUnit[unitId].Values.ToList();
	}

	public Godot.Collections.Array GetEquippedSnapshot(string unitId = DefaultEquipUnitId)
	{
		EnsureUnit(unitId);
		var result = new Godot.Collections.Array();
		foreach (var pair in _equippedByUnit[unitId])
		{
			var dict = ItemToDictionary(pair.Value);
			dict["equipped_slot"] = pair.Key.ToString();
			result.Add(dict);
		}

		return result;
	}

	public bool EquipByBagIndex(int bagIndex, string unitId = DefaultEquipUnitId)
	{
		if (bagIndex < 0 || bagIndex >= _identifiedItems.Count)
		{
			return false;
		}

		EnsureUnit(unitId);
		var item = _identifiedItems[bagIndex];
		var equipped = _equippedByUnit[unitId];
		if (equipped.TryGetValue(item.Slot, out var previous) && previous.Id == item.Id)
		{
			return false;
		}

		// Move currently equipped item back to inventory first.
		if (equipped.TryGetValue(item.Slot, out previous))
		{
			_identifiedItems.Add(previous);
		}

		_identifiedItems.RemoveAt(bagIndex);
		equipped[item.Slot] = item;
		_eventBus.EmitEquipmentChanged(unitId);
		_eventBus.EmitStatsChanged(unitId);
		EmitLootChanged();
		return true;
	}

	public void Unequip(SlotType slot, string unitId = DefaultEquipUnitId)
	{
		EnsureUnit(unitId);
		if (!_equippedByUnit[unitId].Remove(slot, out var item))
		{
			return;
		}

		_identifiedItems.Add(item);
		_eventBus.EmitEquipmentChanged(unitId);
		_eventBus.EmitStatsChanged(unitId);
		EmitLootChanged();
	}

	public bool UnequipBySlotName(string slotName, string unitId = DefaultEquipUnitId)
	{
		if (!Enum.TryParse<SlotType>(slotName, true, out var slot))
		{
			return false;
		}

		var had = _equippedByUnit.GetValueOrDefault(unitId)?.ContainsKey(slot) ?? false;
		Unequip(slot, unitId);
		return had;
	}

	public Godot.Collections.Dictionary? IdentifyNextAsDictionary(int currentStageLevel)
	{
		var item = IdentifyNext(currentStageLevel);
		return item == null ? null : ItemToDictionary(item);
	}

	public Godot.Collections.Array GetIdentifiedItemsSnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var item in _identifiedItems)
		{
			result.Add(ItemToDictionary(item));
		}

		return result;
	}

	public Godot.Collections.Array GetUnidentifiedChestsSnapshot()
	{
		var result = new Godot.Collections.Array();
		for (var i = 0; i < _unidentifiedChests.Count; i++)
		{
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", _unidentifiedChests[i].Id },
				{ "display_name", _unidentifiedChests[i].DisplayName },
				{ "quality", _unidentifiedChests[i].Quality },
				{ "index", i },
			});
		}

		return result;
	}

	public int GetUnidentifiedCount() => _unidentifiedChests.Count;

	public int GetIdentifiedCount() => _identifiedItems.Count;

	public void SalvageByIndex(int index)
	{
		if (index < 0 || index >= _identifiedItems.Count)
		{
			return;
		}

		Salvage(_identifiedItems[index]);
	}

	private Godot.Collections.Dictionary ItemToDictionary(ItemData item)
	{
		var affixes = new Godot.Collections.Array();
		foreach (var affix in item.Affixes)
		{
			affixes.Add(new Godot.Collections.Dictionary
			{
				{ "id", affix.Id },
				{ "display_name", affix.DisplayName },
				{ "value", affix.Value },
				{ "is_primary", affix.IsPrimary },
			});
		}

		return new Godot.Collections.Dictionary
		{
			{ "id", item.Id },
			{ "display_name", item.DisplayName },
			{ "quality", item.Quality },
			{ "item_level", item.ItemLevel },
			{ "rolled_base_stat", item.RolledBaseStat },
			{ "slot", item.Slot.ToString() },
			{ "effect_id", item.EffectId ?? string.Empty },
			{ "affixes", affixes },
		};
	}

	public void AddChest(ItemData chestTemplate)
	{
		_unidentifiedChests.Add(chestTemplate);
		EmitLootChanged();
	}

	public ItemData? IdentifyNext(int currentStageLevel)
	{
		if (_unidentifiedChests.Count == 0)
		{
			return null;
		}

		var chest = _unidentifiedChests[0];
		var chestQuality = string.IsNullOrEmpty(chest.Quality) ? "common" : chest.Quality;
		_unidentifiedChests.RemoveAt(0);

		if (_equipmentTemplates.Count == 0)
		{
			return null;
		}

		var resolvedTemplate = _equipmentTemplates[_rng.RandiRange(0, _equipmentTemplates.Count - 1)];
		var item = ItemGenerator.IdentifyFromTemplate(resolvedTemplate, currentStageLevel, _rng, chestQuality);
		_identifiedItems.Add(item);
		TryTrainingBonusChest();
		EmitLootChanged();
		return item;
	}

	private void TryTrainingBonusChest()
	{
		var prog = GetNodeOrNull<ProgressionManager>("/root/ProgressionManager");
		if (prog == null || !prog.TryRollTrainingBonusChest())
		{
			return;
		}

		_unidentifiedChests.Add(new ItemData
		{
			Id = "bonus_chest",
			DisplayName = "训练宝箱",
		});
		EmitLootChanged();
	}

	public void Salvage(ItemData item)
	{
		_identifiedItems.Remove(item);
		foreach (var unitId in _equippedByUnit.Keys.ToList())
		{
			var slots = _equippedByUnit[unitId]
				.Where(p => p.Value.Id == item.Id)
				.Select(p => p.Key)
				.ToList();
			foreach (var slot in slots)
			{
				_equippedByUnit[unitId].Remove(slot);
				_eventBus.EmitEquipmentChanged(unitId);
				_eventBus.EmitStatsChanged(unitId);
			}
		}

		var prog = GetNodeOrNull<ProgressionManager>("/root/ProgressionManager");
		prog?.AddGoldFromSalvage(item.ItemLevel);
		EmitLootChanged();
	}

	private void EmitLootChanged()
	{
		_eventBus.EmitLootInventoryChanged();
	}

	private void EnsureUnit(string unitId)
	{
		if (!_equippedByUnit.ContainsKey(unitId))
		{
			_equippedByUnit[unitId] = new Dictionary<SlotType, ItemData>();
		}
	}

	public List<ItemSaveDto> ExportIdentifiedItems()
	{
		var result = new List<ItemSaveDto>();
		foreach (var item in _identifiedItems)
		{
			result.Add(ToSaveDto(item));
		}

		return result;
	}

	public List<ChestSaveDto> ExportUnidentifiedChests()
	{
		var result = new List<ChestSaveDto>();
		foreach (var chest in _unidentifiedChests)
		{
			result.Add(new ChestSaveDto
			{
				Id = chest.Id,
				DisplayName = chest.DisplayName,
				Quality = chest.Quality,
			});
		}

		return result;
	}

	public Dictionary<string, int> ExportPendingChests() => new(_pendingChestsByQuality);

	public Dictionary<string, Dictionary<string, ItemSaveDto>> ExportEquippedByUnit()
	{
		var result = new Dictionary<string, Dictionary<string, ItemSaveDto>>();
		foreach (var unitPair in _equippedByUnit)
		{
			var slotMap = new Dictionary<string, ItemSaveDto>();
			foreach (var slotPair in unitPair.Value)
			{
				slotMap[slotPair.Key.ToString()] = ToSaveDto(slotPair.Value);
			}

			result[unitPair.Key] = slotMap;
		}

		return result;
	}

	public void RestoreInventory(
		List<ItemSaveDto> identified,
		List<ChestSaveDto> chests,
		Dictionary<string, int> pending,
		string activeQuality,
		Dictionary<string, Dictionary<string, ItemSaveDto>> equipped)
	{
		_identifiedItems.Clear();
		_unidentifiedChests.Clear();
		_pendingChestsByQuality.Clear();
		EnsureDefaultUnits();
		foreach (var unitId in DefaultUnitIds)
		{
			_equippedByUnit[unitId] = new Dictionary<SlotType, ItemData>();
		}

		foreach (var dto in identified)
		{
			_identifiedItems.Add(FromSaveDto(dto));
		}

		foreach (var dto in chests)
		{
			_unidentifiedChests.Add(new ItemData
			{
				Id = dto.Id,
				DisplayName = dto.DisplayName,
				Quality = dto.Quality,
			});
		}

		foreach (var pair in pending)
		{
			_pendingChestsByQuality[pair.Key] = pair.Value;
		}

		_activePendingQuality = string.IsNullOrEmpty(activeQuality) ? "common" : activeQuality;

		foreach (var unitPair in equipped)
		{
			EnsureUnit(unitPair.Key);
			foreach (var slotPair in unitPair.Value)
			{
				if (!Enum.TryParse<SlotType>(slotPair.Key, true, out var slot))
				{
					continue;
				}

				_equippedByUnit[unitPair.Key][slot] = FromSaveDto(slotPair.Value);
			}
		}

		EmitLootChanged();
	}

	private static ItemSaveDto ToSaveDto(ItemData item)
	{
		var affixes = new List<AffixSaveDto>();
		foreach (var affix in item.Affixes)
		{
			affixes.Add(new AffixSaveDto
			{
				Id = affix.Id,
				DisplayName = affix.DisplayName,
				Value = affix.Value,
				IsPrimary = affix.IsPrimary,
			});
		}

		return new ItemSaveDto
		{
			Id = item.Id,
			DisplayName = item.DisplayName,
			Quality = item.Quality,
			Slot = item.Slot.ToString(),
			ItemLevel = item.ItemLevel,
			RolledBaseStat = item.RolledBaseStat,
			EffectId = item.EffectId ?? string.Empty,
			Affixes = affixes,
		};
	}

	private static ItemData FromSaveDto(ItemSaveDto dto)
	{
		var item = new ItemData
		{
			Id = dto.Id,
			DisplayName = dto.DisplayName,
			Quality = dto.Quality,
			ItemLevel = dto.ItemLevel,
			RolledBaseStat = dto.RolledBaseStat,
			EffectId = string.IsNullOrEmpty(dto.EffectId) ? null : dto.EffectId,
		};

		if (Enum.TryParse<SlotType>(dto.Slot, true, out var slot))
		{
			item.Slot = slot;
		}

		foreach (var affix in dto.Affixes)
		{
			item.Affixes.Add(new AffixRoll
			{
				Id = affix.Id,
				DisplayName = affix.DisplayName,
				Value = affix.Value,
				IsPrimary = affix.IsPrimary,
			});
		}

		return item;
	}

	private sealed class NewGameRoot
	{
		public List<ChestEntry>? StartingChests { get; set; }
	}

	private sealed class ChestEntry
	{
		public string Id { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
	}
}
