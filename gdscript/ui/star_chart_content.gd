extends Control

const POPUP_TEXTS_PATH := "res://data/tables/ui/popup_texts.json"

@onready var _grid: GridContainer = %Grid
@onready var _footer: Label = %Hint


func _ready() -> void:
	_apply_texts()


func refresh() -> void:
	_apply_texts()


func _apply_texts() -> void:
	var data := _load_section("star_chart")
	if data.is_empty():
		return
	if _grid:
		for child in _grid.get_children():
			child.queue_free()
		var nodes: Array = data.get("nodes", [])
		for node_text in nodes:
			var btn := Button.new()
			btn.custom_minimum_size = Vector2(80, 48)
			btn.text = str(node_text)
			btn.disabled = true
			_grid.add_child(btn)
	if _footer:
		_footer.text = str(data.get("footer", ""))


func _load_section(section: String) -> Dictionary:
	if not FileAccess.file_exists(POPUP_TEXTS_PATH):
		return {}
	var file := FileAccess.open(POPUP_TEXTS_PATH, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}
	return parsed.get(section, {})
