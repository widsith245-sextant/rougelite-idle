extends Node2D

## Debug overlay: HitBox + attack range + stop line aligned to visual dolls.

const BAR_HALF_H := 22.0

const COLOR_ALLY_HITBOX := Color(0.2, 0.85, 0.45, 0.35)
const COLOR_ENEMY_HITBOX := Color(0.95, 0.4, 0.3, 0.35)
const COLOR_ALLY_RANGE := Color(0.25, 0.75, 1.0, 0.22)
const COLOR_ENEMY_RANGE := Color(1.0, 0.55, 0.2, 0.22)
const COLOR_STOP_LINE := Color(1.0, 0.92, 0.35, 0.75)

var _units: Array = []
var _stage: Node = null
var _ground_y := 58.0
var _show_ranges := true


func bind_stage(stage: Node) -> void:
	_stage = stage


func _ready() -> void:
	z_index = 20
	set_process(true)


func _process(_delta: float) -> void:
	if _stage and _stage.has_method("get_feet_world_y"):
		_ground_y = _stage.call("get_feet_world_y")
	elif _stage:
		var ally_slots: Node2D = _stage.get("%AllySlots") if _stage.has_method("get") else null
		if ally_slots:
			_ground_y = CombatCoords.feet_world_y(ally_slots)
	_refresh_units()
	queue_redraw()


func _refresh_units() -> void:
	_units.clear()
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null:
		return
	if combat.has_method("GetAllySnapshot"):
		var allies: Variant = combat.call("GetAllySnapshot")
		if typeof(allies) == TYPE_ARRAY:
			for entry in allies:
				if typeof(entry) == TYPE_DICTIONARY:
					_units.append(entry)
	if combat.has_method("GetEnemySnapshot"):
		var enemies: Variant = combat.call("GetEnemySnapshot")
		if typeof(enemies) == TYPE_ARRAY:
			for entry in enemies:
				if typeof(entry) == TYPE_DICTIONARY:
					_units.append(entry)


func _visual_x_for_unit(unit: Dictionary) -> float:
	var unit_id := str(unit.get("id", ""))
	var is_ally := bool(unit.get("is_ally", false))
	if _stage == null or unit_id.is_empty():
		return float(unit.get("position_x", 0.0))
	var nodes: Dictionary = _stage.get("_ally_nodes") if is_ally else _stage.get("_enemy_nodes")
	if nodes.has(unit_id):
		var doll = nodes.get(unit_id)
		if doll != null and is_instance_valid(doll):
			return doll.global_position.x
	return float(unit.get("position_x", 0.0))


func _ground_y_for_unit(unit: Dictionary) -> float:
	var unit_id := str(unit.get("id", ""))
	var is_ally := bool(unit.get("is_ally", false))
	if _stage != null and not unit_id.is_empty():
		var nodes: Dictionary = _stage.get("_ally_nodes") if is_ally else _stage.get("_enemy_nodes")
		if nodes.has(unit_id):
			var doll = nodes.get(unit_id)
			if doll != null and is_instance_valid(doll):
				return doll.global_position.y
	return _ground_y


func _draw() -> void:
	if not _show_ranges or _units.is_empty():
		return

	var allies: Array = []
	var enemies: Array = []
	for entry in _units:
		var unit: Dictionary = entry
		if bool(unit.get("is_ally", false)):
			allies.append(unit)
		else:
			enemies.append(unit)

	for unit in _units:
		_draw_unit(unit)

	for ally in allies:
		var target := _pick_target_enemy(ally, enemies)
		if target.is_empty():
			continue
		_draw_stop_line(ally, target)


func _draw_unit(unit: Dictionary) -> void:
	var pos_x := _visual_x_for_unit(unit)
	var unit_ground_y := _ground_y_for_unit(unit)
	var radius := float(unit.get("hit_box_radius", 10.0))
	var atk_range := float(unit.get("atk_range", 20.0))
	var is_ally := bool(unit.get("is_ally", false))

	var hit_color := COLOR_ALLY_HITBOX if is_ally else COLOR_ENEMY_HITBOX
	var range_color := COLOR_ALLY_RANGE if is_ally else COLOR_ENEMY_RANGE

	var hit_rect := Rect2(pos_x - radius, unit_ground_y - BAR_HALF_H, radius * 2.0, BAR_HALF_H * 2.0)
	draw_rect(hit_rect, hit_color, true)
	draw_rect(hit_rect, hit_color.lightened(0.2), false, 1.0)

	if is_ally:
		var range_start := pos_x + radius
		var range_rect := Rect2(range_start, unit_ground_y - BAR_HALF_H * 0.65, atk_range, BAR_HALF_H * 1.3)
		draw_rect(range_rect, range_color, true)
	else:
		var range_end := pos_x - radius
		var range_rect := Rect2(range_end - atk_range, unit_ground_y - BAR_HALF_H * 0.65, atk_range, BAR_HALF_H * 1.3)
		draw_rect(range_rect, range_color, true)


func _draw_stop_line(ally: Dictionary, enemy: Dictionary) -> void:
	var ally_x := _visual_x_for_unit(ally)
	var ally_r := float(ally.get("hit_box_radius", 10.0))
	var atk_range := float(ally.get("atk_range", 20.0))
	var enemy_x := _visual_x_for_unit(enemy)
	var enemy_r := float(enemy.get("hit_box_radius", 10.0))

	var stop_x := CombatCoords.stop_x_ally_vs_enemy(ally_x, ally_r, enemy_x, enemy_r, atk_range)
	var y0 := _ground_y - BAR_HALF_H - 4.0
	var y1 := _ground_y + BAR_HALF_H + 4.0
	draw_line(Vector2(stop_x, y0), Vector2(stop_x, y1), COLOR_STOP_LINE, 2.0)


func _pick_target_enemy(ally: Dictionary, enemies: Array) -> Dictionary:
	if enemies.is_empty():
		return {}
	var ally_x := float(ally.get("position_x", 0.0))
	var best: Dictionary = {}
	var best_dist := INF
	for enemy in enemies:
		var enemy_x := float(enemy.get("position_x", CombatCoords.ENEMY_ANCHOR_X))
		var dist := absf(enemy_x - ally_x)
		if dist < best_dist:
			best_dist = dist
			best = enemy
	return best
