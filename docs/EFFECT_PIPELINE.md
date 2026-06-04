# 效果结算管线

## 分批验收索引

| 批次 | 范围 | 验收标准 |
|------|------|----------|
| B1 | 战场碰撞 + 2+1 槽 | 敌人/友方不穿模；UI 2 主动 1 被动 |
| B2 | 结算核 | `CombatHitContext` + `ApplyDamage` 走 pipeline |
| B3 | 效果 Handler | 22 效果 Registry + `EmitCombatEffectApplied` |
| B4 | 触发总线 | `EffectTriggerBus` + 机制 scenario |
| B5 | 技能 v2 | `appliedEffects[]` + 富文本 Tooltip |
| B6 | 表现层 | 分类型跳字 + HUD 效果条 |
| GM | F8 面板 | 效果实验室 + scenario 加载 |

## 触发发射点

| 触发 | 发射位置 |
|------|----------|
| OnDealDamage | `CombatSettlementPipeline.ApplyOutgoingModifiers` |
| OnDamaged | `CombatSettlementPipeline.ApplyIncomingModifiers` |
| OnGaugeFull | `CombatManager.EnqueueReadyUnits`（入队时） |
| OnAction | `CombatActionExecutor.ExecuteTurn` 末尾 |
| OnMoveEnd | `CombatActionExecutor` 移动标签后 |
| OnForceSwap | `CombatActionExecutor` ForceSwap 后 |
| OnTimeElapsed | `CombatManager._Process` → `EffectTriggerBus.EmitToAllUnits` |

## Handler 状态（22 效果）

均已注册于 `Scripts/Combat/Effects/Handlers/`：

- **DirectModifiers**：ATK/DEF/VULN/SHIELD/SPEED/PARALYSIS/ACTION_POWER/CRIT 等
- **MarkEffects**：MARK_TAKEDAMAGE/TIMER/STACK_BURST/SPREAD、RESOURCE_STACK
- **ControlTactics**：STUN、CHARGE_ATTACK、BACKSTAB、SWAP_CURSE、ECHO、RECURSION

GM 面板可对任意 effectId 施加并观察 pile/intensity/shield。

## 相关文件

- 配表：`data/tables/combat/combat_effects.json`
- 场景：`data/tables/debug/combat_scenarios.json`
- UI  glossary：`data/tables/ui/effect_glossary.json`
- 文档：`docs/VFX_COMBAT.md`、`docs/GM_TOOLS.md`、`docs/BD_MECHANICS.md`
