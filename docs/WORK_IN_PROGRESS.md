# 进行中任务板（WORK IN PROGRESS）

> **更新日期**：2026-06-02  
> **用途**：远端机 `git pull` 后快速了解近期已完成项与待办；详细装备/开箱规则见 [`EQUIPMENT_LOOT_SNAPSHOT.md`](EQUIPMENT_LOOT_SNAPSHOT.md)。

---

## 近期已完成

### 背包 / GM / 装备系统（2026-06）

| 项 | 说明 | 关键路径 |
|----|------|----------|
| F8 GM 面板 | CanvasLayer layer=10，战斗暂停可开关；子窗口转发 F8 | `scenes/ui/gm/gm_tools_layer.tscn`, `gm_tools_controller.gd` |
| 背包容量 48 | `IdentifyNext` 满包不消耗；卸装/替换检查 `HasBagSpace` | `LootManager.cs`, `early_game_caps.json` |
| 拖拽穿卸装 | 背包格 `slot_type` / `class_id`；`item_grid_cell` 校验 | `backpack_content.gd`, `item_grid_cell.gd` |
| 职业专属武器护甲 | 5 职业各 Weapon+Armor；`EquipByBagIndex` 严格校验 | `equipment_templates.json` |
| 词缀池 | `AffixPoolLoader.RollForSlot`；品质 bonus 词缀 | `item_affix_pool.json`, `ItemGenerator.cs` |
| 白值按槽位 | 护甲→Defense，武器→Damage 等 | `StatsService.cs` |
| 击杀宝箱 rewardTier | tier≥3/5 抬升品质下限 | `ProgressionManager.cs`, `enemy_templates.json` |
| 章节敌人掉落 | goblin/orc killRewards | `drop_tables.json` |
| 占位清理 | 立绘失败提示；装饰槽 mock 文案移除 | `backpack_content.gd`, `PortraitWindowManager` |
| 规则落盘 | 本快照 + manifest + 本文档 | `docs/EQUIPMENT_LOOT_SNAPSHOT.md`, `equipment_manifest.json` |
| UI 全名文案 | 槽位/属性禁简称；`UiLabelsLoader` | `slot_labels.json`, `backpack_content.gd`, `item_grid_cell.gd` |
| UI 信息流文档 | QuickBar→Popup→Loot 信号链 | `docs/UI_INFORMATION_FLOW.md` |
| UI 资产工作流 | manifest + assets 目录占位 | `docs/UI_ASSET_WORKFLOW.md`, `ui_asset_manifest.json` |

### 战斗 / 技能 / 表现（前序里程碑）

- B3–B8 效果 Handler、EffectTriggerBus、技能 v2 富文本
- 跳字分类型、单位 HUD 效果条、GM 战斗调试面板
- 详见 [`MILESTONES.md`](MILESTONES.md) M1–M6 已勾选项

---

## 待办（仓库内已标记）

| 优先级 | 任务 | 来源 |
|--------|------|------|
| 中 | **TrainingMode 关卡表驱动** | `MILESTONES.md` M6 未勾选项 |

---

## 配表 / 体验债（建议后续）

| 项 | 现状 | 建议 |
|----|------|------|
| HeadAccessory 模板 | UI「头部饰品」槽，鉴定池 0 件 | 补 1–2 件通用 T1 或隐藏槽位至开放 |
| 鉴定 RNG | 15 模板全局均匀随机 | 可按职业/关卡权重或宝箱 tier 过滤 |
| 名册 vs 出战位 | 5 职业，最多 3 出战 UI 单位 | 文档已说明；Support/Berserker 编入队后可穿专属装 |
| 装饰槽 UX | 无「暂未开放」提示 | 可选：点击饰槽显示说明 |
| manifest 同步 | 改 `equipment_templates.json` 后 | 同步更新 `equipment_manifest.json` 与 `EQUIPMENT_LOOT_SNAPSHOT.md` |

---

## 建议验收（装备/开箱）

完整条目见 [`EQUIPMENT_LOOT_SNAPSHOT.md` §7](EQUIPMENT_LOOT_SNAPSHOT.md#7-建议验收清单)。快速检查：

1. 每角色 8 槽 UI 与 `SlotType` 一致  
2. 跨职业武器/护甲无法穿上并有提示  
3. 背包 48/48 时鉴定不消耗宝箱  
4. 待领宝箱满 5 自动进未鉴定队列  
5. `dotnet build` 0 错误；训练关背包流程无 ERROR  

## 建议验收（UI 全名）

1. 背包 8 装备槽显示「武器/护甲/头盔…」全名，无单字简称  
2. 属性对比区标题为中文全名（生命上限、攻击伤害…）  
3. 物品格 Tooltip 为完整物品名；宝箱格显示「宝箱」  
4. 技能插槽格显示完整技能名  

---

## 文档索引

| 文档 | 内容 |
|------|------|
| [`UI_INFORMATION_FLOW.md`](UI_INFORMATION_FLOW.md) | 窗口层级、弹窗导航、背包/战斗信息流 |
| [`UI_ASSET_WORKFLOW.md`](UI_ASSET_WORKFLOW.md) | 资产替换步骤与 manifest |
| [`EQUIPMENT_LOOT_SNAPSHOT.md`](EQUIPMENT_LOOT_SNAPSHOT.md) | 8 槽、15 件装备、职业矩阵、开箱/鉴定规则 |
| [`data/tables/loot/equipment_manifest.json`](../data/tables/loot/equipment_manifest.json) | 机器可读装备清单与规则元数据 |
| [`MILESTONES.md`](MILESTONES.md) | 里程碑验收 |
| [`GM_TOOLS.md`](GM_TOOLS.md) | F8 GM 使用说明 |
| [`STATS_SYSTEM.md`](STATS_SYSTEM.md) | 属性与配表索引 |
| [`EARLY_GAME.md`](EARLY_GAME.md) | 前期数值与关卡解锁 |

---

*改完装备/掉落逻辑或配表后，请更新本节「近期已完成」或「配表/体验债」，并刷新快照日期。*
