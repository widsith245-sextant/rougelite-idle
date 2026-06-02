# 属性系统

## StatId 与派生公式

- `MaxHp` = 职业基础 + 队伍等级 % + 装备 flat
- `Damage` = 物理/元素 flat 聚合 + 队伍等级 % + Meta %
- `Dps` = `Damage × AtkSpeed × (1 + CritRate × (CritDamage - 1))`

上下限见 `StatRegistry`（攻速 ≤5、CDR ≤75%、抗性 ≤75%）。

## UnitStats 乘区

- `Base` / `Flat` / `Increased`（% 相加后乘算）
- `StatsService` Autoload：从 `char_class_base.json` + 装备 + `ProgressionManager` 队伍加成 + `MetaManager` + `DbManager` + `early_game_caps.json` 构建

## 初期削弱

`early_game_caps.json` 对友方 `MaxHp` / `Damage` 等 Base 乘系数；`PartyManager.MaxActiveSlots` 默认 1，由 `DbManager` 解锁至 3。

## 对比快照 API

`StatsService.GetSnapshotWithComparison(unitId)` 返回扁平键：

| 键模式 | 含义 |
|--------|------|
| `base_<StatKey>` | 无装备（含队伍等级/Meta） |
| `final_<StatKey>` | 含当前单位装备 |
| `delta_<StatKey>` | final − base |

对比项：`Level`, `MaxHp`, `Dps`, `Damage`, `AtkSpeed`, `AtkRange`, `MoveSpeed`, `CritRate`。

背包 `backpack_stats_compare.gd` 与战斗快照均使用同一套键名（`StatRegistry.ToKey`）。

## StatsComponent（GDScript）

挂于 `CharacterBase`，订阅 `StatsChanged` / `EquipmentChanged`，仅负责展示。

## 配表

| 文件 | 用途 |
|------|------|
| `data/tables/character/char_class_base.json` | 职业素体 |
| `data/tables/progression/player_progression.json` | 队伍 EXP 曲线与每级加成 |
| `data/tables/meta/currencies.json` | 金币/星图/门票初值 |
| `data/tables/combat/engagement_rules.json` | 停步距离与射程校验 |
| `data/tables/loot/item_affix_pool.json` | 词缀目标映射 |
| `data/tables/character/class_skills.json` | 技能战斗定义 |
| `data/tables/character/character_skill_trees.json` | 角色技能树 UI/装配 |
| `data/tables/meta/db_tree.json` | 刷DB 金币 Meta |
| `data/tables/meta/early_game_caps.json` | 初期数值上限 |
| `data/tables/ui/stat_labels.json` | UI 文案 |
