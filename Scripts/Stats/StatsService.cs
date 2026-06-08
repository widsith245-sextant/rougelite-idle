using System.Collections.Generic;
using Godot;
using RougeliteIdle.Combat;
using RougeliteIdle.Core;
using RougeliteIdle.Core.Enums;
using RougeliteIdle.Loot;
using RougeliteIdle.Meta;
using RougeliteIdle.Run;

namespace RougeliteIdle.Stats;

public partial class StatsService : Node
{
	private static readonly StatId[] CompareStatIds =
	{
		StatId.Level,
		StatId.MaxHp,
		StatId.Dps,
		StatId.Damage,
		StatId.AtkSpeed,
		StatId.AtkRange,
		StatId.MoveSpeed,
		StatId.CritRate,
	};

	private readonly Dictionary<string, UnitStats> _unitStats = new();
	private LootManager _loot = null!;
	private MetaManager _meta = null!;
	private EventBus _eventBus = null!;
	private DbManager? _db;

	public override void _Ready()
	{
		_loot = GetNode<LootManager>("/root/LootManager");
		_meta = GetNode<MetaManager>("/root/MetaManager");
		_eventBus = GetNode<EventBus>("/root/EventBus");
		_db = GetNodeOrNull<DbManager>("/root/DbManager");
		_eventBus.EquipmentChanged += OnEquipmentChanged;
		_eventBus.DbNodeUnlocked += _ => InvalidateAllAllies();
		_eventBus.RosterLevelChanged += OnRosterLevelChanged;
	}

	private void OnRosterLevelChanged(string rosterId)
	{
		var party = GetPartyManager();
		if (party == null)
		{
			return;
		}

		for (var i = 0; i < 3; i++)
		{
			if (party.GetRosterIdForSlot(i) != rosterId)
			{
				continue;
			}

			var unitId = party.GetUnitIdForSlot(i);
			Invalidate(unitId);
			_eventBus.EmitStatsChanged(unitId);
		}
	}

	private void OnEquipmentChanged(string unitId)
	{
		Invalidate(unitId);
		var combat = GetNodeOrNull<CombatManager>("/root/CombatManager");
		if (combat == null)
		{
			return;
		}

		foreach (var ally in combat.Allies)
		{
			if (ally.Id == unitId)
			{
				ally.Stats = BuildForUnit(ally);
				_eventBus.EmitStatsChanged(unitId);
				break;
			}
		}
	}

	public UnitStats GetOrCreate(CombatUnitData unit)
	{
		if (_unitStats.TryGetValue(unit.Id, out var existing))
		{
			return existing;
		}

		var stats = BuildForUnit(unit);
		_unitStats[unit.Id] = stats;
		return stats;
	}

	public void Invalidate(string unitId)
	{
		_unitStats.Remove(unitId);
	}

	public void RebuildAll(IReadOnlyList<CombatUnitData> units)
	{
		_unitStats.Clear();
		foreach (var unit in units)
		{
			_unitStats[unit.Id] = BuildForUnit(unit);
		}
	}

	public Godot.Collections.Dictionary GetSnapshot(string unitId)
	{
		if (!_unitStats.TryGetValue(unitId, out var stats))
		{
			var unit = ResolveUnit(unitId);
			if (unit == null)
			{
				return new Godot.Collections.Dictionary();
			}

			stats = BuildForUnit(unit);
			_unitStats[unitId] = stats;
		}

		return stats.ToDictionary();
	}

	public Godot.Collections.Dictionary GetSnapshotWithComparison(string unitId)
	{
		var unit = ResolveUnit(unitId);
		if (unit == null)
		{
			return new Godot.Collections.Dictionary();
		}

		var baseStats = BuildBaseForUnit(unit);
		var finalStats = BuildForUnit(unit);
		_unitStats[unitId] = finalStats;

		var result = new Godot.Collections.Dictionary
		{
			{ "unit_id", unitId },
			{ "class_id", unit.ClassId },
		};

		foreach (var id in CompareStatIds)
		{
			var key = StatRegistry.ToKey(id);
			var baseVal = baseStats.GetFinal(id);
			var finalVal = finalStats.GetFinal(id);
			result[$"base_{key}"] = baseVal;
			result[$"final_{key}"] = finalVal;
			result[$"delta_{key}"] = finalVal - baseVal;
		}

		return result;
	}

