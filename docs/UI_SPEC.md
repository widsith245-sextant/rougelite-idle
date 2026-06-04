# UI 规范

## 文案规范（禁止简称 Label）

- 面向玩家的 **Label、详情、Tooltip、错误提示** 必须使用完整中文（或配表 `displayName`）。
- **禁止**：装备槽单字（武/甲/头…）、属性英文缩写（HP/DMG/Spd）、物品名 4 字硬截断。
- **允许简化仅用于图标层**：`slot_labels.json` 的 `iconAbbrev`、`stat_labels.json` 的 `compactIcon` 路径；48×48 格以图标为主。
- 空间不足时：缩小字号、autowrap、Tooltip，而非删字。
- 加载器：[`gdscript/ui/ui_labels_loader.gd`](../gdscript/ui/ui_labels_loader.gd)；槽位表 [`data/tables/ui/slot_labels.json`](../data/tables/ui/slot_labels.json)。

信息流与资产替换：[`UI_INFORMATION_FLOW.md`](UI_INFORMATION_FLOW.md)、[`UI_ASSET_WORKFLOW.md`](UI_ASSET_WORKFLOW.md)。

## 三层信息架构

### Tier 1 — 战斗 HUD（400×150）

- `project.godot`：`window/size/borderless=true`，`resizable=false`，不置顶
- `gdscript/main/window_chrome.gd` 挂于 `game_root`，启动时锁定 400×150
- 战斗区无右侧 Quick Stats；属性对比在背包/编队弹窗内
- HUD 顶部技能 toast（交战时友方可选左上特写）
- 战斗单位头顶 HUD：小血条 + 技能CD填充方块
- 左上 **宝箱气泡**（品质色 + 5 格积攒，点击单次开箱进背包，满 5 自动开）
- QuickBar 上方 **6px 波次进度条**（摆点 + 当前推进）
- **禁止**在 HUD 内塞入administration、鉴定详情、技能树

- 战斗区高度约 **112px**（为底部进度条 + QuickBar 预留 ~38px）
- `GameRoot` / `QuickBar` 启用 `clip_contents`，防止控件绘制溢出 400×150

### Tier 2 — 管理弹窗（640×480）

- 基类：`popup_window_base.gd` — 挂 `get_tree().root`，独立 OS 窗口
- **全部 satellite 窗 `borderless=true`** + 深色 `ColorRect` 背景（`SatelliteWindow.BACKGROUND_COLOR`）
- 管理：`popup_manager.gd` — 单例式开关各 content 场景

| 键 | Content 场景 |
|----|--------------|
| backpack | `backpack_content.tscn` |
| squad | `squad_content.tscn` |
| skill | `skill_content.tscn` |
| cultivation | `cultivation_content.tscn`（刷DB \| 星图） |
| wonderland | `wonderland_content.tscn` |

### Tier 3 — 子面板密度令牌（Tier2 内共用）

- 窗口：640×480
- 左栏纸娃娃：宽 72px，高 ~120px
- 主网格：48×48 格，8 列，间距 2px
- TabBar：高 22px，Tab 标题使用全名（装备/宝箱/技能）；极窄时可用图标+Tooltip，不截断为无意义单字
- 底栏：单行 12px 字体 — `Lv / EXP / 金币` + 上下文操作按钮
- 详情区：最多 3 行 autowrap，超出滚动

## QuickBar（4 入口）

- 收起：18px 高，右下 **≡ 菜单**
- 展开：宽 **320px**，高 30px，右对齐
- 四个双字入口：**背包** | **编队** | **养成** | **冒险▼**
- **冒险** 为 `MenuButton` 子菜单：训练关卡 / 奇境 Run
- 技能入口合并至背包「技能」Tab；日志/立绘移至设置
- 场景：`scenes/layers/quick_bar.tscn`

## SettingsDock（常驻）

- 左下角 **58×18**「设置」按钮，不随 QuickBar 折叠
- 点击 toggle 设置弹窗（`PopupId.SETTINGS`）
- 设置页「工具」区：打开/关闭日志、立绘预览、打开存档目录
- 场景：`scenes/layers/settings_dock.tscn`

## 日志卫星窗

- 独立 OS 窗 400×160，`borderless=true`，深色 `ColorRect` 背景
- 从 **设置 → 工具 → 打开/关闭日志** 切换（不在 QuickBar）

## 背包布局

```
┌─────────────────────────────────────────────────────────┐
│ 当前查看：[出战角色 ▼]                                   │
├──────────┬──────────────────────────────────────────────┤
│ 纸娃娃   │ 属性对比（8 项中文全名 base→final，Δ 着色）  │
│ 8 装备槽 │ Tab: 装备 | 宝箱 | 技能（槽位全名见 slot_labels）│
├──────────┴──────────────────────────────────────────────┤
│ 详情 + [穿上][卸下][鉴定][分解]   Lv/EXP/金币 底栏       │
└─────────────────────────────────────────────────────────┘
```

- 共享背包 48 格；每人独立 8 槽（`LootManager` per `unitId`）
- 出战角色下拉仅列出已解锁出战槽；编队编辑在「编队」弹窗
- 订阅 `LootInventoryChanged` 自动 refresh；格子 48×48，品质边框
- `StatsService.GetSnapshotWithComparison(unitId)` 驱动对比区

## 编队布局（与背包同密度）

```
┌─────────────────────────────────────────────────────────┐
│ 出战位  [槽1][槽2🔒][槽3🔒]   64×64 大格                 │
├──────────┬──────────────────────────────────────────────┤
│ 纸娃娃   │ 属性摘要（HP/攻/防/速）                        │
│          │ Tab: 可入队 | 未解锁                          │
├──────────┴──────────────────────────────────────────────┤
│ 候选 48px 格 — 点击指派到出战槽                          │
│ 底栏：编组 n/3 · [应用到战斗]                            │
└─────────────────────────────────────────────────────────┘
```

## 养成弹窗（刷DB | 星图）

- **刷DB**：金币解锁编组位、技能插槽、全局 stat；读 `db_tree.json`
- **星图**：星图点解锁全局百分比；读 `star_chart_tree.json`
- 职责：刷DB 不做角色技能；星图不做编组位

## 技能弹窗

- 按 roster 角色 Tab（完整 display_name，最多 6 字）
- 左：技能树（主动/被动分区）；右：**240px** 插槽栏
- 共用组件 `skill_slot_panel.gd`：4 主动 + 2 被动格子，中文名，锁定占位格
- 插槽上限读 `DbManager`；节点读 `character_skill_trees.json`

## 技能发动（战斗区）

- `skill_cast_overlay.tscn`：toast 顶中（宽 ≤160px），友方特写 56×64 左上
- 敌方仅 toast，无特写
- 与 `chest_reveal_overlay` 共用 `UiFxGate` 互斥

## 战斗信息反馈

- 主反馈由单位头顶 `unit_overhead_hud.tscn` 提供（血条 + CD方块）
- 旧全局左右血条 `hud_health_bar.gd` 作为兜底反馈保留开启态
- 鉴定遮罩不拦截 QuickBar 输入区域，菜单可在遮罩期间展开

## 纸娃娃（M3）

- `CharacterBase`：Shadow / Body / Outfit / Head + AnimationPlayer
- HP &lt; 30%：`EcchiDamagedState` 换装 + 红晕 Shader + 抖动
- 受击：`HitFlashController` 白闪

## 弹窗文案

只读页从 `data/tables/ui/popup_texts.json` 加载，禁止硬编码「占位」字样。
