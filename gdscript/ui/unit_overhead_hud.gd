extends Control

@onready var _hp_fill: ColorRect = %HpFill
@onready var _cd_fill: ColorRect = %CdFill
@onready var _effect_row: HBoxContainer = %EffectRow

var _unit_id: String = ""
var _is_ally: bool = true
var _combat: Node
var _effects: Array = []


func setup(unit_id: String, is_ally: bool) -> void:
	_unit_id = unit_id
	_is_ally = is_ally
	_combat = get_node_or_null("/root/CombatManager")
	mouse_filter = Control.MOUSE_FILTER_STOP if is_ally else Control.MOUSE_FILTER_IGNORE
	if _hp_fill:
		_hp_fill.color = Color(0.28, 0.86, 0.36, 1) if is_ally else Color(0.92, 0.34, 0.34, 1)
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("CombatEffectApplied"):
		if not event_bus.is_connected("CombatEffectApplied", _on_effect_applied):
			event_bus.connect("CombatEffectApplied", _on_effect_applied)
	_refresh()


func _gui_input(event: InputEvent) -> void:
	if not _is_ally or _unit_id.is_empty():
		return
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed and mb.button_index == MOUSE_BUTTON_LEFT:
			_open_portrait()
			get_viewport().set_input_as_handled()


func _open_portrait() -> void:
	var damaged := false
	if _combat and _combat.has_method("GetUnitHp"):
		var snap: Variant = _combat.call("GetUnitHp", _unit_id)
		if snap is Dictionary:
			var data: Dictionary = snap
			var cur := float(data.get("current", 1.0))
			var max_v := maxf(1.0, float(data.get("max", 1.0)))
			damaged = (cur / max_v) < 0.30 and cur > 0.0
	var mgr := get_node_or_null("/root/PortraitWindowManager")
	if mgr and mgr.has_method("ShowPortrait"):
		mgr.call("ShowPortrait", _unit_id, damaged)


func _process(_delta: float) -> void:
	_refresh_hp()
	_refresh_cd()
	_refresh_effects()


func set_anchor_global(world_pos: Vector2) -> void:
	global_position = world_pos + Vector2(-14, -30)


func _refresh() -> void:
	_refresh_hp()
	_refresh_cd()
	_refresh_effects()


func _on_effect_applied(target_id: String, effect_id: String, _display_name: String, category: String, pile: int, _intensity: float) -> void:
	if target_id != _unit_id:
		return
	_effects.append({ "effect_id": effect_id, "category": category, "pile": pile })
	if _effects.size() > 5:
		_effects = _effects.slice(_effects.size() - 5)
	_refresh_effects()


func _refresh_effects() -> void:
	if _effect_row == null:
		return
	for child in _effect_row.get_children():
		child.queue_free()
	if _combat and _combat.has_method("GetUnitEffectsSnapshot"):
		_effects = []
		var snap: Variant = _combat.call("GetUnitEffectsSnapshot", _unit_id)
		if typeof(snap) == TYPE_ARRAY:
			for entry in snap.slice(0, 5):
				if entry is Dictionary:
					_effects.append(entry)
	for entry in _effects:
		if entry is Dictionary:
			_add_effect_badge(entry)


func _add_effect_badge(data: Dictionary) -> void:
	var badge := ColorRect.new()
	badge.custom_minimum_size = Vector2(6, 6)
	badge.color = _color_for_category(str(data.get("category", data.get("effect_id", ""))))
	badge.tooltip_text = str(data.get("effect_id", ""))
	if int(data.get("pile", 1)) > 1:
		badge.tooltip_text += " x%d" % int(data.get("pile", 1))
	_effect_row.add_child(badge)


func _color_for_category(category: String) -> Color:
	match category:
		"debuff", "mark":
			return Color(1.0, 0.55, 0.25, 1.0)
		"control", "tactic":
			return Color(0.78, 0.62, 1.0, 1.0)
		"resource":
			return Color(0.4, 0.86, 0.66, 1.0)
		"buff":
			return Color(0.5, 0.78, 1.0, 1.0)
		_:
			return Color(0.85, 0.85, 0.85, 1.0)


func _refresh_hp() -> void:
	if _combat == null or _unit_id.is_empty() or _hp_fill == null:
		return
	var snap: Variant = _combat.call("GetUnitHp", _unit_id)
	if snap == null or typeof(snap) != TYPE_DICTIONARY:
		return
	var data: Dictionary = snap
	var cur := float(data.get("current", 0.0))
	var max_v := maxf(1.0, float(data.get("max", 1.0)))
	var ratio := clampf(cur / max_v, 0.0, 1.0)
	_hp_fill.size = Vector2(28.0 * ratio, 2.0)


func _refresh_cd() -> void:
	if _combat == null or _unit_id.is_empty() or _cd_fill == null:
		return
	var snap: Variant = _combat.call("GetUnitGaugeSnapshot", _unit_id)
	if snap == null or typeof(snap) != TYPE_DICTIONARY:
		return
	var data: Dictionary = snap
	var gauge := float(data.get("gauge", 0.0))
	var max_v := maxf(1.0, float(data.get("max", 100.0)))
	var ratio := clampf(gauge / max_v, 0.0, 1.0)
	_cd_fill.offset_top = 8.0 * (1.0 - ratio)
	_cd_fill.offset_bottom = 8.0
