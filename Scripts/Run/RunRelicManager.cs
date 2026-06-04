using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Run;

public readonly struct RunRelicBuffTotals
{
	public float DamagePercent { get; init; }
	public float MaxHpPercent { get; init; }
	public float MoveSpeedPercent { get; init; }
	public float CritPercent { get; init; }
	public float RunGoldBonusPercent { get; init; }
}

public partial class RunRelicManager : Node
{
	private const string RelicPoolPath = "res://data/tables/run/run_relic_pool.json";

	private readonly List<string> _activeRelicIds = new();
	private readonly List<string> _pendingOffers = new();

	private RunRelicPoolTable _pool = new();
	private EventBus _eventBus = null!;
	private CombatManager _combat = null!;
	private RunSessionManager _runSession = null!;
	private StatsService _stats = null!;
	private RandomNumberGenerator _rng = new();

	private bool _pendingRelicPick;
	private bool _roomRewardPick;

	public bool HasActiveRelics => _activeRelicIds.Count > 0;
	public bool IsPendingPick => _pendingRelicPick;

	public override void _Ready()
	{
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_combat = GetNode<CombatManager>("/root/CombatManager");
		_runSession = GetNode<RunSessionManager>("/root/RunSessionManager");
		_stats = GetNode<StatsService>("/root/StatsService");
		_rng.Randomize();
		LoadPool();
	}

	public void ResetForRun()
	{
		_activeRelicIds.Clear();
		_pendingOffers.Clear();
		_pendingRelicPick = false;
		_roomRewardPick = false;
	}

	public void Clear()
	{
		ResetForRun();
		_combat.SetRunPaused(false);
	}

	public Godot.Collections.Array GetActiveRelicsSnapshot()
	{
		var arr = new Godot.Collections.Array();
		foreach (var relicId in _activeRelicIds)
		{
			var def = FindRelic(relicId);
			if (def == null)
			{
				continue;
			}

			arr.Add(new Godot.Collections.Dictionary
			{
				{ "id", def.Id },
				{ "name", def.Name },
				{ "desc", def.Desc },
			});
		}

		return arr;
	}

	public Godot.Collections.Array GetPendingOffersSnapshot()
	{
		var arr = new Godot.Collections.Array();
		foreach (var relicId in _pendingOffers)
		{
			var def = FindRelic(relicId);
			if (def == null)
			{
				continue;
			}

			arr.Add(new Godot.Collections.Dictionary
			{
				{ "id", def.Id },
				{ "name", def.Name },
				{ "desc", def.Desc },
			});
		}

		return arr;
	}

	public RunRelicBuffTotals GetAggregatedBuffs()
	{
		var damage = 0f;
		var maxHp = 0f;
		var moveSpeed = 0f;
		var crit = 0f;
		var gold = 0f;
		foreach (var relicId in _activeRelicIds)
		{
			var def = FindRelic(relicId);
			if (def?.Effect == null)
			{
				continue;
			}

			AccumulateEffect(def.Effect, ref damage, ref maxHp, ref moveSpeed, ref crit, ref gold);
			if (!string.IsNullOrEmpty(def.Effect.SecondaryType))
			{
				var secondary = new RunRelicEffectEntry
				{
					Type = def.Effect.SecondaryType,
					Value = def.Effect.SecondaryValue,
				};
				AccumulateEffect(secondary, ref damage, ref maxHp, ref moveSpeed, ref crit, ref gold);
			}
		}

		return new RunRelicBuffTotals
		{
			DamagePercent = damage,
			MaxHpPercent = maxHp,
			MoveSpeedPercent = moveSpeed,
			CritPercent = crit,
			RunGoldBonusPercent = gold,
		};
	}

	public float GetRunGoldBonusPercent() => GetAggregatedBuffs().RunGoldBonusPercent;

	public void ApplyRelicModifiers(UnitStats stats)
	{
		if (!HasActiveRelics)
		{
			return;
		}

		var buffs = GetAggregatedBuffs();
		if (buffs.DamagePercent > 0f)
		{
			stats.AddIncreased(StatId.Damage, buffs.DamagePercent / 100f);
		}

		if (buffs.MaxHpPercent > 0f)
		{
			stats.AddIncreased(StatId.MaxHp, buffs.MaxHpPercent / 100f);
		}

		if (buffs.MoveSpeedPercent > 0f)
		{
			stats.AddIncreased(StatId.MoveSpeed, buffs.MoveSpeedPercent / 100f);
		}

		if (buffs.CritPercent > 0f)
		{
			stats.AddFlat(StatId.CritRate, buffs.CritPercent / 100f);
		}
	}

	public void BeginRoomRewardPick()
	{
		if (_pendingRelicPick)
		{
			return;
		}

		BeginRelicPick(isRoomReward: true);
	}

