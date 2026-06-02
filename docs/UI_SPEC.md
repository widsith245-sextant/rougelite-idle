# UI 规范

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

### Tier 2 — 管理弹窗（640×480）

- 基类：`popup_window_base.gd` — 挂 `get_tree().root`，独立 OS 窗口
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
- TabBar：高 22px，标题截断 4–6 字
- 底栏：单行 12px 字体 — `Lv / EXP / 金币` + 上下文操作按钮
- 详情区：最多 3 行 autowrap，超出滚动

## QuickBar

- 收起：18px 高，仅显示 **≡ 菜单**
- 展开：30px，图标行：**背包 / 编队 / 技能 / 养成 / 奇境**
- 每钮 `custom_minimum_size.x ≈ 52`，字号 9–10
- 场景：`scenes/layers/quick_bar.tscn`

## 背包布局

```
┌─────────────────────────────────────────────────────────┐
│ 当前查看：[出战角色 ▼]                                   │
├──────────┬──────────────────────────────────────────────┤
│ 纸娃娃   │ 属性对比（8 项 base→final，Δ 着色）+ 交战距离  │
│ 4×2 部位 │ Tab: 装备 | 宝箱 | 技能摘要                   │
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

- 按 roster 角色 Tab；左技能树、右插槽装配
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
