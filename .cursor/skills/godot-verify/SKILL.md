---
name: godot-verify
description: >-
  验收 Godot 4.6 项目改动：dotnet build、MCP 运行主场景、抓取调试输出。
  在完成 Scripts/gdscript/scenes/data 修改后自动使用；用户提及 godot、mcp、验收、verify、运行项目、改完检验时触发。
---

# Godot 改后验收

## 常量

| 项 | 值 |
|----|-----|
| 项目根 | `E:/rougelite-idle` |
| 主场景 | `res://scenes/main/game_root.tscn` |
| C# 编译 | `scripts/verify.ps1` |

## 验收清单

```
- [ ] 1. [C# 有改动] 运行 scripts/verify.ps1
- [ ] 2. [编辑器未开] godot-cli → launch_editor(project_path)
- [ ] 3. [编辑器已开] 确认 MCP Connected；否则提示 Restart Project
- [ ] 4. godot-cli → run_project(project_path, scene=主场景)
- [ ] 5. 等待 3–5 秒（C# 首次构建可延长至 15 秒）
- [ ] 6. godot-cli → get_debug_output
- [ ] 7. [可选] godot-editor → 读 Output/Debugger 错误
- [ ] 8. godot-cli → stop_project
- [ ] 9. 汇报：编译结果 + 日志摘要 + 通过/失败
```

## 按改动类型

| 改动 | 必做 | 可选（编辑器在线） |
|------|------|-------------------|
| `Scripts/**/*.cs` | verify.ps1 + CLI run/debug | godot-editor console |
| `gdscript/**/*.gd` / `scenes/**/*.tscn` | CLI run/debug | scene tree、脚本校验 |
| `data/tables/**/*.json` | CLI run/debug | — |

## 错误判定

日志含以下任一则**未通过**：`ERROR`、`SCRIPT ERROR`、`Parse Error`、`Failed to`、`Exception`、`CS\d{4}`

## MCP 工具选用

- 编译/运行/日志 → **godot-cli**（`launch_editor`, `run_project`, `get_debug_output`, `stop_project`, `get_godot_version`）
- 场景编辑/实时状态 → **godot-editor**（需 WebSocket 已连接）
