# 装备与开箱规则快照

> **生成日期**：2026-06-02  
> **用途**：核对当前实现的 8 槽装备、职业限定、全部模板与开箱/鉴定规则。  
> **维护**：改 `data/tables/loot/equipment_templates.json` 或穿戴/掉落逻辑后，请同步更新本文档与 `data/tables/loot/equipment_manifest.json`。

**代码/配表依据**：

- 槽位枚举：`Scripts/Core/Enums/SlotType.cs`
- 背包 UI 槽序：`gdscript/ui/backpack_content.gd`
- 穿戴/鉴定/背包：`Scripts/Loot/LootManager.cs`
- 职业解析：`Scripts/Meta/PartyManager.cs`
- 装备模板：`data/tables/loot/equipment_templates.json`
- 机器可读清单：`data/tables/loot/equipment_manifest.json`

---

## 1. 单角色 8 装备槽

每个战斗单位 `unitId`（`ally_a` / `ally_b` / `ally_c`）拥有**独立**的 8 槽装备字典；同槽最多 1 件。

| 序号 | `SlotType` | UI 显示名（全名） | 职业限定穿戴 | 鉴定模板数 |
|------|------------|------------------|--------------|------------|
| 1 | Weapon | 武器 | **是**（`classId` 须匹配单位职业） | 5 |
| 2 | Armor | 护甲 | **是** | 5 |
| 3 | Helmet | 头盔 | 否 | 1 |
| 4 | Gloves | 手套 | 否 | 1 |
| 5 | Boots | 靴子 | 否 | 1 |
| 6 | HeadAccessory | 头部饰品 | 否 | **0**（槽位存在，池内无模板） |
| 7 | BackAccessory | 背部饰品 | 否 | 1 |
| 8 | Trinket | 饰品 | 否 | 1 |

显示名来源：[`data/tables/ui/slot_labels.json`](../data/tables/ui/slot_labels.json)（禁止单字简称 Label，简称仅用于图标资源 `iconAbbrev`）。

**合计模板**：15 件（覆盖 7 种有产出的槽位）。

---

## 2. 战斗单位与职业

### 2.1 背包 UI 固定三单位

| unitId | 默认 rosterId | classId | 显示名 | 新档默认出战 |
|--------|---------------|---------|--------|--------------|
| ally_a | vanguard_a | Vanguard_01 | 先锋剑士 | 是 |
| ally_b | sniper_b | Sniper_01 | 狙击手 | 否（需 DB `db_roster_sniper` 等解锁并入队） |
| ally_c | mage_c | Mage_01 | 大法师 | 否（需 DB `db_roster_mage` 等解锁并入队） |

映射代码：`PartyManager` 中 `RosterIdForUnit`（ally_a→vanguard_a，ally_b→sniper_b，ally_c→mage_c）。

### 2.2 名册五职业（`roster.json`）

| rosterId | classId | 显示名 | 解锁 DB 节点 |
|----------|---------|--------|--------------|
| vanguard_a | Vanguard_01 | 先锋剑士 | （默认） |
| sniper_b | Sniper_01 | 狙击手 | db_roster_sniper |
| mage_c | Mage_01 | 大法师 | db_roster_mage |
| support_d | Support_01 | 支援医师 | db_roster_support |
| berserker_e | Berserker_01 | 狂战士 | db_roster_berserker |

**说明**：出战位最多 3（`PartyManager.MaxActiveSlots` 由 DB 解锁）。Support / Berserker 编入某一 `ally_*` 槽后，`GetClassIdForUnit` 返回对应 `classId`，方可穿其专属武器/护甲。`char_class_base.json` 中 Support / Berserker 的 `allyUnitId` 为空，仅表示无固定默认槽位。

---

## 3. 职业限定穿戴规则

### 3.1 规则摘要

| 条件 | 行为 |
|------|------|
| 部位非 Weapon / Armor | 任意单位可穿（仅须槽位匹配） |
| Weapon / Armor 且模板无 `classId` 或 `classId` = `any` | 任意单位可穿 |
| Weapon / Armor 且模板有 `classId` | **仅**当 `PartyManager.GetClassIdForUnit(unitId)` 与模板 `classId` 相等时可穿 |
| 背包存放 | **不**校验职业；任意职业装备可进 48 格背包 |
| 穿上 / 拖拽至装备槽 | 校验部位 + 职业；失败返回 `GetLastEquipError()`（如「职业不符，无法穿戴」） |
| 替换装备 | 旧装备回背包前须 `HasBagSpace()`；满包则拒绝替换 |
| 卸下 | 须背包未满 |

