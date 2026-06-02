extends CanvasLayer

## Full-screen-ish chest identify reveal overlay.

@onready var _panel: PanelContainer = %Panel
@onready var _title: Label = %Title
@onready var _affix_box: VBoxContainer = %AffixBox

const QUALITY_PATH := "res://data/tables/loot/chest_quality.json"

var _quality_colors: Dictionary = {}
var _reveal_tween: Tween


func _ready() -> void:
	layer = 4
	visible = false
	process_mode = Node.PROCESS_MODE_DISABLED
	_load_quality_colors()
	if _panel:
		_panel.gui_input.connect(_on_panel_input)


func _load_quality_colors() -> void:
	if not FileAccess.file_exists(QUALITY_PATH):
		return
	var file := FileAccess.open(QUALITY_PATH, FileAccess.READ)
	if file == null:
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	for entry in parsed.get("qualities", []):
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		var qid: String = str(entry.get("id", ""))
		var colors: Array = entry.get("color", [0.55, 0.55, 0.6, 1.0])
		if colors.size() >= 3:
			_quality_colors[qid] = Color(float(colors[0]), float(colors[1]), float(colors[2]), float(colors[3] if colors.size() > 3 else 1.0))


func play_reveal(item_data: Dictionary) -> void:
	_finish(true)
	_populate(item_data)
	process_mode = Node.PROCESS_MODE_INHERIT
	visible = true
	if _panel:
		_panel.modulate.a = 0.0
		_panel.scale = Vector2(0.6, 0.6)
	_reveal_tween = create_tween()
	if _panel:
		_reveal_tween.tween_property(_panel, "modulate:a", 1.0, 0.12)
		_reveal_tween.parallel().tween_property(_panel, "scale", Vector2(1.15, 1.15), 0.18).set_trans(Tween.TRANS_BACK)
		_reveal_tween.tween_property(_panel, "scale", Vector2.ONE, 0.12)
	_reveal_tween.tween_interval(0.45)
	if _panel:
		_reveal_tween.tween_property(_panel, "modulate:a", 0.0, 0.2)
	_reveal_tween.tween_callback(_finish)


func _populate(item_data: Dictionary) -> void:
	var quality: String = str(item_data.get("quality", "common"))
	var qcol: Color = _quality_colors.get(quality, Color(0.55, 0.55, 0.6))
	if _title:
		_title.text = "[%s] %s  iLvl %d" % [
			quality,
			item_data.get("display_name", "?"),
			int(item_data.get("item_level", 0)),
		]
		_title.modulate = qcol
	if _affix_box:
		for child in _affix_box.get_children():
			child.queue_free()
		var affixes: Array = item_data.get("affixes", [])
		for i in affixes.size():
			var aff: Dictionary = affixes[i]
			var lbl := Label.new()
			lbl.text = "· %s +%.1f" % [aff.get("display_name", "?"), float(aff.get("value", 0))]
			lbl.modulate.a = 0.0
			lbl.add_theme_font_size_override("font_size", 10)
			_affix_box.add_child(lbl)
			var delay := 0.22 + i * 0.05
			get_tree().create_timer(delay).timeout.connect(func() -> void:
				if is_instance_valid(lbl):
					var tw := create_tween()
					tw.tween_property(lbl, "modulate:a", 1.0, 0.12)
			)
	if _panel:
		var style := StyleBoxFlat.new()
		style.bg_color = Color(0.1, 0.11, 0.14, 0.95)
		style.border_color = qcol
		style.set_border_width_all(3)
		_panel.add_theme_stylebox_override("panel", style)


func _finish(skip_tween_kill: bool = false) -> void:
	if not skip_tween_kill and _reveal_tween and _reveal_tween.is_valid():
		_reveal_tween.kill()
	_reveal_tween = null
	visible = false
	process_mode = Node.PROCESS_MODE_DISABLED
	var gate := get_node_or_null("/root/UiFxGate")
	if gate and gate.has_method("release"):
		gate.call("release")


func _on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		_finish()
		get_viewport().set_input_as_handled()
