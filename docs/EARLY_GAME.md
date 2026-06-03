# 前期体验数值（Early Game）

本文档说明训练模式前期 band 的可验证来源，便于对照配表与运行时行为。

## 解锁节奏

| 规则 | 来源 |
|------|------|
| 新档仅解锁第 1 关 | `early_game_caps.json` → `initialUnlockedStages` |
| 通关当前关解锁下一关 | `stage_catalog.json` → `unlockFrom` 链 |
| 存档字段 | `SaveData v3` → `UnlockedStageIds` / `ClearedStageIds` |

## 第 1 关（chapter_01_level_01）

| 维度 | 值 | 可验证来源 |
|------|-----|-----------|
| 波次 | 8 波 | `data/tables/combat/stages/chapter_01/level_01.json` → `waves` |
| 通关奖励 | 40 exp / 20 金 | 同上 → `onStageComplete` |
| 推荐等级 | Lv1 | `stage_catalog.json` |
| 关卡等级 | 1 | `level_01.json` → `stageLevel` |

## 友方数值（Lv1，cap 后）

| 维度 | 说明 | 来源 |
|------|------|------|
| 基础属性 | 职业 Base × 前期系数 | `char_class_base.json` × `early_game_caps.json` → `playerStatMultipliers` |
| 等级上限 | Lv10 | `playerInitialLevelCap` |
| 前期 band | 前 3 关奖励 ×1.0 | `earlyStageBand.maxStageLevel` / `rewardMultiplier` |

典型 Lv1 系数：`MaxHp×0.72`、`BaseAttack×0.68`、`Defense×0.75`、`Speed×0.9`。

## 奇境 combat 房

- 房间池：`run_room_pool.json`
- 仅使用**已解锁** stage；未解锁模板会被过滤，进入房间时降级为最高已解锁关

## 验证清单

1. 新档：关卡弹窗仅第 1 关可点
2. 通关 level_01 后 level_02 解锁并写入存档
3. 重启后 locked/unlocked 状态保持
4. `SetStageId` 对未解锁关拒绝并播报
5. 前 3 关通关奖励与 `onStageComplete` × `rewardMultiplier` 一致
