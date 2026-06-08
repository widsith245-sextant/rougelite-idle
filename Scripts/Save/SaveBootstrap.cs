using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;
using RougeliteIdle.Meta;

namespace RougeliteIdle.Save;

/// <summary>
/// Runs after all autoloads: load save or initialize new game, then start combat.
/// </summary>
public partial class SaveBootstrap : Node
{
	private float _saveDebounce;
	private const float SaveDebounceSeconds = 0.5f;
	private const long OfflineRewardMinSeconds = 60;
	private OfflineIdleResult? _queuedOfflineRewards;

	public override void _Ready()
	{
		CallDeferred(MethodName.Bootstrap);
	}

	public override void _Process(double delta)
	{
		if (_saveDebounce > 0f)
		{
			_saveDebounce -= (float)delta;
			if (_saveDebounce <= 0f)
			{
				GetNode<SaveManager>("/root/SaveManager").SaveGame();
			}
		}
	}

	private void Bootstrap()
	{
		var save = GetNode<SaveManager>("/root/SaveManager");
		var loot = GetNode<LootManager>("/root/LootManager");
		var combat = GetNode<Combat.CombatManager>("/root/CombatManager");
		var eventBus = GetNode<EventBus>("/root/EventBus");
		var stageProg = GetNodeOrNull<StageProgressionManager>("/root/StageProgressionManager");

		var loaded = save.HasSave && save.LoadGame();
		if (loaded)
		{
			QueueOfflineRewardsWindow(save);
			combat.StartEncounterFromSaveOrDefault();
		}
		else
		{
			stageProg?.InitializeNewGame();
			loot.InitializeNewGame();
			GetNodeOrNull<DbManager>("/root/DbManager")?.GrantStarterNodes();
			combat.StartEncounter();
		}

		eventBus.EquipmentChanged += _ => RequestSave();
		eventBus.SquadChanged += () => RequestSave();
		eventBus.LootInventoryChanged += () => RequestSave();
		eventBus.SkillsChanged += () => RequestSave();
		eventBus.RosterLevelChanged += _ => RequestSave();
		eventBus.DbNodeUnlocked += _ => RequestSave();
		eventBus.StarChartChanged += () => RequestSave();
		eventBus.RosterExpChanged += _ => RequestSave();
		eventBus.WaveCleared += _ => RequestSave();
		eventBus.StageCleared += _ => RequestSave();
		eventBus.StageUnlocked += _ => RequestSave();
		save.StampSessionTime();
	}

	private void QueueOfflineRewardsWindow(SaveManager save)
	{
		if (save.LoadedLastSessionUnix <= 0)
		{
			return;
		}

		var offline = GetNode<OfflineIdleManager>("/root/OfflineIdleManager");
		var rosterProg = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
		var party = GetNode<PartyManager>("/root/PartyManager");
		var stageLevel = rosterProg.GetAverageActiveRosterLevel(party);
		var rewards = offline.CalculateOfflineRewards(save.LoadedLastSessionUnix, stageLevel);
		if (rewards.ElapsedSeconds < OfflineRewardMinSeconds)
		{
			return;
		}

		if (rewards.Gold < 0.01f && rewards.Experience < 0.01f)
		{
			return;
		}

		_queuedOfflineRewards = rewards;
		CallDeferred(MethodName.PresentQueuedOfflineRewards);
	}

	private void PresentQueuedOfflineRewards()
	{
		if (_queuedOfflineRewards == null)
		{
			return;
		}

		var windowMgr = GetNodeOrNull<OfflineRewardsWindowManager>("/root/OfflineRewardsWindowManager");
		windowMgr?.ShowPending(_queuedOfflineRewards);
		_queuedOfflineRewards = null;
	}

	public void RequestSave()
	{
		_saveDebounce = SaveDebounceSeconds;
	}
}
