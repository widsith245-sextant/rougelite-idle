# Changelog

## M2+ — 战斗循环与 Popup UI

- 修复 ActionQueue 读条冻结
- QuickBar 取代 Drawer 滑出面板
- 640×480 独立 Window 弹窗
- 背包 8 列网格 + 鉴定/分解

## 归档与精简

- 旧 Drawer UI → `archive/m1_drawer/`
- 战斗 Debug 面板 → `archive/debug/`
- 删除 `CombatManager` Debug API
- `data/mock/` → `data/tables/{combat,loot,ui}/`
- `CombatEncounterFactory` → `EncounterTableLoader`
- `SeedMockChests` → `LootManager.InitializeNewGame()`
- Save/Offline stub 移除 `GD.Print`，保留 M4 TODO
- 新增 `docs/` 六文档，精简 README

## M3 — 纸娃娃

- `CharacterVisualResource`、`PlaceholderSpriteFactory`
- `CharacterBase` + `HitFlashController` + `EcchiDamagedState` 红晕
- `combat_stage` ColorRect → 纸娃娃
- `GetUnitHp` API、背包预览
- Popup 读 `popup_texts.json`

## M4a–M6 — 属性与 X 轴战斗

- StatsService + StatId/UnitStats + 职业/词缀/技能配表
- QuickStats 右侧面板 + 详细属性 Popup
- BattlefieldController 连续 X 轴 + 混合战斗（普攻轨 + 读条技能）
- PositionChanged 改为 float X
- 暴击 Hit Stop、镜头跟随、重定位残影
- SaveManager 基础存档、range_test 场景
- PositionalMatrix 移至 `archive/combat_v1/`