	public UnitStats BuildForUnit(CombatUnitData unit)
	{
		var stats = BuildBaseForUnit(unit);
		var combat = GetNodeOrNull<CombatManager>("/root/CombatManager");
		if (combat != null && combat.RunRogueliteActive && unit.IsAlly)
		{
			ApplyRunRelicModifiers(stats);
		}
		else
		{
			ApplyEquipment(stats, unit.Id);
		}

		stats.RecalculateDerived();
		ApplyToUnitHp(unit, stats);
		return stats;
	}

	private static void ApplyRunRelicModifiers(UnitStats stats)
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		var relics = tree?.Root.GetNodeOrNull<RunRelicManager>("/root/RunRelicManager");
		relics?.ApplyRelicModifiers(stats);
	}

	public UnitStats BuildBaseForUnit(CombatUnitData unit)
	{
		var stats = new UnitStats();
		var level = ResolveAllyLevel(unit);

		if (!unit.IsAlly && unit.EnemyTemplateAtkSpeed > 0f)
		{
			unit.HitBoxRadius = unit.HitBoxRadius > 0f ? unit.HitBoxRadius : 12f;
			stats.SetBase(StatId.Level, level);
			stats.SetBase(StatId.MaxHp, unit.MaxHp);
			stats.SetBase(StatId.Damage, unit.BaseAttack);
			stats.SetBase(StatId.AtkSpeed, unit.EnemyTemplateAtkSpeed);
			stats.SetBase(StatId.AtkRange, unit.EnemyTemplateAtkRange > 0f ? unit.EnemyTemplateAtkRange : 20f);
			stats.SetBase(StatId.MoveSpeed, unit.EnemyTemplateMoveSpeed > 0f ? unit.EnemyTemplateMoveSpeed : 100f);
			stats.SetBase(StatId.CritRate, 0.05f);
			stats.SetBase(StatId.CritDamage, StatRegistry.DefaultCritDamage);
			stats.RecalculateDerived();
			return stats;
		}

		var classDef = ClassBaseLoader.Get(unit.ClassId);

		if (classDef != null)
		{
			unit.HitBoxRadius = classDef.HitBoxRadius > 0f ? classDef.HitBoxRadius : 10f;
			stats.SetBase(StatId.Level, level);
			stats.SetBase(StatId.MaxHp, classDef.BaseHp + classDef.HpPerLv * (level - 1));
			stats.SetBase(StatId.Damage, classDef.BaseAtk + classDef.AtkPerLv * (level - 1));
			stats.SetBase(StatId.AtkRange, classDef.BaseRange);
			stats.SetBase(StatId.MoveSpeed, classDef.MoveSpeed);
			stats.SetBase(StatId.CritRate, classDef.BaseCrit);
			stats.SetBase(StatId.AtkSpeed, classDef.BaseAtkSpeed);
			stats.SetBase(StatId.CritDamage, StatRegistry.DefaultCritDamage);
		}
		else
		{
			unit.HitBoxRadius = 10f;
			stats.SetBase(StatId.Level, level);
			stats.SetBase(StatId.MaxHp, unit.MaxHp);
			stats.SetBase(StatId.Damage, unit.BaseAttack);
			stats.SetBase(StatId.AtkSpeed, 1f);
			stats.SetBase(StatId.AtkRange, 80f);
			stats.SetBase(StatId.MoveSpeed, 100f);
			stats.SetBase(StatId.CritRate, 0.05f);
			stats.SetBase(StatId.CritDamage, StatRegistry.DefaultCritDamage);
		}

		ApplyEarlyGameCaps(stats, unit);
		ApplyRunBuffModifiers(stats, unit);
		stats.AddIncreased(StatId.Damage, _meta.GlobalStatBonusPercent);
		if (_db != null)
		{
			ApplyDbGlobalStats(stats, _db);
		}

		stats.RecalculateDerived();
		return stats;
	}

	private static void ApplyEarlyGameCaps(UnitStats stats, CombatUnitData unit)
	{
		if (!unit.IsAlly)
		{
			return;
		}

		var caps = EarlyGameCapsLoader.Get();
		stats.SetBase(StatId.MaxHp, stats.GetBase(StatId.MaxHp) * caps.GetMultiplier("MaxHp"));
		stats.SetBase(StatId.Damage, stats.GetBase(StatId.Damage) * caps.GetMultiplier("BaseAttack"));
		if (stats.GetBase(StatId.Defense) > 0f)
		{
			stats.SetBase(StatId.Defense, stats.GetBase(StatId.Defense) * caps.GetMultiplier("Defense"));
		}
	}

	private static void ApplyDbGlobalStats(UnitStats stats, DbManager db)
	{
		ApplyMetaStatIncreased(stats, StatId.MaxHp, db.GetGlobalStatPercent("MaxHp"));
		ApplyMetaStatIncreased(stats, StatId.Damage, db.GetGlobalStatPercent("Damage") + db.GetGlobalStatPercent("BaseAttack"));
		ApplyMetaStatIncreased(stats, StatId.Defense, db.GetGlobalStatPercent("Defense"));
		ApplyMetaStatIncreased(stats, StatId.AtkSpeed, db.GetGlobalStatPercent("AtkSpeed"));
		ApplyMetaStatFlatRate(stats, StatId.CritRate, db.GetGlobalStatPercent("CritRate"));
		ApplyMetaStatFlatRate(stats, StatId.Dodge, db.GetGlobalStatPercent("Dodge"));
	}

	private static void ApplyMetaStatIncreased(UnitStats stats, StatId id, float percent)
	{
		if (percent > 0f)
		{
			stats.AddIncreased(id, percent / 100f);
		}
	}

	private static void ApplyMetaStatFlatRate(UnitStats stats, StatId id, float percentPoints)
	{
		if (percentPoints > 0f)
		{
			stats.AddFlat(id, percentPoints / 100f);
		}
	}

	private static void ApplyRunBuffModifiers(UnitStats stats, CombatUnitData unit)
	{
		if (!unit.IsAlly)
		{
			return;
		}

		var tree = Engine.GetMainLoop() as SceneTree;
		var runCard = tree?.Root.GetNodeOrNull<RunCardManager>("/root/RunCardManager");
		if (runCard == null || !runCard.HasActiveBuffs)
		{
			return;
		}

		var buffs = runCard.GetAggregatedBuffs();
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

	private static int ResolveAllyLevel(CombatUnitData unit)
	{
		if (!unit.IsAlly)
		{
			return unit.Level > 0 ? unit.Level : 1;
		}

		var party = GetPartyManager();
		var rosterId = party?.GetRosterIdForUnit(unit.Id) ?? string.Empty;
		var tree = Engine.GetMainLoop() as SceneTree;
		var rosterProg = tree?.Root.GetNodeOrNull<RosterProgressionManager>("/root/RosterProgressionManager");
		if (!string.IsNullOrEmpty(rosterId) && rosterProg != null)
		{
			return rosterProg.GetLevel(rosterId);
		}

		return unit.Level > 0 ? unit.Level : 1;
	}

	private void InvalidateAllAllies()
	{
		var combat = GetNodeOrNull<CombatManager>("/root/CombatManager");
		if (combat == null)
		{
			return;
		}

		foreach (var ally in combat.Allies)
		{
			Invalidate(ally.Id);
		}
	}

	private void ApplyEquipment(UnitStats stats, string unitId)
	{
		var equipped = _loot.GetEquippedForUnit(unitId);
		foreach (var item in equipped)
		{
			const float equipmentScale = 0.4f;
			if (item.RolledBaseStat > 0f)
			{
				var baseStat = MapBaseStatForSlot(item.Slot, item.RolledBaseStat);
				stats.AddFlat(baseStat.statId, baseStat.value * equipmentScale);
			}

			foreach (var affix in item.Affixes)
			{
				var statId = AffixPoolLoader.MapAffixId(affix.Id)
					?? AffixPoolLoader.MapTarget(affix.Id)
					?? MapLegacyAffix(affix.Id);
				if (statId != null)
				{
					stats.AddFlat(statId.Value, affix.Value * equipmentScale);
				}
			}
		}
	}

	private static (StatId statId, float value) MapBaseStatForSlot(RougeliteIdle.Core.Enums.SlotType slot, float rolled)
	{
		return slot switch
		{
			RougeliteIdle.Core.Enums.SlotType.Armor or RougeliteIdle.Core.Enums.SlotType.Helmet
				or RougeliteIdle.Core.Enums.SlotType.Gloves or RougeliteIdle.Core.Enums.SlotType.Boots
				=> (StatId.Defense, rolled),
			RougeliteIdle.Core.Enums.SlotType.Weapon => (StatId.Damage, rolled),
			RougeliteIdle.Core.Enums.SlotType.BackAccessory or RougeliteIdle.Core.Enums.SlotType.Trinket
				=> (StatId.MoveSpeed, rolled * 0.5f),
			_ => (StatId.Damage, rolled),
		};
	}

	private static void ApplyToUnitHp(CombatUnitData unit, UnitStats stats)
	{
		unit.MaxHp = stats.GetFinal(StatId.MaxHp);
		if (unit.CurrentHp > unit.MaxHp)
		{
			unit.CurrentHp = unit.MaxHp;
		}
	}

	private CombatUnitData? ResolveUnit(string unitId)
	{
		var combat = GetNodeOrNull<CombatManager>("/root/CombatManager");
		if (combat != null)
		{
			foreach (var ally in combat.Allies)
			{
				if (ally.Id == unitId)
				{
					return ally;
				}
			}
		}

		return CreateFallbackUnit(unitId);
	}

	private static CombatUnitData? CreateFallbackUnit(string unitId)
	{
		var party = GetPartyManager();
		var classId = party?.GetClassIdForUnit(unitId) ?? string.Empty;
		var displayName = party?.GetDisplayNameForUnit(unitId) ?? unitId;

		return unitId switch
		{
			"ally_a" or "ally_b" or "ally_c" => new CombatUnitData
			{
				Id = unitId,
				ClassId = string.IsNullOrEmpty(classId) ? ResolveDefaultClass(unitId) : classId,
				DisplayName = displayName,
				IsAlly = true,
				MaxHp = 100,
				CurrentHp = 100,
				BaseAttack = unitId == "ally_c" ? 12 : (unitId == "ally_b" ? 8 : 10),
			},
			_ => null,
		};
	}

	private static PartyManager? GetPartyManager()
	{
		var tree = Engine.GetMainLoop() as SceneTree;
		return tree?.Root.GetNodeOrNull<PartyManager>("/root/PartyManager");
	}

	private static string ResolveDefaultClass(string unitId) => unitId switch
	{
		"ally_a" => "Vanguard_01",
		"ally_b" => "Sniper_01",
		"ally_c" => "Mage_01",
		_ => string.Empty,
	};

	private static StatId? MapLegacyAffix(string affixId) => affixId switch
	{
		"primary_atk" => StatId.Damage,
		"sub_crit" => StatId.CritRate,
		"primary_armor" => StatId.Defense,
		"sub_hp" => StatId.MaxHp,
		"primary_speed" or "suffix_spd_01" => StatId.MoveSpeed,
		_ => affixId.StartsWith("suffix_spd_") ? StatId.MoveSpeed : null,
	};
}