### 3.2 专属武器/护甲可穿戴矩阵

「✓」= 该职业单位可穿；「—」= 穿上时拒绝。

| 模板 classId | 制式长剑 / 重甲 | 狙击步枪 / 轻甲外披 | 奥术法杖 / 法师长袍 | 医杖 / 医护背心 | 狂战斧 / 链甲 |
|--------------|-----------------|---------------------|----------------------|-----------------|---------------|
| Vanguard_01 | ✓ | — | — | — | — |
| Sniper_01 | — | ✓ | — | — | — |
| Mage_01 | — | — | ✓ | — | — |
| Support_01 | — | — | — | ✓ | — |
| Berserker_01 | — | — | — | — | ✓ |

**通用部位**（巡逻头盔、战术手套、行军靴、夜翼披风、幸运符）：**全员可穿**（无 `classId`）。

---

## 4. 全部装备模板（15 件）

来源：`data/tables/loot/equipment_templates.json`。

### 4.1 职业专属（10 件）

| id | 显示名 | 槽位 | classId | iLvl | 白值 [min,max] | 固定词缀概要 |
|----|--------|------|---------|------|----------------|--------------|
| vanguard_sword_t1 | 制式长剑 | Weapon | Vanguard_01 | 5 | 14–20 | 攻击 8–14；暴击 3–6% |
| vanguard_plate_t1 | 重甲胸铠 | Armor | Vanguard_01 | 6 | 22–32 | 护甲 15–25；生命 30–50 |
| sniper_rifle_t1 | 狙击步枪 | Weapon | Sniper_01 | 5 | 18–26 | 攻击 10–16；暴击 5–10% |
| sniper_vest_t1 | 轻甲外披 | Armor | Sniper_01 | 5 | 12–18 | 护甲 8–14 |
| mage_staff_t1 | 奥术法杖 | Weapon | Mage_01 | 5 | 16–24 | 攻击 12–18 |
| mage_robe_t1 | 法师长袍 | Armor | Mage_01 | 5 | 10–16 | 护甲 6–12；生命 20–40 |
| support_rod_t1 | 医杖 | Weapon | Support_01 | 5 | 10–16 | 攻击 6–10；生命 25–45 |
| support_vest_t1 | 医护背心 | Armor | Support_01 | 5 | 14–20 | 护甲 10–18 |
| berserker_axe_t1 | 狂战斧 | Weapon | Berserker_01 | 6 | 20–28 | 攻击 12–20 |
| berserker_chain_t1 | 链甲 | Armor | Berserker_01 | 6 | 18–26 | 护甲 12–20 |

### 4.2 通用（5 件）

| id | 显示名 | 槽位 | classId | iLvl | 白值 [min,max] | 备注 |
|----|--------|------|---------|------|----------------|------|
| common_helmet_t1 | 巡逻头盔 | Helmet | — | 4 | 8–14 | 护甲词缀 |
| common_gloves_t1 | 战术手套 | Gloves | — | 4 | 6–10 | 攻击词缀 |
| common_boots_t1 | 行军靴 | Boots | — | 4 | 5–9 | 速度词缀 |
| wing_t1_01 | 夜翼披风 | BackAccessory | — | 8 | 4–8 | effectId `001` |
| common_trinket_t1 | 幸运符 | Trinket | — | 4 | 3–6 | 暴击词缀 |

### 4.3 鉴定额外词缀

- 模板固定词缀：按上表 `affixes` 在鉴定时 RNG  roll。
- 宝箱品质额外条数：见下节 `chest_quality.json` 的 `bonusAffixCount`；从 `item_affix_pool.json` 按槽位加权抽取（`AffixPoolLoader.RollForSlot`），失败时回退模板词缀重复 roll。

### 4.4 白值映射到属性（`StatsService`）

| 槽位 | 白值 `rolled_base_stat` 计入 |
|------|------------------------------|
| Weapon | Damage |
| Armor, Helmet, Gloves, Boots | Defense |
| BackAccessory, Trinket | MoveSpeed（×0.5） |
| HeadAccessory 及其他默认 | Damage |

---

## 5. 开箱与鉴定规则

### 5.1 流程概览

```
击杀敌人 → GrantKillReward（概率掉落待领宝箱）
         → AddPendingChest（按 common/rare/epic 分品质累积）
         → 同品质 count ≥ 5 → 自动 OpenOnePendingChest → 进入「未鉴定」队列
背包鉴定 → IdentifyNext（消耗队首未鉴定箱，随机模板生成装备进背包）
```

新档：`new_game.json` 提供 3 个无 quality 的「盲盒宝箱」，直接进入未鉴定队列。

