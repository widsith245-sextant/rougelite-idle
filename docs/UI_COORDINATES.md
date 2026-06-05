# UI 坐标与层级规范

主视口固定 **400×150**。所有战斗 HUD、跳字、overlay 应通过 `CombatCoords`（`gdscript/combat/combat_coords.gd`）转换坐标，禁止在各处硬编码 `340` / `58` 等魔法数。

## Y 轴分区

| 区间 (px) | 用途 |
|-----------|------|
| 0–4 | 顶栏双血条 (`HudLayer`) |
| 4–112 | 战斗区 (`CombatStage` + `CombatWorld`) |
| 112–126 | 过渡带 (`CombatFillBg`，对齐进度条顶边) |
| 126–150 | Chrome：波次条 + QuickBar + SettingsDock |

## CanvasLayer 注册表

| layer | 节点 | 职责 |
|-------|------|------|
| 0 | `BackgroundLayer` | 训练/奇境背景 |
| 1 | `CombatStageLayer` | 战斗 Control + `CombatWorld` Node2D |
| 2 | `UnitOverheadLayer`（嵌套） | 单位头顶 HUD、伤害跳字 |
| 2 | `HudLayer` | 顶部 4px 血条 |
| 3 | `ChestBubbleHud` / `StageProgressLayer` / `QuickBarLayer` / `SettingsDockLayer` | 宝箱、波次、底栏 |
| 4 | `SkillCastOverlay` / `ChestRevealOverlay` / `RunHud` | 技能 toast、鉴定、奇境 HUD |
| 10 | `GmToolsLayer` | GM 面板 |

同层内按 `game_root.tscn` 子节点顺序叠放（后节点在上）。

## 三套 X 坐标

1. **Logic X**（C# `CombatUnitData.PositionX`）：碰撞、AI、停步判定  
2. **CombatWorld X**（Node2D `global_position.x`）：纸娃娃视觉；静止时 `logic_x == world_x`  
3. **Canvas HUD X**：`CombatCoords.doll_to_canvas_pos(doll, camera)` + 偏移  

### 锚点命名

| 名称 | 定义 |
|------|------|
| FeetAnchor | 脚底 Y = slot.y + FEET_OFFSET_Y (-32) |
| BodyAnchor | `get_anchor_position()` = feet + (0, -20) |
| HudAnchor | BodyAnchor + (-14, -30) |

### 停步 / 射程（logic 空间）

```
edge_dist = max(0, |dx| - r_attacker - r_target)
in_range  = edge_dist <= atk_range
stop_x    = enemy_x - (atk_range + ally_r + enemy_r)   // 友军在左
```

## OS 卫星窗

| 窗口 | 尺寸 | 停靠 |
|------|------|------|
| 主窗 | 400×150 | 屏幕右下 |
| 管理弹窗 | 640×480 | 主窗左侧（放不下则右侧） |
| 播报 | 400×72 | 主窗上方 |
| 日志 | 400×160 | 主窗下方 |
| 立绘 | 220×280 | 居中 |

绑定：`SatelliteWindow.ensure_transient_parent()` + `DisplayServer.window_set_transient`。

## 编排（无 Director）

- 管理弹窗：`PopupManager`（`GameRoot/PopupManager`）
- 卫星窗：各 `*WindowManager` C# autoload
- 战斗表现：`CombatStage` + `EventBus` 信号
- 新 overlay：优先挂 `UnitOverheadLayer`（HUD/跳字）或 `CombatWorld`（Node2D 调试绘制）

## 扩展约定

- 新战斗 VFX：经 `vfx_manager.gd`，使用 `CombatCoords` + `BattleCamera` 引用  
- 新 CanvasLayer：在本文档注册 layer 值，避免与 0–10 冲突  
- 禁止在 GDScript 重复定义 `ENEMY_ANCHOR_X`，应读 `CombatCoords` 或场景节点 `global_position`
