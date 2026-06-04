# 奇境 Run（Roguelite）

## 概述

奇境为独立 Run 模式：局外装备**不**带入战斗，Meta（星图 / DB / 角色等级 / early caps）仍生效。Run 内通过**遗物**与**卡牌**构筑。

| 来源 | 内容 |
|------|------|
| reward 房 | 遗物三选一（`RunRelicManager`） |
| 每 3 波 | Run 卡牌三选一（`RunCardManager`，不变） |

## 装备隔离

`StatsService.BuildForUnit` 在 `CombatManager.RunRogueliteActive == true` 时：

- 跳过 `ApplyEquipment`（0.4× 局外装备）
- 改为 `RunRelicManager.ApplyRelicModifiers`

Run 结束后 `RunSessionManager.RebuildPartyStatsForRun()` 恢复局外装备属性。

## 遗物

- 配表：`data/tables/run/run_relic_pool.json`
- Autoload：`RunRelicManager`
- API：`ResetForRun()` / `ApplyRelic(id)` / `GetActiveRelicsSnapshot()` / `GetAggregatedBuffs()`

## 统计与 S/A/B/C 结算

`RunStatsAggregator` 追踪：

- 击杀、承伤、最低 HP%、死亡次数、用时

规则表：`data/tables/run/run_score_rules.json`

| 评级 | 条件（默认） | 黄金宝箱品质 |
|------|--------------|--------------|
| S | 通关 + 0 死亡 + 房间 ≥ 7 | legendary |
| A | 通关 + 死亡 ≤ 1 | epic |
| B | 通关 | rare |
| C | 失败且房间 ≥ 3 | common |

`SettleRun` → 金币/经验 → `RunSettlementWindowManager` 弹窗 → 领取时 `LootManager.GrantRunSettlementChest(quality)`。

## UI

- 遗物选择：`run_relic_pick_content`（PopupManager `open_run_relic_pick`）
- 结算窗：`run_settlement_window.tscn` + 宝箱 reveal 动画

## P2（本期未做）

- Run 中途存档
- reward 房 `goldBonus` 接线
- 卡牌与遗物 UI 合并展示策略
