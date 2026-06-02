using System.Collections.Generic;
using System.Linq;
using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;
using RougeliteIdle.Meta;
using RougeliteIdle.Run;
using RougeliteIdle.Save;
using RougeliteIdle.Stats;

namespace RougeliteIdle.Combat;

public partial class CombatManager : Node
{
	public const bool TrainingMode = true;

	/// <summary>When true, stage clear / party wipe are handled by RunSessionManager.</summary>
	public bool RunRogueliteActive { get; set; }

	public bool RunPaused { get; private set; }

	public void SetRunPaused(bool paused) => RunPaused = paused;

	private const float ActionResolveDelay = 0.35f;
	private const float RepositionSettleTime = 0.15f;
	private const float PositionEmitEpsilon = 0.01f;

	private readonly ActionQueue _actionQueue = new();
	private readonly BattlefieldController _battlefield = new();
	private readonly List<CombatUnitData> _allies = new();
	private readonly List<CombatUnitData> _enemies = new();
	private readonly List<CombatUnitData> _allUnits = new();

	private CombatActionExecutor _executor = null!;
	private EventBus _eventBus = null!;
	private StatsService _statsService = null!;
	private MetaManager _meta = null!;
	private LootManager _loot = null!;
	private ProgressionManager _progression = null!;
	private StageRunController? _stageRun;
	private CombatUnitData? _pendingActor;
	private float _resolveTimer;
	private float _repositionTimer;
	private bool _isResolving;
	private readonly HashSet<string> _processingUnitIds = new();
	private readonly Dictionary<string, float> _lastEmittedX = new();
	private string _activeEnemyTemplateId = string.Empty;
	private string _currentStageId = "chapter_01_level_01";
	private CombatSaveDto? _pendingCombatSave;
	private bool _partyWipeHandled;

	public IReadOnlyList<CombatUnitData> Allies => _allies;
	public IReadOnlyList<CombatUnitData> Enemies => _enemies;
	public BattlefieldController Battlefield => _battlefield;
	public StageRunController? StageRun => _stageRun;
	public bool IsMarching => _stageRun?.State == StageRunState.Marching;

