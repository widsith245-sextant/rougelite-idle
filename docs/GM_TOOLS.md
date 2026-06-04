# GM 工具（F8）

## 入口

- 全局快捷键 **F8**（`project.godot` → `toggle_gm`）
- `data/tables/meta/debug_settings.json` 中 `gmToolsEnabled: false` 可关闭
- 面板：`scenes/ui/gm/gm_combat_panel.tscn`，由 `game_root` 下 `GmToolsController` 挂载

## 分区

| 分区 | 功能 |
|------|------|
| 战斗控制 | 重启遭遇、满 gauge、强制 ally_a 行动、快速模式 |
| 效果实验室 | 下拉 effectId → 目标 → pile/intensity → 施加/清除 |
| 触发模拟 | OnDamaged / OnMoveEnd / OnForceSwap / OnTimeElapsed |
| 场景预设 | 读 `combat_scenarios.json` 一键加载 |
| 表现预览 | 选 category 预览跳字 |
| 调试 | 打印单位 ActiveEffects JSON |

## C# Debug API（CombatManager）

- `DebugRestartEncounter()` / `DebugFillGauges()` / `DebugForceAllyTurn(id)`
- `DebugApplyEffect(targetId, effectId, pile, intensity)`
- `DebugEmitTrigger(kind, unitId)` / `DebugLoadScenario(scenarioId)`
- `DebugPreviewDamageNumber(category, amount, worldX)`
- `GetDebugSnapshot()` / `GetUnitEffectsSnapshot(unitId)`

## scenario 格式

见 `data/tables/debug/combat_scenarios.json`：

```json
{
  "id": "mech_backstab",
  "label": "机制-背刺",
  "applyEffects": [
    { "targetId": "ally_a", "effectId": "BACKSTAB", "pile": 1, "intensity": 2 }
  ]
}
```

加载时：重启战斗 → 逐个 `DebugApplyEffect`。

## 与 BD 框定关系

BD1–7 仅在 scenario + `docs/BD_MECHANICS.md` 中框定，**不**写入奇境 Run 遗物/房间逻辑。
