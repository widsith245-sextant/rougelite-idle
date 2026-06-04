# BD 机制框定（非奇境）

本阶段仅在 GM scenario + 文档中框定 BD 组合，**不接入** `RunRelicManager` 或奇境房间。

## BD1–7 方向（框定）

| ID | 标签 | 核心效果组合 | GM scenario |
|----|------|--------------|-------------|
| BD1 | 反击流 | SHIELD_WITH_RETALIATION + DEF_PERCENT_UP | `bd1_tank_retaliate` |
| BD2 | 持续 DOT | MARK_TIMER + MARK_TAKEDAMAGE | `bd2_dot_curse` |
| BD3 | 背刺爆发 | BACKSTAB + CRIT_RATE_UP | `mech_backstab` |
| BD4 | 换位诅咒 | SWAP_CURSE + VULNERABILITY | `mech_swap_curse` |
| BD5 | 定时压制 | MARK_TIMER + RESOURCE_STACK | `mech_mark_timer` |
| BD6 | 冲锋连击 | CHARGE_ATTACK + ATK_PERCENT_UP | `mech_charge_attack` |
| BD7 | 连动复读 | ECHO + RECURSION | （待增 scenario） |

## 机制验收 scenario

| scenario | 验证点 |
|----------|--------|
| `mech_backstab` | 源 X > 目标 X 时 +30% 并叠 MARK |
| `mech_swap_curse` | ForceSwap 后相邻敌人 VULN |
| `mech_mark_timer` | 每 2s 扣血 pile-- |
| `mech_charge_attack` | Charge 结束追加普攻 |

使用 F8 GM 面板 → 加载场景 → 触发模拟按钮验收。
