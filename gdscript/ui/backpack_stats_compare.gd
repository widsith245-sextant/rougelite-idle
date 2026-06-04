extends Control

## Eight-stat compare strip: base → final (Δ colored).

@onready var _grid: GridContainer = %StatGrid
@onready var _range_hint: Label = %RangeHint

var _value_nodes: Dictionary = {}
var _equip_hint: Label
var _preview_item: Dictionary = {}


func _ready() -> void:
	_build_labels()
	_equip_hint = Label.new()
	_equip_hint.add_theme_font_size_override("font_size", 8)
	_equip_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	if _range_hint and _range_hint.get_parent():
		_range_hint.get_parent().add_child(_equip_hint)


func refresh(unit_id: String, preview_item: Dictionary = {}) -> void:
	_preview_item = preview_item
	var stats := get_node_or_null("/root/StatsService")
	if stats == null:
		return
	var snap: Variant = stats.call("GetSnapshotWithComparison", unit_id)
	if typeof(snap) != TYPE_DICTIONARY:
		return
	_apply_snapshot(snap)
	_refresh_range_hint(snap)
	_refresh_equip_hint(unit_id)


func _build_labels() -> void:
	if _grid == null:
		return
	for key in UiLabelsLoader.STAT_COMPARE_KEYS:
		var box := VBoxContainer.new()
		box.add_theme_constant_override("separation", 0)
		var title := Label.new()
		title.text = UiLabelsLoader.get_stat_display_name(key)
		title.add_theme_font_size_override("font_size", 8)
		var value := Label.new()
		value.name = "Val_%s" % key
		value.text = "-"
		value.add_theme_font_size_override("font_size", 9)
		box.add_child(title)
		box.add_child(value)
		_grid.add_child(box)
		_value_nodes[key] = value


func _apply_snapshot(snap: Dictionary) -> void:
	for key in UiLabelsLoader.STAT_COMPARE_KEYS:
		var node: Label = _value_nodes.get(key)
		if node == null:
			continue
		var base_val: float = float(snap.get("base_%s" % key, 0.0))
		var final_val: float = float(snap.get("final_%s" % key, 0.0))
		var delta: float = float(snap.get("delta_%s" % key, 0.0))
		node.text = _format_compare(key, base_val, final_val, delta)
		if absf(delta) > 0.001:
			node.add_theme_color_override("font_color", Color(0.4, 0.95, 0.5) if delta > 0 else Color(0.95, 0.45, 0.4))
		else:
			node.remove_theme_color_override("font_color")


func _format_compare(key: String, base_val: float, final_val: float, _delta: float) -> String:
	var b := _format_single(key, base_val)
	var f := _format_single(key, final_val)
	if absf(final_val - base_val) < 0.001:
		return f
	return "%s → %s" % [b, f]


func _format_single(key: String, raw: float) -> String:
	if key == "CritRate":
		return "%.0f%%" % (raw * 100.0)
	if key == "Level":
		return str(int(raw))
	if key in ["MaxHp", "Damage", "Dps"]:
		return "%.0f" % raw
	return "%.1f" % raw


func _refresh_range_hint(snap: Dictionary) -> void:
	if _range_hint == null:
		return
	var class_id: String = str(snap.get("class_id", ""))
	var rules := _load_engagement_rules()
	var enemy_x: float = float(rules.get("enemyAnchorX", 340.0))
	var atk_range: float = float(snap.get("final_AtkRange", snap.get("base_AtkRange", 0.0)))
	if atk_range <= 0.0:
		for entry in rules.get("classes", []):
			if str(entry.get("classId", "")) == class_id:
				atk_range = float(entry.get("atkRange", atk_range))
				break
	var stop_x: float = enemy_x - atk_range
	var dist: float = maxf(0.0, enemy_x - stop_x)
	_range_hint.text = "距木桩约 %.0f | 停步 X≈%.0f | 射程 %.0f" % [dist, stop_x, atk_range]


func _refresh_equip_hint(unit_id: String) -> void:
	if _equip_hint == null:
		return
	if _preview_item.is_empty():
		_equip_hint.text = ""
		_equip_hint.remove_theme_color_override("font_color")
		return
	var slot := str(_preview_item.get("slot", ""))
	if slot != "Weapon" and slot != "Armor":
		_equip_hint.text = ""
		_equip_hint.remove_theme_color_override("font_color")
		return
	var item_class := str(_preview_item.get("class_id", ""))
	if item_class.is_empty() or item_class == "any":
		_equip_hint.text = ""
		_equip_hint.remove_theme_color_override("font_color")
		return
	var loot := get_node_or_null("/root/LootManager")
	var bag_index := int(_preview_item.get("_bag_index", -1))
	if loot and bag_index >= 0 and loot.has_method("CanEquipByBagIndex"):
		var can: bool = loot.call("CanEquipByBagIndex", bag_index, unit_id)
		var err := str(loot.call("GetLastEquipError")) if loot.has_method("GetLastEquipError") else ""
		if can:
			_equip_hint.text = "可穿戴（%s 专属）" % item_class
			_equip_hint.add_theme_color_override("font_color", Color(0.4, 0.95, 0.5))
		else:
			_equip_hint.text = err if not err.is_empty() else "职业不符，无法穿戴"
			_equip_hint.add_theme_color_override("font_color", Color(0.55, 0.55, 0.58))
		return
	var party := get_node_or_null("/root/PartyManager")
	var unit_class := str(party.call("GetClassIdForUnit", unit_id)) if party else ""
	if unit_class == item_class:
		_equip_hint.text = "可穿戴（%s 专属）" % item_class
		_equip_hint.add_theme_color_override("font_color", Color(0.4, 0.95, 0.5))
	else:
		_equip_hint.text = "职业不符，无法穿戴（需 %s）" % item_class
		_equip_hint.add_theme_color_override("font_color", Color(0.55, 0.55, 0.58))


func _load_engagement_rules() -> Dictionary:
	const PATH := "res://data/tables/combat/engagement_rules.json"
	if not FileAccess.file_exists(PATH):
		return {}
	var file := FileAccess.open(PATH, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}
