extends Control

## Right-side compact stat strip (leader ally_a).

const LEADER_ID := "ally_a"
const DISPLAY_KEYS: Array[String] = UiLabelsLoader.get_stat_compare_keys()

@onready var _grid: GridContainer = %StatGrid


func _ready() -> void:
	_build_labels()
	call_deferred("_connect_stats")
	_refresh_poll()


func _process(delta: float) -> void:
	_poll_accum += delta
	if _poll_accum >= 0.4:
		_poll_accum = 0.0
		_refresh_from_service()


var _poll_accum: float = 0.0


func _build_labels() -> void:
	for key in DISPLAY_KEYS:
		var box := VBoxContainer.new()
		box.add_theme_constant_override("separation", 0)
		var title := Label.new()
		title.text = UiLabelsLoader.get_stat_display_name(key)
		title.add_theme_font_size_override("font_size", 7)
		var value := Label.new()
		value.name = "Val_%s" % key
		value.text = "-"
		value.add_theme_font_size_override("font_size", 8)
		box.add_child(title)
		box.add_child(value)
		_grid.add_child(box)


func _connect_stats() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null:
		return
	_refresh_from_service()


func _refresh_poll() -> void:
	_refresh_from_service()


func _refresh_from_service() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null:
		return
	var snap: Variant = combat.call("GetUnitStatsSnapshot", LEADER_ID)
	if typeof(snap) != TYPE_DICTIONARY:
		return
	_apply_snapshot(snap)


func _apply_snapshot(snap: Dictionary) -> void:
	if _grid == null:
		return
	for key in DISPLAY_KEYS:
		var node := _grid.find_child("Val_%s" % key, true, false)
		if node == null:
			continue
		var raw: float = float(snap.get(key, 0.0))
		if key == "CritRate":
			node.text = "%.0f%%" % (raw * 100.0)
		elif key == "Level":
			node.text = str(int(raw))
		elif key in ["MaxHp", "Damage", "Dps"]:
			node.text = "%.0f" % raw
		else:
			node.text = "%.1f" % raw
