extends Control

@onready var _hp_fill: ColorRect = %HpFill
@onready var _cd_fill: ColorRect = %CdFill

var _unit_id: String = ""
var _combat: Node


func setup(unit_id: String, is_ally: bool) -> void:
	_unit_id = unit_id
	_combat = get_node_or_null("/root/CombatManager")
	if _hp_fill:
		_hp_fill.color = Color(0.28, 0.86, 0.36, 1) if is_ally else Color(0.92, 0.34, 0.34, 1)
	_refresh()


func _process(_delta: float) -> void:
	_refresh_hp()
	_refresh_cd()


func set_anchor_global(world_pos: Vector2) -> void:
	global_position = world_pos + Vector2(-14, -26)


func _refresh() -> void:
	_refresh_hp()
	_refresh_cd()


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
