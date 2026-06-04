# UI 资产替换工作流

> **更新**：2026-06-02  
> 与 [`data/tables/ui/ui_asset_manifest.json`](../data/tables/ui/ui_asset_manifest.json) 同步维护。

---

## 1. 原则

- **Label 用全名**：槽位、属性、按钮文字见 `slot_labels.json` / `stat_labels.json`。
- **简称只用于图标**：`iconAbbrev`、`compactIcon` 路径命名，不显示在 Label 上。
- 48×48 格：优先 **图标 + Tooltip 全名**，文字可 7px 双行 autowrap。

---

## 2. 目录约定

```
assets/ui/slots/      # 8 槽空态图标
assets/ui/items/      # 装备模板图标 {templateId}.png
assets/ui/chests/     # common / rare / epic
assets/ui/nav/        # QuickBar 可选图标
assets/ui/stats/      # 属性条可选小图标（非 Label 文字）
assets/characters/    # 立绘、纸娃娃贴图
resources/characters/ # *_visual.tres 引用上述贴图
```

首版目录已含 `.gitkeep`；未放图时代码仍用 `PlaceholderSpriteFactory` / 色块。

---

## 3. 替换步骤

### 步骤 A：新增资源文件

1. 按上表路径导出 PNG（建议 48×48 物品、32×32 槽位图标）。
2. 放入对应 `assets/...` 目录。

### 步骤 B：登记 manifest

编辑 [`ui_asset_manifest.json`](../data/tables/ui/ui_asset_manifest.json)：

- `targetPath`：实际资源路径  
- `status`：`placeholder` → `ready`  
- 新增条目时补全 `id`, `role`, `usedByScenes`

### 步骤 C：接线

| 资产类型 | 配置位置 | 代码读取点 |
|----------|----------|------------|
| 装备槽图标 | `slot_labels.json` → `iconPath` | 后续 `item_grid_cell` 可扩展读 `UiLabelsLoader.get_slot_icon_path` |
| 物品图标 | manifest `item_icon_template` 或按 id 命名 | `item_grid_cell` / `backpack_content` |
| 宝箱 | `assets/ui/chests/{quality}.png` | `item_grid_cell._apply_chest` |
| 纸娃娃 | `resources/characters/{rosterId}_visual.tres` | `CharacterVisualResource` 各 `@export Texture2D` |
| 立绘 | `assets/characters/portraits/` | `PlaceholderSpriteFactory` 或 Portrait 窗 |

### 步骤 D：自测清单

- [ ] 背包 8 槽空态：显示全名（武器/护甲/…），无单字简称  
- [ ] 背包有装备：物品全名或 Tooltip 完整  
- [ ] 未鉴定格：显示「宝箱」而非「箱」  
- [ ] 属性对比 8 项：中文全名标题  
- [ ] 三单位纸娃娃、战斗 HUD、宝箱气泡正常  
- [ ] 改表后更新 manifest 日期与 `WORK_IN_PROGRESS.md`（若批量替换）

### 步骤 E：提交

```bash
git add assets/ data/tables/ui/ docs/
git commit -m "ui: add slot icons for helmet/gloves"
```

远端机 `git pull` 后即可继续下一批资产。

---

## 4. manifest 条目速查

| id 前缀 | role | 当前状态 |
|---------|------|----------|
| `character_visual_*` | paper_doll | placeholder（色块） |
| `slot_icon_*` | equip_slot_empty | placeholder（文字全名） |
| `item_icon_*` | inventory_item | placeholder（品质色块） |
| `chest_icon_*` | chest_unidentified | placeholder |
| `quickbar_nav` | navigation | ready（文字按钮） |
| `theme_default` | theme | ready |

完整列表见 JSON 内 `assets` 数组。

---

## 5. 与策划文档联动

| 变更类型 | 同步更新 |
|----------|----------|
| 新增装备模板 | `equipment_templates.json` + `equipment_manifest.json` + `EQUIPMENT_LOOT_SNAPSHOT.md` |
| 改槽位中文名 | `slot_labels.json` + `EQUIPMENT_LOOT_SNAPSHOT` §1 |
| 仅换图不改名 | `ui_asset_manifest.json` + 本文件步骤 D |

---

## 6. 相关文档

- [`UI_INFORMATION_FLOW.md`](UI_INFORMATION_FLOW.md) — 信号与 refresh 链  
- [`UI_SPEC.md`](UI_SPEC.md) — 文案禁止简称规范  
- [`WORK_IN_PROGRESS.md`](WORK_IN_PROGRESS.md) — 任务板
