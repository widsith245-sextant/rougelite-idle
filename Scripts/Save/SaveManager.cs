using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Loot;
using RougeliteIdle.Meta;

namespace RougeliteIdle.Save;

public partial class SaveManager : Node
{
	private const string SavePath = "user://savegame.json";

	public bool HasSave => FileAccess.FileExists(SavePath);

	public void SaveGame()
	{
		var loot = GetNode<LootManager>("/root/LootManager");
		var meta = GetNode<MetaManager>("/root/MetaManager");
		var prog = GetNode<ProgressionManager>("/root/ProgressionManager");
		var db = GetNodeOrNull<DbManager>("/root/DbManager");
		var party = GetNode<PartyManager>("/root/PartyManager");
		var skills = GetNode<CharacterSkillManager>("/root/CharacterSkillManager");
		var combat = GetNode<CombatManager>("/root/CombatManager");
		var rosterProg = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");

		var data = new SaveData
		{
			Version = 2,
			LastSessionUnix = (long)Time.GetUnixTimeFromSystem(),
			StarChartPoints = meta.StarChartPoints,
			GlobalStatBonusPercent = meta.GlobalStatBonusPercent,
			StarChartPurchasedNodes = meta.GetPurchasedStarNodeIds(),
			Gold = prog.Gold,
			WonderlandTickets = prog.WonderlandTickets,
			TrainingBonusChests = prog.TrainingBonusChests,
			DbUnlockedNodes = db?.GetPurchasedNodeIds().ToList() ?? new List<string>(),
			ActiveRosterIds = party.ExportActiveRosterIds(),
			IdentifiedItems = loot.ExportIdentifiedItems(),
			UnidentifiedChests = loot.ExportUnidentifiedChests(),
			PendingChestsByQuality = loot.ExportPendingChests(),
			ActivePendingQuality = loot.ActivePendingQuality,
			EquippedByUnit = loot.ExportEquippedByUnit(),
			SkillUnlockedByRoster = skills.ExportUnlockedNodes(),
			SkillEquippedByRoster = skills.ExportEquippedSkills(),
			Combat = combat.ExportCombatSave(),
			RosterProgress = rosterProg.ExportProgress(),
		};

		var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		file?.StoreString(json);
	}

	public bool LoadGame()
	{
		if (!HasSave)
		{
			return false;
		}

		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return false;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var data = JsonSerializer.Deserialize<SaveData>(file.GetAsText(), options);
		if (data == null)
		{
			return false;
		}

		var meta = GetNode<MetaManager>("/root/MetaManager");
		meta.Restore(data.StarChartPoints, data.GlobalStatBonusPercent);
		meta.RestoreStarNodes(data.StarChartPurchasedNodes ?? new List<string>());

		var prog = GetNode<ProgressionManager>("/root/ProgressionManager");
		prog.RestoreProgression(
			data.Gold,
			data.WonderlandTickets,
			data.TrainingBonusChests);

		var rosterProg = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
		rosterProg.RestoreProgress(
			data.RosterProgress,
			data.TeamLevel,
			data.TeamExp);

		var db = GetNodeOrNull<DbManager>("/root/DbManager");
		db?.RestorePurchasedNodes(data.DbUnlockedNodes ?? new List<string>());

		var party = GetNode<PartyManager>("/root/PartyManager");
		party.RestoreActiveRosterIds(data.ActiveRosterIds ?? new List<string>());

		var loot = GetNode<LootManager>("/root/LootManager");
		loot.RestoreInventory(
			data.IdentifiedItems ?? new List<ItemSaveDto>(),
			data.UnidentifiedChests ?? new List<ChestSaveDto>(),
			data.PendingChestsByQuality ?? new Dictionary<string, int>(),
			data.ActivePendingQuality ?? "common",
			data.EquippedByUnit ?? new Dictionary<string, Dictionary<string, ItemSaveDto>>());

		var skills = GetNode<CharacterSkillManager>("/root/CharacterSkillManager");
		skills.RestoreState(
			data.SkillUnlockedByRoster ?? new Dictionary<string, List<string>>(),
			data.SkillEquippedByRoster ?? new Dictionary<string, Dictionary<string, string>>());

		var combat = GetNode<CombatManager>("/root/CombatManager");
		combat.SetPendingCombatSave(data.Combat);

		return true;
	}

	public void StampSessionTime()
	{
		if (!HasSave)
		{
			return;
		}

		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var data = JsonSerializer.Deserialize<SaveData>(file.GetAsText(), options);
		if (data == null)
		{
			return;
		}

		data.LastSessionUnix = (long)Time.GetUnixTimeFromSystem();
		var json = JsonSerializer.Serialize(data);
		using var write = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		write?.StoreString(json);
	}
}
