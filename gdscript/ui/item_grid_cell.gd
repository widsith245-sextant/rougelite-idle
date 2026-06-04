extends PanelContainer

## Single inventory grid cell with optional quality border, drag-and-drop, hover.

signal cell_pressed(index: int)
signal cell_hovered(index: int, entered: bool)
signal cell_dropped(target_index: int, payload: Dictionary)

@export var cell_index: int = -1
@export var cell_size: Vector2 = Vector2(48, 48)
@export var drag_kind: String = ""
@export var slot_type_name: String = ""
@export var roster_id: String = ""

const QUALITY_PATH := "res://data/tables/loot/chest_quality.json"

var _icon: ColorRect
var _name_label: Label
var _count_label: Label
var _quality_colors: Dictionary = {}

var _pending_item: Array = []
var _pending_chest: Array = []
var _pending_slot: String = ""
var _current_quality: String = ""
var _has_item: bool = false


func _ready() -> void:
	custom_minimum_size = cell_size
	_load_quality_colors()
	_cache_nodes()
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)
	gui_input.connect(_on_gui_input)
	clear_cell()
	_flush_pending()


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


func _cache_nodes() -> void:
	_icon = %Icon
	_name_label = %NameLabel
	_count_label = %CountLabel
	if _name_label:
		_name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_name_label.max_lines_visible = 2
		_name_label.add_theme_font_size_override("font_size", 7)


func _set_name_label_text(text: String) -> void:
	if _name_label == null:
		return
	_name_label.text = text


func _flush_pending() -> void:
	if _pending_item.size() >= 2:
		var q: String = str(_pending_item[3]) if _pending_item.size() > 3 else ""
		_apply_item(_pending_item[0], _pending_item[1], _pending_item[2] if _pending_item.size() > 2 else 1, q)
		_pending_item.clear()
	if _pending_chest.size() >= 1:
		var q2: String = str(_pending_chest[1]) if _pending_chest.size() > 1 else "common"
		_apply_chest(str(_pending_chest[0]), q2)
		_pending_chest.clear()
	if not _pending_slot.is_empty():
		_apply_slot(_pending_slot)
		_pending_slot = ""


func setup_item(display_name: String, item_level: int = 0, count: int = 1, quality: String = "") -> void:
	if not is_node_ready():
		_pending_item = [display_name, item_level, count, quality]
		return
	_apply_item(display_name, item_level, count, quality)


func _apply_item(display_name: String, item_level: int, count: int, quality: String) -> void:
	if _name_label == null:
		return
	_has_item = true
	_set_name_label_text(display_name)
	tooltip_text = display_name
	_count_label.text = str(count) if count > 1 else ""
	_count_label.visible = count > 1
	var q: String = quality if not quality.is_empty() else _quality_from_level(item_level)
	_icon.color = _color_for_quality(q)
	_apply_quality_border(q)


func setup_chest(display_name: String, quality: String = "common") -> void:
	if not is_node_ready():
		_pending_chest = [display_name, quality]
		return
	_apply_chest(display_name, quality)


func _apply_chest(_display_name: String, quality: String) -> void:
	if _name_label == null:
		return
	_has_item = true
	_set_name_label_text("宝箱")
	tooltip_text = _display_name if not _display_name.is_empty() else "未鉴定宝箱"
	_count_label.visible = false
	_current_quality = quality
	_icon.color = _color_for_quality(quality)
	_apply_quality_border(quality)
	_start_chest_glow()


func setup_slot(slot_label: String) -> void:
	if not is_node_ready():
		_pending_slot = slot_label
		return
	_apply_slot(slot_label)


func setup_roster(display_name: String, roster_id_param: String = "") -> void:
	if not is_node_ready():
		_pending_item = [display_name, 0, 1, PlaceholderSpriteFactory.quality_for_roster(roster_id_param)]
		roster_id = roster_id_param
		return
	roster_id = roster_id_param
	_has_item = not roster_id.is_empty()
	_set_name_label_text(display_name)
	tooltip_text = display_name
	_count_label.visible = false
	var q := PlaceholderSpriteFactory.quality_for_roster(roster_id)
	_icon.color = PlaceholderSpriteFactory.color_for_roster(roster_id)
	_apply_quality_border(q)


func _apply_slot(slot_label: String) -> void:
	if _name_label == null:
		return
	_has_item = false
	_set_name_label_text(slot_label)
	tooltip_text = slot_label
	_count_label.visible = false
	_icon.color = Color(0.35, 0.4, 0.55, 1)
	add_theme_stylebox_override("panel", _slot_style())
	_current_quality = ""