	public void ApplyRelic(string relicId)
	{
		if (!_pendingRelicPick || string.IsNullOrEmpty(relicId))
		{
			return;
		}

		var def = FindRelic(relicId);
		if (def == null)
		{
			return;
		}

		_activeRelicIds.Add(relicId);
		ApplyRelicEffect(def);
		_stats.RebuildAll(_combat.Allies);
		foreach (var ally in _combat.Allies)
		{
			ally.Stats = _stats.GetOrCreate(ally);
			ally.MaxHp = ally.Stats.GetFinal(StatId.MaxHp);
			ally.CurrentHp = Mathf.Min(ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitStatsChanged(ally.Id);
		}

		_eventBus.EmitCombatBroadcast($"获得遗物：{def.Name}", "run");
		_pendingRelicPick = false;
		_pendingOffers.Clear();
		_combat.SetRunPaused(false);

		if (_roomRewardPick)
		{
			_roomRewardPick = false;
			_runSession.CompleteRoomAfterRelicReward();
		}
	}

	private void BeginRelicPick(bool isRoomReward)
	{
		if (_pool.Relics.Count == 0)
		{
			if (isRoomReward)
			{
				_runSession.CompleteRoomAfterRelicReward();
			}

			return;
		}

		_pendingRelicPick = true;
		_roomRewardPick = isRoomReward;
		_pendingOffers.Clear();
		_pendingOffers.AddRange(PickRandomRelics(_pool.ChoicesPerPick > 0 ? _pool.ChoicesPerPick : 3));
		_combat.SetRunPaused(true);
		_eventBus.EmitRunRelicPickOffered();
		OpenRelicPickUi();
	}

	private void OpenRelicPickUi()
	{
		var tree = GetTree();
		if (tree == null)
		{
			return;
		}

		var popup = tree.Root.GetNodeOrNull<Node>("GameRoot/PopupManager");
		popup?.Call("open_run_relic_pick");
	}

	private List<string> PickRandomRelics(int count)
	{
		var picks = new List<string>();
		var pool = _pool.Relics.ToList();
		if (pool.Count == 0)
		{
			return picks;
		}

		for (var i = 0; i < count; i++)
		{
			var idx = _rng.RandiRange(0, pool.Count - 1);
			picks.Add(pool[idx].Id);
		}

		return picks;
	}

	private void ApplyRelicEffect(RunRelicDefinition def)
	{
		if (def.Effect == null)
		{
			return;
		}

		if (def.Effect.Type == "InstantHealPercent" && def.Effect.Value > 0f)
		{
			var ratio = def.Effect.Value / 100f;
			foreach (var ally in _combat.Allies)
			{
				if (ally.CurrentHp <= 0f)
				{
					continue;
				}

				ally.CurrentHp = Mathf.Min(ally.MaxHp, ally.CurrentHp + ally.MaxHp * ratio);
				_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			}
		}
	}

	private static void AccumulateEffect(
		RunRelicEffectEntry effect,
		ref float damage,
		ref float maxHp,
		ref float moveSpeed,
		ref float crit,
		ref float gold)
	{
		switch (effect.Type)
		{
			case "TeamDamagePercent":
				damage += effect.Value;
				break;
			case "TeamMaxHpPercent":
				maxHp += effect.Value;
				break;
			case "TeamMoveSpeedPercent":
				moveSpeed += effect.Value;
				break;
			case "TeamCritPercent":
				crit += effect.Value;
				break;
			case "RunGoldBonusPercent":
				gold += effect.Value;
				break;
		}
	}

	private RunRelicDefinition? FindRelic(string relicId) =>
		_pool.Relics.FirstOrDefault(r => r.Id == relicId);

	private void LoadPool()
	{
		if (!FileAccess.FileExists(RelicPoolPath))
		{
			GD.PushWarning($"Run relic pool not found: {RelicPoolPath}");
			return;
		}

		using var file = FileAccess.Open(RelicPoolPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		_pool = JsonSerializer.Deserialize<RunRelicPoolTable>(file.GetAsText(), options) ?? new RunRelicPoolTable();
	}

	private sealed class RunRelicPoolTable
	{
		public int ChoicesPerPick { get; set; } = 3;
		public List<RunRelicDefinition> Relics { get; set; } = new();
	}

	private sealed class RunRelicDefinition
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Desc { get; set; } = string.Empty;
		public RunRelicEffectEntry? Effect { get; set; }
	}

	private sealed class RunRelicEffectEntry
	{
		public string Type { get; set; } = string.Empty;
		public float Value { get; set; }
		public string SecondaryType { get; set; } = string.Empty;
		public float SecondaryValue { get; set; }
	}
}
