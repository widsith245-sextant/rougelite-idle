using System.Collections.Generic;
using System.Linq;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;
using RougeliteIdle.Meta;
using RougeliteIdle.Run;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public sealed class StageRunController
{
	private const float WaveClearDelay = 0.35f;

	private readonly StageDefinition _stage;
	private readonly BattlefieldController _battlefield;
	private readonly EventBus _eventBus;
	private readonly Dictionary<string, float> _initialAllyX = new();

	private float _runProgress;
	private int _currentWaveIndex;
	private float _waveClearTimer;
	private bool _marchStateEmitted;
	private int _remainingInWave;
	private WaveDefinition? _activeWave;
	private bool _clusterWaveActive;

	public StageRunState State { get; private set; } = StageRunState.Marching;

	public float RunProgress => _runProgress;
	public int CurrentWaveIndex => _currentWaveIndex;
	public int RemainingInWave => _remainingInWave;
	public int WaveTotal => _stage.Waves.Count;
	public bool IsEngaging => State == StageRunState.Engaging;

	public void RestoreRunState(StageRunState state, float runProgress, int waveIndex, int remainingInWave)
	{
		State = state;
		_runProgress = runProgress;
		_currentWaveIndex = waveIndex;
		_remainingInWave = remainingInWave;
		_marchStateEmitted = state != StageRunState.Marching;
		EmitMarchState(state == StageRunState.Marching);
		EmitProgress();
	}

	public StageRunController(StageDefinition stage, BattlefieldController battlefield, EventBus eventBus)
	{
		_stage = stage;
		_battlefield = battlefield;
		_eventBus = eventBus;
	}

	public void CacheInitialFormation(IReadOnlyList<CombatUnitData> allies)
	{
		_initialAllyX.Clear();
		foreach (var ally in allies)
		{
			_initialAllyX[ally.Id] = ally.PositionX;
		}
	}

	public void BeginRun()
	{
		_runProgress = 0f;
		_currentWaveIndex = 0;
		State = StageRunState.Marching;
		EmitMarchState(true);
		EmitProgress();
	}

	public void Tick(float dt, CombatManager combat)
	{
		switch (State)
		{
			case StageRunState.Marching:
				TickMarching(dt, combat);
				break;
			case StageRunState.WaveClearing:
				TickWaveClearing(dt, combat);
				break;
		}
	}

	private void TickMarching(float dt, CombatManager combat)
	{
		if (_stage.StageLength <= 0f)
		{
			return;
		}

		_runProgress += _stage.MarchSpeed * dt / _stage.StageLength;
		EmitProgress();

		if (_currentWaveIndex >= _stage.Waves.Count)
		{
			if (_runProgress >= 1f)
			{
				CompleteStage(combat);
			}

			return;
		}

		var wave = _stage.Waves[_currentWaveIndex];
		if (_runProgress < wave.Progress)
		{
			TickAllyMarch(dt, combat);
			return;
		}

		SpawnWave(combat, wave);
	}

	private void TickAllyMarch(float dt, CombatManager combat)
	{
		foreach (var ally in combat.Allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			var oldX = ally.PositionX;
			_battlefield.MarchRight(ally, _stage.MarchSpeed, dt);
			combat.EmitPositionChange(ally, oldX);
		}
	}

	private void SpawnWave(CombatManager combat, WaveDefinition wave)
	{
		_activeWave = wave;
		_clusterWaveActive = wave.IsCluster;
		var statMul = ResolveEnemyStatMultiplier(combat);
		var enemies = new List<CombatUnitData>();
		foreach (var (enemyId, offset, instanceIndex) in wave.EnumerateSpawns())
		{
			var enemy = EnemyTemplateLoader.CreateEnemy(enemyId, offset, instanceIndex, _stage.StageLevel, statMul);
			if (enemy != null)
			{
				enemies.Add(enemy);
			}
		}

		if (enemies.Count == 0)
		{
			_currentWaveIndex++;
			return;
		}

		if (_clusterWaveActive)
		{
			_remainingInWave = enemies.Count;
			combat.SpawnEnemyCluster(enemies);
		}
		else
		{
			_remainingInWave = enemies.Count;
			combat.SpawnEnemy(enemies[0], clearExisting: true);
		}

		State = StageRunState.Engaging;
		EmitMarchState(false);
		_eventBus.EmitWaveStarted(_currentWaveIndex);
		_eventBus.EmitCombatBroadcast($"第 {_currentWaveIndex + 1} 波来袭", "wave");
		LogSpawn(wave, enemies.Count);
	}

	public void OnEnemyKilled(CombatManager combat, CombatUnitData enemy)
	{
		if (State != StageRunState.Engaging || _activeWave == null)
		{
			return;
		}

		var templateId = string.IsNullOrEmpty(enemy.TemplateId)
			? EnemyTemplateLoader.ResolveTemplateId(enemy.Id)
			: enemy.TemplateId;
		var prog = combat.GetProgressionManager();
		var loot = combat.GetLootManager();
		_eventBus.EmitCombatBroadcast($"击败 {enemy.DisplayName}", "kill");
		prog?.GrantKillReward(templateId, EnemyTemplateLoader.GetEnemyLevel(templateId), loot);

		combat.RemoveEnemy(enemy);
		_remainingInWave--;

		if (_clusterWaveActive)
		{
			if (_remainingInWave > 0 && combat.HasLivingEnemies())
			{
				return;
			}

			if (_remainingInWave > 0)
			{
				return;
			}
		}
		else if (_remainingInWave > 0)
		{
			var spawns = _activeWave.EnumerateSpawns().ToList();
			var nextIndex = spawns.Count - _remainingInWave;
			if (nextIndex >= 0 && nextIndex < spawns.Count)
			{
				var (enemyId, offset, instanceIndex) = spawns[nextIndex];
				var next = EnemyTemplateLoader.CreateEnemy(enemyId, offset, instanceIndex, _stage.StageLevel, ResolveEnemyStatMultiplier(combat));
				if (next != null)
				{
					combat.SpawnEnemy(next, clearExisting: true);
				}
			}

			return;
		}

		State = StageRunState.WaveClearing;
		_waveClearTimer = WaveClearDelay;
		combat.ClearAllEnemies();
		_eventBus.EmitWaveCleared(_currentWaveIndex);
		_eventBus.EmitCombatBroadcast("波次清空", "wave");
		_currentWaveIndex++;
		_activeWave = null;
	}

	private void TickWaveClearing(float dt, CombatManager combat)
	{
		_waveClearTimer -= dt;
		if (_waveClearTimer > 0f)
		{
			return;
		}

		RestoreFormation(combat);
		if (_currentWaveIndex >= _stage.Waves.Count && _runProgress >= 1f)
		{
			CompleteStage(combat);
			return;
		}

		State = StageRunState.Marching;
		EmitMarchState(true);
		EmitProgress();
	}

	private void RestoreFormation(CombatManager combat)
	{
		foreach (var ally in combat.Allies)
		{
			var oldX = ally.PositionX;
			if (_initialAllyX.TryGetValue(ally.Id, out var x))
			{
				ally.PositionX = x;
			}

			ally.CombatState = UnitCombatState.Idle;
			combat.EmitPositionChange(ally, oldX);
		}
	}

	private void CompleteStage(CombatManager combat)
	{
		if (combat.RunRogueliteActive)
		{
			combat.GetNodeOrNull<EventBus>("/root/EventBus")?.EmitStageRunCompleted(_stage.StageId);
			_runProgress = 0f;
			_currentWaveIndex = 0;
			State = StageRunState.Marching;
			EmitMarchState(true);
			EmitProgress();
			return;
		}

		var prog = combat.GetProgressionManager();
		prog?.GrantStageComplete(_stage.StageId, _stage.StageLevel);
		combat.RestoreAllAlliesHpToMax(prog?.GetStageClearHealPercent() ?? 1f);
		_runProgress = 0f;
		_currentWaveIndex = 0;
		State = StageRunState.Marching;
		EmitMarchState(true);
		EmitProgress();
	}

	private static float ResolveEnemyStatMultiplier(CombatManager combat)
	{
		if (!combat.RunRogueliteActive)
		{
			return 1f;
		}

		var tree = combat.GetTree();
		var run = tree?.Root.GetNodeOrNull<RunSessionManager>("/root/RunSessionManager");
		return run?.GetEnemyStatMultiplier() ?? 12f;
	}

	private void EmitProgress()
	{
		_eventBus.EmitRunProgressChanged(_runProgress, _currentWaveIndex, WaveTotal);
	}

	private void EmitMarchState(bool isMarching)
	{
		if (_marchStateEmitted == isMarching && isMarching)
		{
			return;
		}

		_marchStateEmitted = isMarching;
		_eventBus.EmitMarchStateChanged(isMarching);
	}

	private static void LogSpawn(WaveDefinition wave, int count)
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		tree?.Root.GetNodeOrNull<GameLogger>("/root/GameLogger")?.LogCombat(
			$"SpawnWave mode={wave.SpawnMode} count={count} cluster={wave.IsCluster}");
	}
}
