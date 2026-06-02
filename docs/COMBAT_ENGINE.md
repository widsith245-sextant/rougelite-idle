# 战斗引擎

## 核心组件

| 类 | 职责 |
|----|------|
| `ActionQueue` | 按 Speed 读条，就绪单位入队 |
| `BattlefieldController` | X 轴战场：行军、逼近、Charge/Retreat |
| `StageRunController` | 训练关：行军 → 波次触发 → 交战 → 复位 |
| `CombatActionExecutor` | 执行主动技、结算伤害 |
| `PassiveSkillResolver` | 监听换位触发被动 |
| `EventBus` | 向表现层广播信号 |

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

`MoveTag`：`Charge` / `Retreat`，带 `distance` 像素。

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
