# 里程碑验收

## M1 — UI 骨架 ✅

## M2 — 连携战斗引擎 ✅

## M2+ — QuickBar + Popup ✅

## M3 — 纸娃娃 ✅

## M4a — StatsComponent + 配表 ✅

- [x] StatId / StatRegistry / UnitStats
- [x] StatsService Autoload
- [x] char_class_base / item_affix_pool / class_skills
- [x] LootManager 穿戴 + affix snapshot

## M4b — 属性 UI ✅

- [x] QuickStatsPanel 右侧 88px
- [x] Detailed Stats Popup + QuickBar「属性」

## M5 — X 轴混合战斗 ✅

- [x] BattlefieldController 替换 PositionalMatrix（已归档）
- [x] PositionChanged(float oldX, newX)
- [x] 普攻轨 + 读条技能轨
- [x] encounter classId / initialPositionX
- [x] range_test_battle 场景

## M5+ — 演出 ✅

- [x] 暴击 Hit Stop
- [x] Camera2D 跟随前排
- [x] Reposition 残影（modulate tween）

## M6 — 存档 / 离线 ✅（基础）

- [x] SaveManager JSON 存星图/会话时间
- [x] OfflineIdleManager 挂钩 SaveGame
- [x] 完整装备/背包序列化（SaveManager v3：identified / chests / equipped）
- [ ] TrainingMode 关卡表驱动（后续）

---

装备/开箱规则快照见 [`EQUIPMENT_LOOT_SNAPSHOT.md`](EQUIPMENT_LOOT_SNAPSHOT.md)；UI 信息流 [`UI_INFORMATION_FLOW.md`](UI_INFORMATION_FLOW.md)；资产工作流 [`UI_ASSET_WORKFLOW.md`](UI_ASSET_WORKFLOW.md)；任务板 [`WORK_IN_PROGRESS.md`](WORK_IN_PROGRESS.md)。