func clear_cell() -> void:
	if _name_label == null:
		return
	_has_item = false
	slot_type_name = ""
	_name_label.text = ""
	_count_label.visible = false
	_icon.color = Color(0.18, 0.2, 0.24, 1)
	_current_quality = ""
	remove_theme_stylebox_override("panel")


func _color_for_quality(quality: String) -> Color:
	if _quality_colors.has(quality):
		return _quality_colors[quality]
	return _color_for_level(0)


func _quality_from_level(item_level: int) -> String:
	if item_level >= 8:
		return "epic"
	if item_level >= 5:
		return "rare"
	return "common"


func _color_for_level(item_level: int) -> Color:
	return _color_for_quality(_quality_from_level(item_level))


func _apply_quality_border(quality: String) -> void:
	if quality.is_empty():
		remove_theme_stylebox_override("panel")
		return
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.15, 0.17, 0.22, 0.9)
	style.border_color = _color_for_quality(quality)
	style.set_border_width_all(2)
	add_theme_stylebox_override("panel", style)


func _start_chest_glow() -> void:
	if _current_quality.is_empty() or _icon == null:
		return
	var base: Color = _icon.color
	var tween := create_tween().set_loops()
	tween.tween_property(_icon, "modulate", base.lightened(0.15), 0.8)
	tween.tween_property(_icon, "modulate", Color.WHITE, 0.8)


func _slot_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.15, 0.17, 0.22, 0.9)
	style.border_color = Color(0.9, 0.75, 0.3, 1)
	style.set_border_width_all(2)
	return style


func _on_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		cell_pressed.emit(cell_index)


func _on_mouse_entered() -> void:
	cell_hovered.emit(cell_index, true)


func _on_mouse_exited() -> void:
	cell_hovered.emit(cell_index, false)


func _get_drag_data(_at_position: Vector2) -> Variant:
	if drag_kind.is_empty():
		return null
	if drag_kind == "bag" and not _has_item:
		return null
	if drag_kind == "equip_slot" and not _has_item:
		return null
	if drag_kind == "bench" and roster_id.is_empty():
		return null
	if drag_kind == "squad_slot" and roster_id.is_empty():
		return null
	if drag_kind == "skill_slot":
		var skill_node_id: String = str(get_meta("node_id", ""))
		if skill_node_id.is_empty():
			return null

	var payload := {
		"drag_kind": drag_kind,
		"cell_index": cell_index,
		"slot_type": slot_type_name,
		"roster_id": roster_id,
	}
	if drag_kind == "bag" and has_meta("class_id"):
		payload["class_id"] = str(get_meta("class_id", ""))
	if drag_kind == "squad_slot":
		payload["drag_kind"] = "roster"
		payload["source"] = "squad_slot"
	elif drag_kind == "bench":
		payload["drag_kind"] = "roster"
		payload["source"] = "bench"
	elif drag_kind == "skill_slot":
		payload["node_id"] = str(get_meta("node_id", ""))
		payload["slot_key"] = str(get_meta("slot_key", ""))
		payload["roster_id"] = str(get_meta("roster_id", ""))
	set_drag_preview(_make_drag_preview())
	return payload


func _can_drop_data(_at_position: Vector2, data: Variant) -> bool:
	if typeof(data) != TYPE_DICTIONARY:
		return false
	var payload: Dictionary = data
	var kind: String = str(payload.get("drag_kind", ""))
	if drag_kind == "equip_slot" and kind == "bag":
		if slot_type_name.is_empty():
			return false
		return str(payload.get("slot_type", "")) == slot_type_name
	if drag_kind == "bag" and kind == "equip_slot":
		if _has_item:
			return false
		return _loot_has_bag_space()
	if drag_kind == "squad_slot" and kind == "roster":
		return true
	if drag_kind == "skill_slot":
		var slot_key: String = str(get_meta("slot_key", ""))
		if kind == "skill_node":
			var node_type: String = str(payload.get("node_type", "active"))
			if str(payload.get("roster_id", "")) != str(get_meta("roster_id", "")):
				return false
			if node_type == "passive":
				return slot_key == "passive_0"
			return slot_key.begins_with("active_")
		if kind == "skill_slot":
			return str(payload.get("roster_id", "")) == str(get_meta("roster_id", ""))
	return false


func _drop_data(_at_position: Vector2, data: Variant) -> void:
	if typeof(data) != TYPE_DICTIONARY:
		return
	cell_dropped.emit(cell_index, data)


func _make_drag_preview() -> Control:
	var preview := PanelContainer.new()
	preview.custom_minimum_size = cell_size
	var label := Label.new()
	label.text = _name_label.text if _name_label else "?"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	preview.add_child(label)
	return preview


func _loot_has_bag_space() -> bool:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return false
	return bool(loot.call("HasBagSpace"))
