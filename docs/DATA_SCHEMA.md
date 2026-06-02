# 数据表 Schema

配置根目录：`res://data/tables/`。

## combat/encounter_default.json

```json
{
  "allies": [ UnitEntry... ],
  "enemies": [ UnitEntry... ]
}
```

### UnitEntry

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 单位 ID（如 `ally_a`） |
| displayName | string | 显示名 |
| formationIndex | int | 阵型槽位 0–2（敌人可省略） |
| speed | float | 读条速度 |
| baseAttack | float | 基础攻击 |
| maxHp | float | 最大 HP |
| activeSkill | SkillEntry | 主动技 |
| passives | PassiveEntry[] | 被动列表 |

### SkillEntry

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 技能 ID |
| skillMultiplier | float | 伤害倍率 |
| moveTags | `{ kind, distance }[]` | Charge / Retreat |

### PassiveEntry

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 被动 ID |
| triggerType | string | `OnAnyAllyMoved` / `OnSelfReachSlot` 等 |
| targetSlot | int | 触发槽位（-1 表示任意） |
| skillMultiplier | float | 被动伤害倍率 |

## loot/equipment_templates.json

```json
{ "items": [ TemplateItem... ] }
```

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 模板 ID（如 `blade_t1_01`） |
| displayName | string | 名称 |
| slot | string | Weapon / Armor / … |
| itemLevel | int | 模板 iLvl |
| baseStatMin / baseStatMax | float | 白值区间 |
| effectId | string? | 特效 ID |
| affixes | Affix[] | 词缀定义 |

## loot/new_game.json

```json
{ "startingChests": [ { "id", "displayName" } ] }
```

新游戏时 `LootManager.InitializeNewGame()` 读入未鉴定宝箱。

## progression/player_progression.json

| 字段 | 类型 | 说明 |
|------|------|------|
| maxTeamLevel | int | 队伍等级上限 |
| expBase | float | EXP 基数（需求 = expBase × level^expExponent） |
| expExponent | float | 曲线指数（默认 1.35） |
| perLevelMaxHpPercent | float | 每级全队 MaxHp +% |
| perLevelDamagePercent | float | 每级全队 Damage +% |

## meta/currencies.json

| 字段 | 类型 | 说明 |
|------|------|------|
| initialGold | int | 初始金币 |
| salvageGoldPerItemLevel | int | 分解返还 = iLvl × 该值 |
| offlineStarChartPerHour | float | 离线星图点/小时 |

## loot/drop_tables.json

`training`：训练关鉴定后额外宝箱概率与上限。

## combat/engagement_rules.json

战场宽、敌人锚点 X、各职业 `atkRange` / `stopX`（停步 = enemyAnchorX − atkRange）。

## combat/stage_training.json

| 字段 | 说明 |
|------|------|
| stageLength | 抽象跑道长度（像素等价） |
| marchSpeed | 行军速度 |
| stageLevel | 关卡等级（用于奖励衰减） |
| waves[].progress | 0–1 波次触发点 |
| waves[].enemyId | 对应 enemy_templates.json |
| waves[].count | 本波刷新数量（顺序投放） |
| waves[].spawnMode | `sequential`（默认）或 `cluster`（同屏多敌） |
| waves[].entries[] | cluster 格式：`enemyId`, `count`, `offsetStep`/`spawnOffsetX` |
| waves[].rewardWeight | 本波奖励权重（预留） |
| onStageComplete | 通关 EXP/金币 |

## combat/stage_catalog.json

章节到关卡索引：`chapterId -> levels[]`，每关包含 `stageId/recommendedLevel/unlockFrom/path`。

## combat/stages/chapter_01/level_XX.json

第一章 10 关配置，每关可配置波次数（当前默认 8 波模板）。

## combat/enemy_templates.json

训练关波次敌人体模板（HP/技能/显示名），新增 `level` 用于等级差奖励衰减。

| 字段 | 说明 |
|------|------|
| archetype | trash / elite / boss |
| tags | 克制与池子权重标签 |
| damageProfile.type | 对应 damage_profiles.json |
| onHitEffects[] | `{ id, chance }` 命中效果 |
| rewardTier | 1–5，击杀奖励倍率 |

## combat/damage_profiles.json

伤害类型：`id`, `defenseStat`, `critAllowed`, 可选 `damageScale`。

## combat/combat_effects.json

战斗效果 MVP：`bleed_light`, `slow_heavy`, `armor_break` 等。

## run/run_room_pool.json

奇境 Run 房间池：`templates[]`（combat/rest/reward）、`bossRoom`、`runLength.min/max`。

## character/roster.json

`rosterId`, `classId`, `displayName`, `defaultSlot`, `unlockDbNodeId?` — `PartyManager` 编队源；空 unlock 表示初始可用。

## loot/chest_quality.json

| 字段 | 说明 |
|------|------|
| id | common / rare / epic |
| color | RGBA |
| maxAccumulate | 气泡积攒上限 |
| itemLevelBonus | 鉴定 iLvl 加成 |
| bonusAffixCount | 额外词缀 roll |
| minQualityTier | 写入 `ItemData.quality` |

## meta/early_game_caps.json

初期削弱：`defaultMaxActiveSlots`, `playerStatMultipliers`, `defaultMaxActiveSkillSlots`, `passiveSlotsUnlocked`。

## meta/reward_decay_rules.json

奖励衰减参数：

- `startPenaltyDiff`：高于目标多少级开始衰减
- `perLevelPenalty`：每超1级衰减比例
- `minMultiplier`：最低奖励倍率
- `bonusUnderLevel`：低于目标时每级加成
- `maxBonusMultiplier`：加成上限
- `healOnStageClearPercent`：关卡完成回血比例

## meta/db_tree.json

金币 Meta 树：`nodes[].costGold`, `effects[]`（SquadSlot / ActiveSkillSlot / PassiveSlot / GlobalStat / RosterUnlock）。

## meta/star_chart_tree.json

星图点树：`costStarPoints`, `statBonusPercent`。

## character/character_skill_trees.json

按 `rosterId` 的角色技能树（与刷DB 分离）：`nodeType` active|passive, `skillId`, `prerequisites`。

## loot/drop_tables.json — killRewards / stageComplete

杀怪与通关经济；`chest.chance` + `quality` 驱动 pending 气泡。

## 每人装备（LootManager）

- 内存：`unitId → SlotType → ItemData`（默认 `ally_a` / `ally_b` / `ally_c`）
- API：`EquipByBagIndex(index, unitId)`、`Unequip` / `UnequipBySlotName`、`GetEquippedSnapshot(unitId)`
- 存档预留：`SaveData.equippedByUnit`（M6 完整序列化）

## ui/popup_texts.json

技能 / 星图 / 奇境弹窗的最小文案配置（title、lines、nodes、footer 等）。
