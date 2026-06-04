# 战斗引擎

## 核心组件

| 类 | 职责 |
|----|------|
| `ActionQueue` | 按 Speed 读条，就绪单位入队 |
| `BattlefieldController` | X 轴战场：行军、逼近、Charge/Retreat/ForceSwap |
| `StageRunController` | 训练关：行军 → 波次触发 → 交战 → 复位 |
| `CombatActionExecutor` | Gauge 主技能、普攻、换位技、伤害结算 |
| `PassiveSkillResolver` | 换位/前线被动 |
| `ActiveSkillTriggerResolver` | X 轴位移、ForceSwap、编组换位主动触发 |
| `EventBus` | 向表现层广播信号 |

## 六技能槽与触发模型

每角色 **2 主动 + 1 被动** 装配（`active_0..1` / `passive_0`）；技能树仍保留 6 节点可选。

| 槽位 | 触发 |
|------|------|
| `active_0` | ActionGauge 满（主技能 CD） |
| `active_1..3` | 普攻计数达 `triggerParam.basicAttackThreshold` |
| 含 `OnXMove` | Charge/Retreat 位移事件 |
| 含 `OnForceSwap` | ForceSwap 阵位互换 |
| 被动 | `OnFrontLine` / `OnAnyAllyMoved` / `OnSquadSwap` / `OnXMove` 等 |

Gauge 未满时普攻造成 **0.35×** 基础伤害并递增 `BasicAttackCounter`。

## 三种换位语义

| 类型 | 触发点 |
|------|--------|
| X 轴 Charge/Retreat | `PositionChangeEvent` → Resolver |
| 编组换位 | `PartyManager.SwapSquadSlots` → `EventBus.SquadSwapped` |
| ForceSwap | 主动技 `moveTags: ForceSwap` → `BattlefieldController.ApplyForceSwap` |

## 训练关阶段循环

`stage_training.json` 定义跑道长度、行军速度、波次摆点（0–1 进度）。

```
Marching ──(进度到达)──► Engaging ──(敌人全灭)──► WaveClearing ──► Marching
                              │                        │
                         发 WaveStarted            复位 initialPositionX
```

- **Marching**：友军 `MarchRight`，底部进度条推进；无敌人、无读条技能
- **Engaging**：现有混合战斗（逼近 + 普攻 + 读条技能）
- **WaveClearing**：0.35s 缓冲后友军 X 回到初始阵型（40/72/104）

敌人由 `enemy_templates.json` 按波次 `enemyId` 生成，不再常驻木桩无限复活。

## 阵型与 X 轴

```
友军 ←———————— 跑道 ————————► 敌人锚点 X≈340
ally_a(40)  ally_b(72)  ally_c(104)
```

- 单位间 **HitBox 碰撞**：`BattlefieldController.ApplySeparation` 禁止穿模
- 敌人仍攻击最前友方；**远程**单位在 `AtkRange` 内停步即可输出
- `MoveTag`：`Charge` / `Retreat` / `ForceSwap`

## 经济与掉落

杀怪/通关奖励读 `drop_tables.json` → `ProgressionManager.GrantKillReward` / `GrantStageComplete`。

宝箱先进入 `LootManager` pending 队列，主窗体气泡积攒（按品质最多 5），满格或点击 flush 进背包。

## 编队

`PartyManager` + `roster.json`：三槽 `ally_a/b/c` 映射 rosterId → classId/displayName。`EncounterTableLoader` 与背包/技能 UI 均读此配置。

## 表现层 API

| C# 方法 | 用途 |
|---------|------|
| `GetAllySnapshot()` | 编队 id + position_x |
| `GetUnitHp(unitId)` | `{ current, max }` |
| `IsMarching` | 是否行军态（技能特写隐藏） |
| `StageRun.RunProgress` | 底部进度条 |

## EventBus 战斗信号

- `PositionChanged(entityId, oldX, newX)`
- `RunProgressChanged(progress, waveIndex, waveTotal)`
- `WaveStarted(index)` / `WaveCleared(index)`
- `MarchStateChanged(isMarching)`
- `PendingChestChanged(quality, count)` / `LootInventoryChanged()`
- `DamageDealt` / `UnitHpChanged` / `CombatActionStarted`