	public bool IsEngaging => _stageRun?.State == StageRunState.Engaging;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_statsService = GetNode<StatsService>("/root/StatsService");
		_meta = GetNode<MetaManager>("/root/MetaManager");
		_loot = GetNode<LootManager>("/root/LootManager");
		_progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		_executor = new CombatActionExecutor(_eventBus);
		_eventBus.SquadChanged += OnSquadChanged;
	}

	public void SetPendingCombatSave(CombatSaveDto? save) => _pendingCombatSave = save;

	public void StartEncounterFromSaveOrDefault()
	{
		if (_pendingCombatSave != null)
		{
			_currentStageId = string.IsNullOrEmpty(_pendingCombatSave.StageId)
				? _currentStageId
				: _pendingCombatSave.StageId;

			if (IsSavePartyWiped(_pendingCombatSave))
			{
				_pendingCombatSave = null;
			}
		}

		StartEncounter();
		if (_pendingCombatSave != null)
		{
			ApplyCombatSave(_pendingCombatSave);
			_pendingCombatSave = null;
		}
	}

	private void OnSquadChanged()
	{
		ResyncPartyFromRoster();
	}

	public void ResyncPartyFromRoster()
	{
		var saved = new Dictionary<string, (float Hp, float X, float Gauge)>();
		foreach (var ally in _allies)
		{
			saved[ally.Id] = (ally.CurrentHp, ally.PositionX, ally.ActionGauge);
		}

		_allies.Clear();
		_allies.AddRange(EncounterTableLoader.BuildActiveAllies());

		foreach (var ally in _allies)
		{
			if (!saved.TryGetValue(ally.Id, out var snap))
			{
				continue;
			}

			ally.Stats = _statsService.GetOrCreate(ally);
			ally.MaxHp = ally.Stats.GetFinal(StatId.MaxHp);
			ally.CurrentHp = Mathf.Clamp(snap.Hp, 0f, ally.MaxHp);
			ally.PositionX = snap.X;
			ally.ActionGauge = snap.Gauge;
		}

		_allUnits.RemoveAll(u => u.IsAlly);
		_allUnits.AddRange(_allies);
		_battlefield.RegisterUnits(_allies, _enemies);
		_statsService.RebuildAll(_allUnits);

		if (_stageRun != null)
		{
			_stageRun.CacheInitialFormation(_allies);
		}

		foreach (var ally in _allies)
		{
			_lastEmittedX[ally.Id] = ally.PositionX;
			_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
			_eventBus.EmitStatsChanged(ally.Id);
		}

		GetNodeOrNull<GameLogger>("/root/GameLogger")?.LogCombat("ResyncPartyFromRoster allies=" + _allies.Count);
	}

	public void ReloadEncounter(string tablePath = "")
	{
		var path = string.IsNullOrEmpty(tablePath)
			? "res://data/tables/combat/encounter_default.json"
			: tablePath;
		var (allies, enemies) = EncounterTableLoader.LoadEncounter(path);
		allies = EncounterTableLoader.FilterActiveAllies(allies);
		ApplyEncounter(allies, TrainingMode ? new List<CombatUnitData>() : enemies);
	}

	public void StartEncounter()
	{
		var (allies, enemies) = EncounterTableLoader.LoadDefaultEncounter();
		allies = EncounterTableLoader.FilterActiveAllies(allies);
		ApplyEncounter(allies, TrainingMode ? new List<CombatUnitData>() : enemies);
	}

	public void SetStageId(string stageId)
	{
		if (string.IsNullOrEmpty(stageId))
		{
			return;
		}

		_currentStageId = stageId;
		_partyWipeHandled = false;
		StartEncounter();
	}

	public void RestartCurrentStage()
	{
		_partyWipeHandled = false;
		_isResolving = false;
		_pendingActor = null;
		_actionQueue.Clear();
		_processingUnitIds.Clear();

		var (allies, enemies) = EncounterTableLoader.LoadDefaultEncounter();
		allies = EncounterTableLoader.FilterActiveAllies(allies);
		ApplyEncounter(allies, TrainingMode ? new List<CombatUnitData>() : enemies);

		var bootstrap = GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap");
		bootstrap?.RequestSave();
	}

	private void ApplyEncounter(List<CombatUnitData> allies, List<CombatUnitData> enemies)
	{
		_allies.Clear();
		_allies.AddRange(allies);
		_enemies.Clear();
		_enemies.AddRange(enemies);
		_allUnits.Clear();
		_allUnits.AddRange(_allies);
		_allUnits.AddRange(_enemies);

		_battlefield.RegisterUnits(_allies, _enemies);
		_statsService.RebuildAll(_allUnits);
		_actionQueue.Clear();
		_isResolving = false;
		_pendingActor = null;
		_resolveTimer = 0f;
		_repositionTimer = 0f;
		_processingUnitIds.Clear();
		_lastEmittedX.Clear();
		_activeEnemyTemplateId = string.Empty;

		foreach (var unit in _allUnits)
		{
			unit.ActionGauge = 0f;
			unit.NormalAttackTimer = 0f;
			unit.StunTimer = 0f;
			unit.Stats = _statsService.GetOrCreate(unit);
			unit.MaxHp = unit.Stats.GetFinal(StatId.MaxHp);
			unit.CurrentHp = unit.MaxHp;
			_lastEmittedX[unit.Id] = unit.PositionX;
			_eventBus.EmitUnitHpChanged(unit.Id, unit.CurrentHp, unit.MaxHp);
			_eventBus.EmitStatsChanged(unit.Id);
		}

		if (TrainingMode)
		{
			var stage = StageTableLoader.LoadByStageId(_currentStageId);
			_currentStageId = stage.StageId;
			_stageRun = new StageRunController(stage, _battlefield, _eventBus);
			_stageRun.CacheInitialFormation(_allies);
			_stageRun.BeginRun();
		}
		else
		{
			_stageRun = null;
		}

		_eventBus.EmitCombatStateChanged(1);
	}

	public override void _Process(double delta)
	{
		if (RunPaused)
		{
			return;
		}

		var dt = (float)delta;
		CheckPartyWipe();

		if (TrainingMode && _stageRun != null)
		{
			_stageRun.Tick(dt, this);

			if (_stageRun.State == StageRunState.Marching)
			{
				return;
			}

			if (_stageRun.State == StageRunState.WaveClearing)
			{
				return;
			}
		}

		var livingEnemies = GetLivingEnemies();
		if (livingEnemies.Count > 0)
		{
			_executor.Effects.Tick(dt, _allUnits, _executor);
			foreach (var enemy in livingEnemies)
			{
				TickHybridCombat(dt, enemy);
				CheckEnemyDeath(enemy);
			}
		}

		if (_isResolving)
		{
			_resolveTimer -= dt;
			if (_resolveTimer <= 0f && _pendingActor != null)
			{
				FinishSkillTurn(_pendingActor);
				_pendingActor = null;
				_isResolving = false;
			}

			return;
		}

		TickSkillGauges(dt);
		EnqueueReadyUnits();

		var next = _actionQueue.Dequeue();
		if (next == null)
		{
			return;
		}

		_isResolving = true;
		_resolveTimer = ActionResolveDelay;
		_pendingActor = next;
		_processingUnitIds.Add(next.Id);
		next.CombatState = UnitCombatState.Casting;
		_eventBus.EmitCombatActionStarted(next.Id);
	}

	private void CheckEnemyDeath(CombatUnitData enemy)
	{
		if (enemy.CurrentHp > 0f || _stageRun == null || _stageRun.State != StageRunState.Engaging)
		{
			return;
		}

		_stageRun.OnEnemyKilled(this, enemy);
	}

	public bool HasLivingEnemies() => _enemies.Any(e => e.CurrentHp > 0f);

	public List<CombatUnitData> GetLivingEnemies()
	{
		var result = new List<CombatUnitData>();
		foreach (var enemy in _enemies)
		{
			if (enemy.CurrentHp > 0f)
			{
				result.Add(enemy);
			}
		}

		return result;
	}

	public void SpawnEnemyCluster(IReadOnlyList<CombatUnitData> enemies)
	{
		_enemies.Clear();
		foreach (var enemy in enemies)
		{
			AddEnemyInternal(enemy);
		}

		_battlefield.RegisterUnits(_allies, _enemies);
		_activeEnemyTemplateId = enemies.Count > 0 ? enemies[0].TemplateId : string.Empty;
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.LogCombat($"SpawnEnemyCluster count={enemies.Count}");
		_eventBus.EmitCombatStateChanged(2);
	}

	public void SpawnEnemy(CombatUnitData enemy, bool clearExisting = true)
	{
		if (clearExisting)
		{
			_enemies.Clear();
		}

		AddEnemyInternal(enemy);
		_battlefield.RegisterUnits(_allies, _enemies);
		_activeEnemyTemplateId = enemy.TemplateId;
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.LogCombat($"SpawnEnemy id={enemy.Id} template={enemy.TemplateId}");
		_eventBus.EmitCombatStateChanged(2);
	}

	private void AddEnemyInternal(CombatUnitData enemy)
	{
		if (!_enemies.Contains(enemy))
		{
			_enemies.Add(enemy);
		}

		if (!_allUnits.Contains(enemy))
		{
			_allUnits.Add(enemy);
		}

		enemy.Stats = _statsService.GetOrCreate(enemy);
		enemy.MaxHp = enemy.Stats.GetFinal(StatId.MaxHp);
		if (enemy.CurrentHp <= 0f || enemy.CurrentHp > enemy.MaxHp)
		{
			enemy.CurrentHp = enemy.MaxHp;
		}

		_eventBus.EmitUnitHpChanged(enemy.Id, enemy.CurrentHp, enemy.MaxHp);
	}

	public void RemoveEnemy(CombatUnitData enemy)
	{
		_enemies.Remove(enemy);
		_allUnits.Remove(enemy);
		_eventBus.EmitUnitHpChanged(enemy.Id, 0f, enemy.MaxHp);
	}

	public void ClearAllEnemies()
	{
		foreach (var enemy in _enemies.ToList())
		{
			_allUnits.Remove(enemy);
		}

		_enemies.Clear();
		_activeEnemyTemplateId = string.Empty;
		_eventBus.EmitCombatStateChanged(1);
	}

	public void DespawnActiveEnemy() => ClearAllEnemies();

	public Godot.Collections.Array GetEnemySnapshot()
	{
		var result = new Godot.Collections.Array();
		foreach (var enemy in _enemies)
		{
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", enemy.Id },
				{ "template_id", enemy.TemplateId },
				{ "position_x", enemy.PositionX },
				{ "current_hp", enemy.CurrentHp },
				{ "max_hp", enemy.MaxHp },
			});
		}

		return result;
	}

	public void EmitPositionChange(CombatUnitData unit, float oldX)
	{
		EmitPositionIfChanged(unit, oldX);
	}

	public ProgressionManager? GetProgressionManager() => _progression;

	public LootManager? GetLootManager() => _loot;

	private void TickHybridCombat(float dt, CombatUnitData focusEnemy)
	{
		foreach (var ally in _allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			var target = GetTargetEnemyForAlly(ally) ?? focusEnemy;
			if (target == null || target.CurrentHp <= 0f)
			{
				continue;
			}

			var oldX = ally.PositionX;
			UnitCombatAI.Tick(ally, target, _battlefield, dt);
			EmitPositionIfChanged(ally, oldX);

			if (_processingUnitIds.Contains(ally.Id))
			{
				continue;
			}

			if (UnitCombatAI.TickNormalAttack(ally, target, _battlefield, dt, out var isCrit))
			{
				var damage = DamageFormula.CalculateFromStats(
					ally.Stats,
					_meta.GlobalStatBonusPercent,
					0f,
					1f,
					null,
					out isCrit,
					ally.DamageType);
				_executor.ApplyDamage(ally, target, damage, isCrit);
				_executor.Effects.TryApplyOnHitEffects(ally, target, ally.OnHitEffects);
				ally.CombatState = UnitCombatState.InRange;
				CheckEnemyDeath(target);
			}
		}

		if (focusEnemy != null && focusEnemy.CurrentHp > 0f && ShouldRunEngagementMovement())
		{
			var frontAlly = _battlefield.GetFrontAlly(_allies);
			if (frontAlly != null && frontAlly.CurrentHp > 0f)
			{
				var oldEnemyX = focusEnemy.PositionX;
				UnitCombatAI.Tick(focusEnemy, frontAlly, _battlefield, dt);
				EmitPositionIfChanged(focusEnemy, oldEnemyX);
			}
		}

		if (_repositionTimer > 0f)
		{
			_repositionTimer -= dt;
			return;
		}

		foreach (var ally in _allies)
		{
			if (ally.CombatState == UnitCombatState.Reposition)
			{
				ally.CombatState = UnitCombatState.InRange;
			}
		}
	}

	private bool ShouldRunEngagementMovement() =>
		_stageRun == null || _stageRun.IsEngaging;

	private void EmitPositionIfChanged(CombatUnitData unit, float oldX)
	{
		if (System.Math.Abs(oldX - unit.PositionX) < PositionEmitEpsilon)
		{
			return;
		}

		_eventBus.EmitPositionChanged(unit.Id, oldX, unit.PositionX);
		_lastEmittedX[unit.Id] = unit.PositionX;
	}

	private void TickSkillGauges(float dt)
	{
		foreach (var unit in _allUnits)
		{
			if (unit.CurrentHp <= 0f || unit.IsBlockingOutput)
			{
				continue;
			}

			if (_enemies.Count == 0 && !unit.IsAlly)
			{
				continue;
			}

			ActionGauge.Tick(unit, dt);
		}
	}

	private void EnqueueReadyUnits()
	{
		if (_stageRun != null && _stageRun.State != StageRunState.Engaging)
		{
			return;
		}

		var enemy = GetFrontEnemy();
		foreach (var unit in _allUnits)
		{
			if (unit.CurrentHp <= 0f || unit.IsBlockingOutput)
			{
				continue;
			}

			if (_enemies.Count == 0 && !unit.IsAlly)
			{
				continue;
			}

			if (_processingUnitIds.Contains(unit.Id))
			{
				continue;
			}

			if (!CanQueueSkillTurn(unit, unit.IsAlly ? GetTargetEnemyForAlly(unit) : enemy))
			{
				continue;
			}

			if (ActionGauge.IsReady(unit))
			{
				_actionQueue.TryEnqueue(unit);
			}
		}
	}

	private bool CanQueueSkillTurn(CombatUnitData unit, CombatUnitData? enemy)
	{
		if (unit.IsAlly)
		{
			if (enemy == null || enemy.CurrentHp <= 0f)
			{
				return false;
			}

			return _battlefield.IsInAttackRange(unit, enemy);
		}

		var front = _battlefield.GetFrontAlly(_allies);
		return front != null && front.CurrentHp > 0f && _battlefield.IsInAttackRange(unit, front);
	}

	private void FinishSkillTurn(CombatUnitData actor)
	{
		var enemy = actor.IsAlly ? GetTargetEnemyForAlly(actor) : GetFrontEnemy();
		var consumed = _executor.ExecuteTurn(actor, _battlefield, _allies, enemy, _meta.GlobalStatBonusPercent);
		if (consumed)
		{
			ActionGauge.Reset(actor);
		}

		_processingUnitIds.Remove(actor.Id);
		_repositionTimer = RepositionSettleTime;
		_statsService.Invalidate(actor.Id);
		actor.Stats = _statsService.GetOrCreate(actor);

		if (enemy != null)
		{
			CheckEnemyDeath(enemy);
		}

		foreach (var living in GetLivingEnemies())
		{
			CheckEnemyDeath(living);
		}
	}

	private CombatUnitData? GetFrontEnemy()
	{
		CombatUnitData? best = null;
		var bestX = float.MaxValue;
		foreach (var enemy in GetLivingEnemies())
		{
			if (enemy.PositionX < bestX)
			{
				bestX = enemy.PositionX;
				best = enemy;
			}
		}

		return best;
	}

	private CombatUnitData? GetTargetEnemyForAlly(CombatUnitData ally)
	{
		CombatUnitData? best = null;
		var bestDist = float.MaxValue;
		foreach (var enemy in GetLivingEnemies())
		{
			var dist = System.Math.Abs(enemy.PositionX - ally.PositionX);
			if (dist < bestDist)
			{
				bestDist = dist;
				best = enemy;
			}
		}

		return best;
	}

	private CombatUnitData? GetPrimaryEnemy() => GetFrontEnemy();

	private CombatUnitData? FindUnit(string unitId)
	{
		foreach (var unit in _allUnits)
		{
			if (unit.Id == unitId)
			{
				return unit;
			}
		}

		return null;
	}

	public float GetAllyHpRatio()
	{
		var current = 0f;
		var max = 0f;
		foreach (var ally in _allies)
		{
			current += ally.CurrentHp;
			max += ally.MaxHp;
		}

		return max > 0f ? current / max : 0f;
	}

	public float GetEnemyHpRatio()
	{
		var enemy = GetPrimaryEnemy();
		if (enemy == null || enemy.MaxHp <= 0f)
		{
			return 0f;
		}

		return enemy.CurrentHp / enemy.MaxHp;
	}

	public Godot.Collections.Dictionary? GetUnitHp(string unitId)
	{
		var unit = FindUnit(unitId);
		if (unit == null)
		{
			return null;
		}

		return new Godot.Collections.Dictionary
		{
			{ "current", unit.CurrentHp },
			{ "max", unit.MaxHp },
		};
	}

	public Godot.Collections.Dictionary? GetUnitStatsSnapshot(string unitId) =>
		_statsService.GetSnapshot(unitId);

	public float GetFrontAllyX()
	{
		var front = _battlefield.GetFrontAlly(_allies);
		return front?.PositionX ?? 0f;
	}

	public Godot.Collections.Dictionary GetAllyFormationBounds()
	{
		var minX = float.MaxValue;
		var maxX = float.MinValue;
		foreach (var ally in _allies)
		{
			if (ally.CurrentHp <= 0f)
			{
				continue;
			}

			minX = System.Math.Min(minX, ally.PositionX);
			maxX = System.Math.Max(maxX, ally.PositionX);
		}

		if (minX == float.MaxValue)
		{
			minX = 40f;
			maxX = 104f;
		}

		return new Godot.Collections.Dictionary
		{
			{ "min_x", minX },
			{ "max_x", maxX },
		};
	}

	public Godot.Collections.Dictionary? GetUnitBattleDisplay(string unitId)
	{
		var unit = FindUnit(unitId);
		if (unit == null)
		{
			return null;
		}

		return new Godot.Collections.Dictionary
		{
			{ "id", unit.Id },
			{ "display_name", unit.DisplayName },
			{ "class_id", unit.ClassId },
			{ "skill_id", unit.ActiveSkill?.Id ?? string.Empty },
		};
	}

	public Godot.Collections.Array GetAllySnapshot()
	{
		var party = GetNodeOrNull<PartyManager>("/root/PartyManager");
		var result = new Godot.Collections.Array();
		foreach (var ally in _allies)
		{
			var slotIndex = EncounterTableLoader.ResolveSlotIndexPublic(ally.Id);
			var rosterId = party?.GetRosterIdForSlot(slotIndex) ?? string.Empty;
			result.Add(new Godot.Collections.Dictionary
			{
				{ "id", ally.Id },
				{ "roster_id", rosterId },
				{ "slot_index", slotIndex },
				{ "formation_index", ally.FormationIndex },
				{ "position_x", ally.PositionX },
				{ "class_id", ally.ClassId },
			});
		}

		return result;
	}

	public Godot.Collections.Dictionary? GetUnitGaugeSnapshot(string unitId)
	{
		var unit = FindUnit(unitId);
		if (unit == null)
		{
			return null;
		}

		return new Godot.Collections.Dictionary
		{
			{ "unit_id", unit.Id },
			{ "gauge", unit.ActionGauge },
			{ "max", ActionGauge.GaugeMax },
			{ "skill_id", unit.ActiveSkill?.Id ?? string.Empty },
		};
	}

	public string GetCurrentStageId() => _currentStageId;

	private void CheckPartyWipe()
	{
		if (_allies.Count == 0)
		{
			return;
		}

		if (_allies.Any(a => a.CurrentHp > 0f))
		{
			_partyWipeHandled = false;
			return;
		}

		if (_partyWipeHandled)
		{
			return;
		}

		_partyWipeHandled = true;
		if (RunRogueliteActive)
		{
			GetNodeOrNull<Run.RunSessionManager>("/root/RunSessionManager")?.FailRun();
			return;
		}

		RestartCurrentStage();
	}

	private static bool IsSavePartyWiped(CombatSaveDto save) =>
		save.Allies.Count > 0 && save.Allies.All(a => a.CurrentHp <= 0f);

	public CombatSaveDto? ExportCombatSave()
	{
		if (_allies.Count > 0 && _allies.All(a => a.CurrentHp <= 0f))
		{
			return new CombatSaveDto { StageId = _currentStageId };
		}

		var save = new CombatSaveDto
		{
			StageId = _currentStageId,
			ActiveEnemyTemplateId = _activeEnemyTemplateId,
		};

		if (_stageRun != null)
		{
			save.StageRunState = (int)_stageRun.State;
			save.RunProgress = _stageRun.RunProgress;
			save.CurrentWaveIndex = _stageRun.CurrentWaveIndex;
			save.RemainingInWave = _stageRun.RemainingInWave;
		}

		foreach (var ally in _allies)
		{
			save.Allies.Add(new UnitCombatSaveDto
			{
				Id = ally.Id,
				CurrentHp = ally.CurrentHp,
				MaxHp = ally.MaxHp,
				PositionX = ally.PositionX,
				ActionGauge = ally.ActionGauge,
			});
		}

		var enemy = GetPrimaryEnemy();
		if (enemy != null)
		{
			save.Enemy = new UnitCombatSaveDto
			{
				Id = enemy.Id,
				CurrentHp = enemy.CurrentHp,
				MaxHp = enemy.MaxHp,
				PositionX = enemy.PositionX,
				ActionGauge = enemy.ActionGauge,
			};
		}

		return save;
	}

	private void ApplyCombatSave(CombatSaveDto save)
	{
		if (IsSavePartyWiped(save))
		{
			return;
		}

		if (save.Allies.Count > 0)
		{
			foreach (var allySave in save.Allies)
			{
				foreach (var ally in _allies)
				{
					if (ally.Id != allySave.Id)
					{
						continue;
					}

					var oldX = ally.PositionX;
					ally.CurrentHp = Mathf.Clamp(allySave.CurrentHp, 0f, ally.MaxHp);
					ally.PositionX = allySave.PositionX;
					ally.ActionGauge = allySave.ActionGauge;
					_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
					EmitPositionIfChanged(ally, oldX);
					break;
				}
			}
		}

		if (_stageRun != null)
		{
			_stageRun.RestoreRunState(
				(StageRunState)save.StageRunState,
				save.RunProgress,
				save.CurrentWaveIndex,
				save.RemainingInWave);
		}

		if (save.Enemy != null && !string.IsNullOrEmpty(save.ActiveEnemyTemplateId))
		{
			var enemy = EnemyTemplateLoader.CreateEnemy(save.ActiveEnemyTemplateId);
			if (enemy != null)
			{
				enemy.CurrentHp = Mathf.Clamp(save.Enemy.CurrentHp, 0f, enemy.MaxHp);
				enemy.PositionX = save.Enemy.PositionX;
				enemy.ActionGauge = save.Enemy.ActionGauge;
				SpawnEnemy(enemy, clearExisting: true);
			}
		}
	}

	public Godot.Collections.Array GetCurrentStageWaveProgresses() =>
		StageTableLoader.GetWaveProgressArray(_currentStageId);

	public string GetActiveEnemyId() => _activeEnemyTemplateId;

	public void RestoreAllAlliesHpToMax(float ratio = 1f)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		foreach (var ally in _allies)
		{
			ally.CurrentHp = ally.MaxHp * ratio;
			_eventBus.EmitUnitHpChanged(ally.Id, ally.CurrentHp, ally.MaxHp);
		}
	}
}
