using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;

namespace RougeliteIdle.Save;

/// <summary>
/// Runs after all autoloads: load save or initialize new game, then start combat.
/// </summary>
public partial class SaveBootstrap : Node
{
	private float _saveDebounce;
	private const float SaveDebounceSeconds = 0.5f;

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

		if (save.HasSave && save.LoadGame())
		{
			combat.StartEncounterFromSaveOrDefault();
		}
		else
		{
			loot.InitializeNewGame();
			combat.StartEncounter();
		}

		eventBus.EquipmentChanged += _ => RequestSave();
		eventBus.SquadChanged += () => RequestSave();
		eventBus.LootInventoryChanged += () => RequestSave();
		eventBus.SkillsChanged += () => RequestSave();
		eventBus.RosterLevelChanged += _ => RequestSave();
		save.StampSessionTime();
	}

	public void RequestSave()
	{
		_saveDebounce = SaveDebounceSeconds;
	}
}
