extends Control

## Pending chest bubble: quality icon + accumulate pips (max 5). Click opens one chest.

const QUALITY_PATH := "res://data/tables/loot/chest_quality.json"

@onready var _panel: PanelContainer = %BubblePanel
@onready var _icon: ColorRect = %ChestIcon
@onready var _pip_row: HBoxContainer = %PipRow
@onready var _count_label: Label = %CountLabel
@onready var _tooltip: Label = %TooltipLabel

var _quality_colors: Dictionary = {}
var _quality_names: Dictionary = {}
var _max_accumulate: int = 5
var _current_quality: String = "common"
var _current_count: int = 0
var _hovering := false
var _opening := false
var _pulse_tween: Tween


func _apply_flat_panel(panel: PanelContainer) -> void:
	if panel == null:
		return
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.09, 0.11, 0.92)
	style.set_border_width_all(0)
	panel.add_theme_stylebox_override("panel", style)


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	_apply_flat_panel(_panel)
	_load_quality_table()
	_build_pips()
	_connect_bus()
	call_deferred("_refresh_from_loot")
	gui_input.connect(_on_gui_input)
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)
	if _panel:
		_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if _icon:
		_icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if _pip_row:
		for child in _pip_row.get_children():
			child.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if _count_label:
		_count_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if _tooltip:
		_tooltip.visible = false
		_tooltip.mouse_filter = Control.MOUSE_FILTER_IGNORE


func _connect_bus() -> void:
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus == null:
		return
	if event_bus.has_signal("PendingChestChanged"):
		event_bus.connect("PendingChestChanged", _on_pending_chest_changed)


func _load_quality_table() -> void:
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
		_quality_names[qid] = str(entry.get("displayName", qid))
		if qid == "common":
			_max_accumulate = int(entry.get("maxAccumulate", 5))


func _build_pips() -> void:
	if _pip_row == null:
		return
	for child in _pip_row.get_children():
		child.queue_free()
	for i in 5:
		var pip := ColorRect.new()
		pip.custom_minimum_size = Vector2(8, 8)
		pip.color = Color(0.25, 0.27, 0.32, 1)
		pip.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_pip_row.add_child(pip)


func _on_pending_chest_changed(quality: String, count: int) -> void:
	_current_quality = quality
	_current_count = count
	_apply_display()


func _refresh_from_loot() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		visible = false
		return
	if not loot.has_method("GetPendingChestSnapshot"):
		return
	var snap: Variant = loot.call("GetPendingChestSnapshot")
	if typeof(snap) != TYPE_DICTIONARY:
		visible = false
		return
	var data: Dictionary = snap
	_current_quality = str(data.get("quality", "common"))
	_current_count = int(data.get("count", 0))
	_max_accumulate = int(data.get("max", 5))
	_apply_display()


func _apply_display() -> void:
	visible = _current_count > 0
	if not visible:
		_stop_pulse()
		return
	var col: Color = _quality_colors.get(_current_quality, Color(0.55, 0.55, 0.6))
	if _icon:
		_icon.color = col
	if _count_label:
		_count_label.text = str(_current_count)
		_count_label.visible = _current_count > 1
	if _pip_row:
		for i in _pip_row.get_child_count():
			var pip: ColorRect = _pip_row.get_child(i)
			pip.color = col if i < _current_count else Color(0.22, 0.24, 0.28, 1)
	_update_tooltip()
	if not _opening:
		_start_pulse(col)


func _update_tooltip() -> void:
	if _tooltip == null:
		return
	var quality_name: String = str(_quality_names.get(_current_quality, _current_quality))
	_tooltip.text = "%s × %d/%d" % [quality_name, _current_count, _max_accumulate]
	_tooltip.visible = _hovering and _current_count > 0


func _on_mouse_entered() -> void:
	_hovering = true
	_update_tooltip()


func _on_mouse_exited() -> void:
	_hovering = false
	if _tooltip:
		_tooltip.visible = false


func _on_gui_input(event: InputEvent) -> void:
	if _opening:
		return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_open_one_chest()


func _open_one_chest() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	if not loot.has_method("OpenOnePendingChest"):
		return
	_opening = true
	var ok: bool = loot.call("OpenOnePendingChest", _current_quality)
	if ok:
		_play_opening_sequence()
	else:
		_opening = false


func _play_opening_sequence() -> void:
	if _panel == null:
		_opening = false
		return
	var tween := create_tween()
	tween.tween_property(_panel, "scale", Vector2(1.25, 1.25), 0.1)
	if _icon:
		tween.parallel().tween_property(_icon, "modulate", Color(2.0, 2.0, 2.0, 1.0), 0.08)
	tween.tween_property(_panel, "scale", Vector2.ONE, 0.12)
	if _icon:
		tween.parallel().tween_property(_icon, "modulate", Color.WHITE, 0.12)
	tween.tween_callback(func() -> void:
		_opening = false
		_refresh_from_loot()
	)


func _start_pulse(col: Color) -> void:
	_stop_pulse()
	if _panel == null:
		return
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.12, 0.13, 0.16, 0.92)
	style.border_color = col
	style.set_border_width_all(2)
	_panel.add_theme_stylebox_override("panel", style)
	_pulse_tween = create_tween().set_loops()
	_pulse_tween.tween_property(style, "border_color", col.lightened(0.25), 0.6)
	_pulse_tween.tween_property(style, "border_color", col, 0.6)


func _stop_pulse() -> void:
	if _pulse_tween and _pulse_tween.is_valid():
		_pulse_tween.kill()
	_pulse_tween = null
