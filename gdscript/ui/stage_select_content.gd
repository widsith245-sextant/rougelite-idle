extends Control

const CATALOG_PATH := "res://data/tables/combat/stage_catalog.json"
const EARLY_CAPS_PATH := "res://data/tables/meta/early_game_caps.json"

@onready var _list: VBoxContainer = %StageList
@onready var _detail: Label = %DetailLabel
@onready var _hint: Label = %Hint

var _selected_stage_id: String = ""
var _snapshot: Array = []


func _ready() -> void:
	_load_snapshot()
	_update_early_band_hint()
	_build_list()
	_select_default()
	var bus := get_node_or_null("/root/EventBus")
	if bus:
		if bus.has_signal("StageUnlocked"):
			bus.stage_unlocked.connect(_on_progression_changed)
		if bus.has_signal("StageCleared"):
			bus.stage_cleared.connect(_on_progression_changed)


func refresh() -> void:
	_load_snapshot()
	_build_list()


func _on_progression_changed(_stage_id: String = "") -> void:
	_load_snapshot()
	_build_list()


func _load_snapshot() -> void:
	_snapshot.clear()
	var prog := get_node_or_null("/root/StageProgressionManager")
	if prog and prog.has_method("GetSnapshot"):
		var raw: Variant = prog.call("GetSnapshot")
		if typeof(raw) == TYPE_ARRAY:
			_snapshot = raw
			return

	if not FileAccess.file_exists(CATALOG_PATH):
		return
	var file := FileAccess.open(CATALOG_PATH, FileAccess.READ)
	if file == null:
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	for chapter in parsed.get("chapters", []):
		if typeof(chapter) != TYPE_DICTIONARY:
			continue
		for level in chapter.get("levels", []):
			if typeof(level) != TYPE_DICTIONARY:
				continue
			var lv: Dictionary = level
			_snapshot.append({
				"stage_id": str(lv.get("stageId", "")),
				"recommended_level": int(lv.get("recommendedLevel", 1)),
				"unlock_from": str(lv.get("unlockFrom", "")),
				"unlocked": true,
				"cleared": false,
				"exp": 0,
				"gold": 0,
			})


func _update_early_band_hint() -> void:
	if _hint == null or not FileAccess.file_exists(EARLY_CAPS_PATH):
		return
	var file := FileAccess.open(EARLY_CAPS_PATH, FileAccess.READ)
	if file == null:
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	var caps: Dictionary = parsed
	var level_cap := int(caps.get("playerInitialLevelCap", 10))
	var band: Dictionary = caps.get("earlyStageBand", {})
	var max_stage := int(band.get("maxStageLevel", 3))
	var reward_mul := float(band.get("rewardMultiplier", 1.0))
	_hint.text = "开战前选择关卡（选中后立即生效）\n前期 band：Lv1–%d / 前%d关奖励×%.1f" % [
		level_cap, max_stage, reward_mul,
	]


func _build_list() -> void:
	if _list == null:
		return
	for child in _list.get_children():
		child.queue_free()

	if _snapshot.is_empty():
		return

	var chapter_header := Label.new()
	chapter_header.text = "第一章"
	chapter_header.add_theme_font_size_override("font_size", 11)
	_list.add_child(chapter_header)

	for entry in _snapshot:
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		var row: Dictionary = entry
		var sid: String = str(row.get("stage_id", ""))
		if sid.is_empty():
			continue
		var title := _format_stage_title(sid)
		var rec: int = int(row.get("recommended_level", 1))
		var unlocked: bool = bool(row.get("unlocked", false))
		var cleared: bool = bool(row.get("cleared", false))
		var btn := Button.new()
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		if unlocked:
			var prefix := "✓ " if cleared else ""
			btn.text = "%s%s (推荐Lv%d)" % [prefix, title, rec]
			btn.pressed.connect(_on_stage_pressed.bind(sid, title, rec, row))
		else:
			btn.text = "🔒 %s · 需通关%s" % [title, _format_stage_title(str(row.get("unlock_from", "")))]
			btn.disabled = true
		_list.add_child(btn)


func _select_default() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("GetCurrentStageId"):
		_selected_stage_id = str(combat.call("GetCurrentStageId"))
		_refresh_detail_for_stage(_selected_stage_id)


func _on_stage_pressed(stage_id: String, title: String, rec_level: int, row: Dictionary) -> void:
	_selected_stage_id = stage_id
	_refresh_detail_for_row(title, rec_level, row)
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("SetStageId"):
		combat.call("SetStageId", stage_id)
	var bootstrap := get_node_or_null("/root/SaveBootstrap")
	if bootstrap and bootstrap.has_method("RequestSave"):
		bootstrap.call("RequestSave")


func _refresh_detail_for_stage(stage_id: String) -> void:
	for entry in _snapshot:
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		var row: Dictionary = entry
		if str(row.get("stage_id", "")) == stage_id:
			_refresh_detail_for_row(_format_stage_title(stage_id), int(row.get("recommended_level", 1)), row)
			return
	_set_detail("当前关卡：%s" % stage_id)


func _refresh_detail_for_row(title: String, rec_level: int, row: Dictionary) -> void:
	var exp := float(row.get("exp", 0))
	var gold := int(row.get("gold", 0))
	var cleared: bool = bool(row.get("cleared", false))
	var unlocked: bool = bool(row.get("unlocked", false))
	var status := "已通关" if cleared else ("已解锁" if unlocked else "未解锁")
	_set_detail("已选：%s | 推荐Lv%d | 奖励 %.0f exp / %d 金 | %s" % [
		title, rec_level, exp, gold, status,
	])


func _set_detail(text: String) -> void:
	if _detail:
		_detail.text = text


func _format_stage_title(stage_id: String) -> String:
	if stage_id.is_empty():
		return "上一关"
	var parts := stage_id.split("_")
	if parts.size() >= 4 and parts[2] == "level":
		return "第%s关" % parts[3]
	return stage_id
