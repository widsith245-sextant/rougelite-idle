# Rougelite Idle

Godot 4.6 极简桌面挂机刷宝。核心逻辑 **C# Autoload**，UI / 战斗表现 / 纸娃娃 **GDScript**。

- 主战斗视口：**400 × 150** 无边框桌面窗（不置顶，不可缩放）
- 功能页弹窗：**640 × 480** 独立 OS 窗口

## 快速开始

1. 用 Godot 4.6+（.NET 版）打开项目并编译 C#
2. 运行 `scenes/main/game_root.tscn`（无系统标题栏的小窗贴桌面）
3. 观察自动战斗：行军推进 → 波次交战 → 复位；底部进度条与左上宝箱气泡
4. 点击右下角 **≡ 菜单** → **背包**：换角、属性对比、鉴定/穿装

## Cursor + MCP 开发

本项目已配置双 Godot MCP（见 [`.cursor/mcp.json`](.cursor/mcp.json)）：

- **godot-cli**：CLI 打开编辑器、运行项目、抓取调试输出
- **godot-editor**：编辑器内实时操作（需 Godot 已开且右上角 **MCP Connected**）

首次使用：重启 Cursor → Settings → MCP 确认两服务器在线 → 用 Godot 打开本项目并 Restart Project。Agent 改完代码后会按 `.cursor/skills/godot-verify/SKILL.md` 自动验收（`scripts/verify.ps1` + MCP 运行主场景）。

## 文档

| 文档 | 说明 |
|------|------|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | 双线架构、四层 UI、模块边界 |
| [COMBAT_ENGINE.md](docs/COMBAT_ENGINE.md) | 阵型、移动标签、被动连携 |
| [DATA_SCHEMA.md](docs/DATA_SCHEMA.md) | `data/tables/` JSON 字段 |
| [UI_SPEC.md](docs/UI_SPEC.md) | QuickBar、Popup、背包布局、文案规范 |
| [UI_INFORMATION_FLOW.md](docs/UI_INFORMATION_FLOW.md) | UI 信息流与信号链 |
| [UI_ASSET_WORKFLOW.md](docs/UI_ASSET_WORKFLOW.md) | 资产替换工作流 |
| [MILESTONES.md](docs/MILESTONES.md) | M1–M6 验收标准 |
| [STATS_SYSTEM.md](docs/STATS_SYSTEM.md) | 属性字典与派生公式 |
| [CHANGELOG.md](docs/CHANGELOG.md) | 里程碑变更记录 |

## 目录概览

```
Scripts/          C# Autoload（战斗、掉落、Meta、存档）
gdscript/         GDScript 表现层
data/tables/      正式配置表
resources/        CharacterVisual 等资源
archive/          废弃参考（.gdignore，不参与构建）
docs/             项目文档
```

## Autoload

`EventBus` · `StatsService` · `CombatManager` · `LootManager` · `MetaManager` · `PartyManager` · `ProgressionManager` · `SaveManager` · `OfflineIdleManager`
