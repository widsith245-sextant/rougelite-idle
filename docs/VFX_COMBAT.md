# 战斗跳字与 VFX 规范

## displayTag / category 对照

| 类型 | category | 颜色 | 前缀/样式 |
|------|----------|------|-----------|
| 物理伤害 | physical | 白/浅黄 14px | 纯数字 |
| 魔法伤害 | magical | 蓝紫 | 纯数字 |
| 标记追加 | mark | 橙红 | `+N` |
| 护盾吸收 | shield | cyan | `吸收 N` |
| 反伤 | retaliate | 红边 | `反 N` |
| 治疗 | heal | 绿色 | `+N` |
| 暴击 | 任意 + isCrit | 放大 + `!` | 字号 16px |

## 多段伤害

- `vfx_manager.spawn_damage_number_staggered(amount, anchor_node, opts)`：跳字挂在 `UnitOverheadLayer`，坐标经 `CombatCoords.doll_to_canvas_pos` + `HUD_DAMAGE_OFFSET`
- `vfx_manager.spawn_damage_fly_line(source, target, category)`：source→target 飞线，颜色与 category 一致
- 禁止仅在 `VfxManager` 下用固定 `global_position` 生成跳字（目标移动后会向右/左飘离）
- 支持 `sequence_index` 水平 spread（12px/段）；同帧多 hit 使用 `STAGGER_DELAY = 0.08s` 错开
- `opts.target_id` 用于 per-target 序号计数
- `combat_stage._resolve_damage_anchor` 解析 `_enemy_nodes` / `_ally_nodes` 后传入 VfxManager

## HUD 效果条

- `unit_overhead_hud` 的 `EffectRow` 最多 5 个色块
- 同步 `CombatManager.GetUnitEffectsSnapshot` 与 `CombatEffectApplied` 信号
- pile>1 时在 tooltip 显示层数

## 事件字段

`EventBus.DamageDealt(sourceId, targetId, amount, isCrit, damageType, displayTag)`

Handler 通过 `CombatHitContext.DisplayTag` 设置 displayTag，pipeline 发射事件。
