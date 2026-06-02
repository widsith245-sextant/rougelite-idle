extends Control

const CATALOG_PATH := "res://data/tables/combat/stage_catalog.json"

@onready var _list: VBoxContainer = %StageList
@onready var _detail: Label = %DetailLabel

var _selected_stage_id: String = ""


func _ready() -> void:
	_build_list()
	_select_default()


func refresh() -> void:
	_build_list()


func _build_list() -> void:
	if _list == null:
		return
	for child in _list.get_children():
		child.queue_free()

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
		var ch: Dictionary = chapter
		var header := Label.new()
		header.text = str(ch.get("displayName", ch.get("chapterId", "章节")))
		header.add_theme_font_size_override("font_size", 11)
		_list.add_child(header)
		for level in ch.get("levels", []):
			if typeof(level) != TYPE_DICTIONARY:
				continue
			var lv: Dictionary = level
			var btn := Button.new()
			var sid: String = str(lv.get("stageId", ""))
			var title: String = str(lv.get("displayName", _format_stage_title(sid)))
			var rec: int = int(lv.get("recommendedLevel", 1))
			btn.text = "%s (推荐Lv%d)" % [title, rec]
			btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
			btn.pressed.connect(_on_stage_pressed.bind(sid, title, rec))
			_list.add_child(btn)


func _select_default() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("GetCurrentStageId"):
		_selected_stage_id = str(combat.call("GetCurrentStageId"))
		_set_detail("当前关卡：%s" % _selected_stage_id)


func _on_stage_pressed(stage_id: String, title: String, rec_level: int) -> void:
	_selected_stage_id = stage_id
	_set_detail("已选：%s | 推荐等级 %d" % [title, rec_level])
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("SetStageId"):
		combat.call("SetStageId", stage_id)
	var bootstrap := get_node_or_null("/root/SaveBootstrap")
	if bootstrap and bootstrap.has_method("RequestSave"):
		bootstrap.call("RequestSave")


func _set_detail(text: String) -> void:
	if _detail:
		_detail.text = text


func _format_stage_title(stage_id: String) -> String:
	if stage_id.is_empty():
		return "未知关卡"
	var parts := stage_id.split("_")
	if parts.size() >= 4 and parts[2] == "level":
		return "第%s关" % parts[3]
	return stage_id
