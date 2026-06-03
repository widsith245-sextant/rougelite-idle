using Godot;

namespace RougeliteIdle.Core;

public partial class EventBus : Node
{
	[Signal]
	public delegate void PositionChangedEventHandler(string entityId, float oldX, float newX);

	[Signal]
	public delegate void DamageDealtEventHandler(string sourceId, string targetId, float amount, bool isCrit);

	[Signal]
	public delegate void UnitHpChangedEventHandler(string entityId, float currentHp, float maxHp);

	[Signal]
	public delegate void CombatStateChangedEventHandler(int state);

	[Signal]
	public delegate void CombatActionStartedEventHandler(string actorId);

	[Signal]
	public delegate void EquipmentChangedEventHandler(string unitId);

	[Signal]
	public delegate void StatsChangedEventHandler(string unitId);

	[Signal]
	public delegate void RunProgressChangedEventHandler(float progress, int waveIndex, int waveTotal);

	[Signal]
	public delegate void WaveStartedEventHandler(int waveIndex);

	[Signal]
	public delegate void WaveClearedEventHandler(int waveIndex);

	[Signal]
	public delegate void PendingChestChangedEventHandler(string quality, int count);

	[Signal]
	public delegate void LootInventoryChangedEventHandler();

	[Signal]
	public delegate void MarchStateChangedEventHandler(bool isMarching);

	[Signal]
	public delegate void SquadChangedEventHandler();

	[Signal]
	public delegate void ChestBubbleOpeningEventHandler(string quality);

	[Signal]
	public delegate void ChestOpenedEventHandler(string quality, string chestId);

	[Signal]
	public delegate void DbNodeUnlockedEventHandler(string nodeId);

	[Signal]
	public delegate void SkillsChangedEventHandler();

	[Signal]
	public delegate void RosterLevelChangedEventHandler(string rosterId);

	[Signal]
	public delegate void RosterExpChangedEventHandler(string rosterId);

	[Signal]
	public delegate void StarChartChangedEventHandler();

	[Signal]
	public delegate void RunSessionChangedEventHandler(string state, int roomIndex, int roomTotal, string roomType);

	[Signal]
	public delegate void StageRunCompletedEventHandler(string stageId);

	[Signal]
	public delegate void CombatBroadcastEventHandler(string message, string category);

	[Signal]
	public delegate void RunVisualModeChangedEventHandler(string mode);

	[Signal]
	public delegate void RunCardPickOfferedEventHandler();

	[Signal]
	public delegate void ItemIdentifiedEventHandler(Godot.Collections.Dictionary itemData);

	[Signal]
	public delegate void StageClearedEventHandler(string stageId);

	[Signal]
	public delegate void StageUnlockedEventHandler(string stageId);

	public void EmitPositionChanged(string entityId, float oldX, float newX)
	{
		EmitSignal(SignalName.PositionChanged, entityId, oldX, newX);
	}

	public void EmitDamageDealt(string sourceId, string targetId, float amount, bool isCrit = false)
	{
		EmitSignal(SignalName.DamageDealt, sourceId, targetId, amount, isCrit);
	}

	public void EmitUnitHpChanged(string entityId, float currentHp, float maxHp)
	{
		EmitSignal(SignalName.UnitHpChanged, entityId, currentHp, maxHp);
	}

	public void EmitCombatStateChanged(int state)
	{
		EmitSignal(SignalName.CombatStateChanged, state);
	}

	public void EmitCombatActionStarted(string actorId)
	{
		EmitSignal(SignalName.CombatActionStarted, actorId);
	}

	public void EmitEquipmentChanged(string unitId)
	{
		EmitSignal(SignalName.EquipmentChanged, unitId);
	}

	public void EmitStatsChanged(string unitId)
	{
		EmitSignal(SignalName.StatsChanged, unitId);
	}

	public void EmitRunProgressChanged(float progress, int waveIndex, int waveTotal)
	{
		EmitSignal(SignalName.RunProgressChanged, progress, waveIndex, waveTotal);
	}

	public void EmitWaveStarted(int waveIndex)
	{
		EmitSignal(SignalName.WaveStarted, waveIndex);
	}

	public void EmitWaveCleared(int waveIndex)
	{
		EmitSignal(SignalName.WaveCleared, waveIndex);
	}

	public void EmitPendingChestChanged(string quality, int count)
	{
		EmitSignal(SignalName.PendingChestChanged, quality, count);
	}

	public void EmitLootInventoryChanged()
	{
		EmitSignal(SignalName.LootInventoryChanged);
	}

	public void EmitMarchStateChanged(bool isMarching)
	{
		EmitSignal(SignalName.MarchStateChanged, isMarching);
	}

	public void EmitSquadChanged()
	{
		EmitSignal(SignalName.SquadChanged);
	}

	public void EmitChestBubbleOpening(string quality)
	{
		EmitSignal(SignalName.ChestBubbleOpening, quality);
	}

	public void EmitChestOpened(string quality, string chestId)
	{
		EmitSignal(SignalName.ChestOpened, quality, chestId);
	}

	public void EmitDbNodeUnlocked(string nodeId)
	{
		EmitSignal(SignalName.DbNodeUnlocked, nodeId);
	}

	public void EmitSkillsChanged()
	{
		EmitSignal(SignalName.SkillsChanged);
	}

	public void EmitRosterLevelChanged(string rosterId)
	{
		EmitSignal(SignalName.RosterLevelChanged, rosterId);
	}

	public void EmitRosterExpChanged(string rosterId)
	{
		EmitSignal(SignalName.RosterExpChanged, rosterId);
	}

	public void EmitStarChartChanged()
	{
		EmitSignal(SignalName.StarChartChanged);
	}

	public void EmitRunSessionChanged(string state, int roomIndex, int roomTotal, string roomType)
	{
		EmitSignal(SignalName.RunSessionChanged, state, roomIndex, roomTotal, roomType);
	}

	public void EmitStageRunCompleted(string stageId)
	{
		EmitSignal(SignalName.StageRunCompleted, stageId);
	}

	public void EmitCombatBroadcast(string message, string category = "reward")
	{
		EmitSignal(SignalName.CombatBroadcast, message, category);
	}

	public void EmitRunVisualModeChanged(string mode)
	{
		EmitSignal(SignalName.RunVisualModeChanged, mode);
	}

	public void EmitRunCardPickOffered()
	{
		EmitSignal(SignalName.RunCardPickOffered);
	}

	public void EmitItemIdentified(Godot.Collections.Dictionary itemData)
	{
		EmitSignal(SignalName.ItemIdentified, itemData);
	}

	public void EmitStageCleared(string stageId)
	{
		EmitSignal(SignalName.StageCleared, stageId);
	}

	public void EmitStageUnlocked(string stageId)
	{
		EmitSignal(SignalName.StageUnlocked, stageId);
	}
}