### 5.2 待领宝箱（Pending）

| 品质 id | 显示名 | 最大累积 | iLvl 加成 | 额外词缀数 | 产出品质下限 |
|---------|--------|----------|-----------|------------|--------------|
| common | 普通宝箱 | 5 | 0 | 0 | common |
| rare | 稀有宝箱 | 5 | 2 | 1 | rare |
| epic | 史诗宝箱 | 5 | 4 | 2 | epic |

- 每种品质**独立计数**；满 5 自动开 1 个并扣减计数。
- `FlushPendingToInventory` / `FlushAllPendingToInventory` 可将待领一次性灌入未鉴定队列（结算等场景）。

### 5.3 击杀掉落（`drop_tables.json` → `killRewards`）

| 敌人 id | 经验 | 金币 | 宝箱概率 | 基础 quality |
|---------|------|------|----------|--------------|
| training_dummy | 8 | 3 | 12% | common |
| training_elite | 25 | 12 | 35% | rare |
| training_boss | 80 | 35 | 80% | epic |
| goblin_scout | 12 | 5 | 15% | common |
| goblin_archer | 14 | 6 | 18% | common |
| orc_brute | 22 | 10 | 28% | rare |

**rewardTier 抬升**（`enemy_templates.json` + `ProgressionManager.ResolveChestQualityWithRewardTier`）：

- 敌人 `rewardTier` ≥ 3：实际品质至少 **rare**
- 敌人 `rewardTier` ≥ 5：实际品质至少 **epic**
- 与上表 `quality` 取**较高**档

示例 rewardTier：training_dummy=1，goblin_scout/archer=2，training_elite/orc_brute=3，training_boss=5。

### 5.4 鉴定（IdentifyNext）

| 规则 | 说明 |
|------|------|
| 前置 | 未鉴定队列非空，且 `identifiedItems.Count < 48` |
| 满包 | **不消耗**宝箱，返回 null，错误「背包已满」 |
| 模板选择 | 从全部 15 模板 **均匀随机** 1 件（与职业无关） |
| 物品等级 | `currentStageLevel` + 宝箱 `itemLevelBonus` |
| 训练奖励 | 每次鉴定后 10% 再入 1 未鉴定箱（`drop_tables.training`，全局限 5 次） |

背包容量：`early_game_caps.json` → `bagSlotsVisible` = **48**（`LootManager.BagCapacity`）。

### 5.5 UI 交互（背包）

| 操作 | 规则 |
|------|------|
| 鉴定 / 批量鉴定 | `CanIdentify()` = 有待鉴定且未满包 |
| 背包→装备槽拖拽 | `slot_type` 须与目标槽一致；再经 `EquipByBagIndex` |
| 装备槽→空背包格 | 须 `HasBagSpace()` |
| 选中背包格 | 详情 + `CanEquipByBagIndex` 提示；属性条 `StatsCompare` 显示可穿戴/职业不符 |

---

## 6. 已知缺口（核对用）

1. **HeadAccessory**：UI 有「头部饰品」槽，鉴定池无模板，正常流程无法获得头部饰品。  
2. **鉴定 RNG**：全局均匀随机，非按单位职业或 rewardTier 权重。  
3. **出战位与名册**：5 职业名册 vs 最多 3 出战位；Support/Berserker 需解锁并编入 `ally_*` 后才可在背包 UI 穿专属装。  
4. **装饰槽 UX**：未单独标注「暂未开放」；仅槽位可点但无产出。

---

## 7. 建议验收清单

- [ ] 每名出战角色左侧 8 槽 UI 与上表一致  
- [ ] 本职业武器/护甲可穿；跨职业武器/护甲拒绝并有提示  
- [ ] 通用头盔/手套/靴子/背部饰品/饰品五职业均可穿  
- [ ] 背包 48/48 时鉴定与批量鉴定不消耗宝箱  
- [ ] 满包时卸装、替换装备失败有提示  
- [ ] 击杀后待领宝箱累积，满 5 自动转入未鉴定队列  
- [ ] 高 rewardTier 敌人掉落宝箱品质不低于 rare/epic 规则  

---

**相关文档**：UI 信息流 [`UI_INFORMATION_FLOW.md`](UI_INFORMATION_FLOW.md)；资产替换 [`UI_ASSET_WORKFLOW.md`](UI_ASSET_WORKFLOW.md)；进行中任务 [`WORK_IN_PROGRESS.md`](WORK_IN_PROGRESS.md)；里程碑 [`MILESTONES.md`](MILESTONES.md)。
