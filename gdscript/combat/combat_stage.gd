extends Control

## Multi-enemy combat stage visuals.

const STAGE_HEIGHT := 120
const TWEEN_DURATION := 0.15
const MICRO_MOVE_DURATION := 0.04
const SNAP_MOVE_LOGIC_DELTA := 0.5
const ENEMY_ANCHOR_X := 340.0
const MARCH_CAMERA_OFFSET := 70.0
const ENGAGE_CAMERA_OFFSET := 50.0
const MARCH_ZOOM := Vector2(0.92, 0.92)
const ENGAGE_ZOOM := Vector2(1.12, 1.12)
const OverheadHudScene := preload("res://scenes/ui/components/unit_overhead_hud.tscn")

@onready var _overhead_layer: CanvasLayer = %UnitOverheadLayer
@onready var _ally_slots: Node2D = %AllySlots
@onready var _enemy_spawn: Node2D = %EnemySpawn
@onready var _vfx_manager: Node2D = %VfxManager
@onready var _camera: Camera2D = %BattleCamera
@onready var _stage_bg: ColorRect = %StageBg

const TRAINING_STAGE_TINT := Color(1.0, 1.0, 1.0, 1.0)
const WONDERLAND_STAGE_TINT := Color(0.85, 0.75, 1.0, 1.0)

var _ally_nodes: Dictionary = {}
var _enemy_nodes: Dictionary = {}
var _unit_positions: Dictionary = {}
var _overhead_nodes: Dictionary = {}
var _hit_stop_timer: float = 0.0
var _is_marching: bool = true
var _camera_zoom: Vector2 = Vector2.ONE


func _ready() -> void:
	custom_minimum_size = Vector2(400, STAGE_HEIGHT)
	_hide_placeholder_slots()
	call_deferred("_setup_allies_from_manager")
	_connect_event_bus(get_node_or_null("/root/EventBus"))


func _process(delta: float) -> void:
	if _hit_stop_timer > 0.0:
		_hit_stop_timer -= delta
	_update_camera_follow()
	if _is_marching:
		_apply_march_walk()
	_refresh_overhead_positions()


func _connect_event_bus(event_bus: Node) -> void:
	if event_bus == null:
		return
	if event_bus.has_signal("PositionChanged"):
		event_bus.connect("PositionChanged", _on_position_changed)
	if event_bus.has_signal("DamageDealt"):
		event_bus.connect("DamageDealt", _on_damage_dealt)
	if event_bus.has_signal("CombatEffectApplied"):
		event_bus.connect("CombatEffectApplied", _on_combat_effect_applied)
	if event_bus.has_signal("CombatStateChanged"):
		event_bus.connect("CombatStateChanged", _on_combat_state_changed)
	if event_bus.has_signal("WaveStarted"):
		event_bus.connect("WaveStarted", _on_wave_started)
	if event_bus.has_signal("WaveCleared"):
		event_bus.connect("WaveCleared", _on_wave_cleared)
	if event_bus.has_signal("MarchStateChanged"):
		event_bus.connect("MarchStateChanged", _on_march_state_changed)
	if event_bus.has_signal("SquadChanged"):
		event_bus.connect("SquadChanged", _on_squad_changed)
	if event_bus.has_signal("UnitHpChanged"):
		event_bus.connect("UnitHpChanged", _on_unit_hp_changed)
	if event_bus.has_signal("RunVisualModeChanged"):
		event_bus.connect("RunVisualModeChanged", _on_run_visual_mode_changed)


func _on_march_state_changed(is_marching: bool) -> void:
	_is_marching = is_marching
	if is_marching:
		_apply_march_walk()
	else:
		_stop_march_walk()


func _on_run_visual_mode_changed(mode: String) -> void:
	if _stage_bg == null:
		return
	var target := WONDERLAND_STAGE_TINT if mode == "wonderland" else TRAINING_STAGE_TINT
	var tw := create_tween()
	tw.tween_property(_stage_bg, "modulate", target, 0.3)


func _on_squad_changed() -> void:
	call_deferred("_setup_allies_from_manager")


func _apply_march_walk() -> void:
	for entity_id in _ally_nodes.keys():
		var doll: Node2D = _ally_nodes[entity_id]
		if doll and doll.has_method("play_walk"):
			doll.play_walk()


func _stop_march_walk() -> void:
	for entity_id in _ally_nodes.keys():
		var doll: Node2D = _ally_nodes[entity_id]
		if doll and doll.has_method("play_idle"):
			doll.play_idle()


func _hide_placeholder_slots() -> void:
	for child in _ally_slots.get_children():
		child.visible = false
	var enemy_block := _enemy_spawn.get_node_or_null("EnemyBlock")
	if enemy_block:
		enemy_block.visible = false


func _setup_allies_from_manager() -> void:
	var combat_manager := get_node_or_null("/root/CombatManager")
	if combat_manager == null:
		return

	for entity_id in _ally_nodes.keys():
		var node: Node = _ally_nodes[entity_id]
		node.queue_free()
	_ally_nodes.clear()
	for entity_id in _overhead_nodes.keys():
		if _enemy_nodes.has(entity_id):
			continue
		var hud: Node = _overhead_nodes[entity_id]
		hud.queue_free()
		_overhead_nodes.erase(entity_id)
	_unit_positions.clear()

	var snapshot: Array = combat_manager.call("GetAllySnapshot")
	for entry in snapshot:
		var entity_id: String = entry.get("id", "")
		var pos_x: float = float(entry.get("position_x", 40.0))
		_spawn_ally(entity_id, pos_x)


func _spawn_ally(entity_id: String, pos_x: float) -> void:
	var doll: Node2D = CharacterBase.spawn(_ally_slots, entity_id, _logic_to_ally_local_x(pos_x))
	_ally_nodes[entity_id] = doll
	_unit_positions[entity_id] = doll.get_anchor_position() if doll.has_method("get_anchor_position") else doll.global_position
	_spawn_overhead(entity_id, true)


func _sync_enemies_from_manager() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null or not combat.has_method("GetEnemySnapshot"):
		return

	var snapshot: Array = combat.call("GetEnemySnapshot")
	var seen: Dictionary = {}
	for entry in snapshot:
		var entity_id: String = str(entry.get("id", ""))
		if entity_id.is_empty():
			continue
		seen[entity_id] = true
		var logic_x: float = float(entry.get("position_x", ENEMY_ANCHOR_X))
		var local_x := logic_x - ENEMY_ANCHOR_X
		if _enemy_nodes.has(entity_id):
			var existing: Node2D = _enemy_nodes[entity_id]
			if existing and is_instance_valid(existing):
				existing.position.x = local_x
			continue
		var doll: Node2D = CharacterBase.spawn(_enemy_spawn, entity_id, local_x)
		if doll:
			doll.modulate = Color(0.95, 0.55, 0.35)
			_enemy_nodes[entity_id] = doll
			_unit_positions[entity_id] = doll.get_anchor_position()
			_spawn_overhead(entity_id, false)

	for entity_id in _enemy_nodes.keys():
		if seen.has(entity_id):
			continue
		_despawn_enemy(entity_id)


func _despawn_enemy(entity_id: String) -> void:
	if _enemy_nodes.has(entity_id):
		var node: Node = _enemy_nodes[entity_id]
		if is_instance_valid(node):
			node.queue_free()
		_enemy_nodes.erase(entity_id)
	if _overhead_nodes.has(entity_id):
		var hud: Node = _overhead_nodes[entity_id]
		if is_instance_valid(hud):
			hud.queue_free()
		_overhead_nodes.erase(entity_id)
	_unit_positions.erase(entity_id)


func _despawn_all_enemies() -> void:
	for entity_id in _enemy_nodes.keys():
		_despawn_enemy(entity_id)


func _on_wave_started(_wave_index: int) -> void:
	_sync_enemies_from_manager()


func _on_wave_cleared(_wave_index: int) -> void:
	_despawn_all_enemies()


func _on_unit_hp_changed(entity_id: String, current_hp: float, _max_hp: float) -> void:
	if current_hp <= 0.0 and _enemy_nodes.has(entity_id):
		_despawn_enemy(entity_id)


func _on_position_changed(entity_id: String, old_x: float, new_x: float) -> void:
	var logic_delta := absf(new_x - old_x)
	if _enemy_nodes.has(entity_id):
		var enemy_doll: Node2D = _enemy_nodes[entity_id]
		if enemy_doll and is_instance_valid(enemy_doll):
			_apply_logic_x_to_node(enemy_doll, new_x - ENEMY_ANCHOR_X, logic_delta)
			_unit_positions[entity_id] = enemy_doll.get_anchor_position() if enemy_doll.has_method("get_anchor_position") else enemy_doll.global_position
		return
	if not _ally_nodes.has(entity_id):
		return

	var doll: Node2D = _ally_nodes[entity_id]
	var local_x := _logic_to_ally_local_x(new_x)
	_apply_logic_x_to_node(doll, local_x, logic_delta)
	if logic_delta >= 20.0 and doll.has_method("play_reposition_trail"):
		doll.play_reposition_trail()
	_unit_positions[entity_id] = doll.get_anchor_position() if doll.has_method("get_anchor_position") else doll.global_position


func _apply_logic_x_to_node(node: Node2D, local_x: float, logic_delta: float) -> void:
	if logic_delta <= SNAP_MOVE_LOGIC_DELTA:
		node.position.x = local_x
		return
	var dur := MICRO_MOVE_DURATION if logic_delta < 8.0 else TWEEN_DURATION
	if node.has_method("move_to_x"):
		node.move_to_x(local_x, dur)
	else:
		node.position.x = local_x


func _logic_to_ally_local_x(logic_x: float) -> float:
	return logic_x - _ally_slots.global_position.x


func _on_damage_dealt(
	_source_id: String,
	target_id: String,
	amount: float,
	is_crit: bool = false,
	damage_type: String = "",
	display_tag: String = "",
) -> void:
	if _vfx_manager == null:
		return

	var anchor: Node2D = _resolve_damage_anchor(target_id)
	var category := display_tag if not display_tag.is_empty() else damage_type
	var opts := {
		"category": category if not category.is_empty() else "physical",
		"is_crit": is_crit,
		"target_id": target_id,
	}
	if anchor != null and _vfx_manager.has_method("spawn_damage_number_staggered"):
		_vfx_manager.spawn_damage_number_staggered(amount, anchor, opts)
	elif _vfx_manager.has_method("spawn_damage_number"):
		var world_pos: Vector2 = _unit_positions.get(target_id, _enemy_spawn.global_position)
		_vfx_manager.spawn_damage_number(amount, world_pos, opts)

	if is_crit and _ally_nodes.has(_source_id):
		var doll: Node = _ally_nodes[_source_id]
		if doll.has_method("apply_hit_stop"):
			doll.apply_hit_stop(0.05)
			_hit_stop_timer = 0.05


func _resolve_damage_anchor(target_id: String) -> Node2D:
	if target_id.is_empty():
		return null
	if _enemy_nodes.has(target_id):
		var enemy: Node2D = _enemy_nodes[target_id]
		if enemy != null and is_instance_valid(enemy):
			return enemy
	if _ally_nodes.has(target_id):
		var ally: Node2D = _ally_nodes[target_id]
		if ally != null and is_instance_valid(ally):
			return ally
	return null


func _on_combat_effect_applied(_target_id: String, _effect_id: String, _display_name: String, _category: String, _pile: int, _intensity: float) -> void:
	pass


func _on_combat_state_changed(state: int) -> void:
	if _ally_nodes.is_empty():
		_setup_allies_from_manager()
	if state == 2:
		_sync_enemies_from_manager()
	elif state == 1:
		_despawn_all_enemies()


func _update_camera_follow() -> void:
	if _camera == null:
		return
	var combat_manager := get_node_or_null("/root/CombatManager")
	if combat_manager == null:
		return

	var engaging := bool(combat_manager.get("IsEngaging"))
	var bounds: Variant = combat_manager.call("GetAllyFormationBounds")
	var min_x := 40.0
	var max_x := 104.0
	if typeof(bounds) == TYPE_DICTIONARY:
		var b: Dictionary = bounds
		min_x = float(b.get("min_x", min_x))
		max_x = float(b.get("max_x", max_x))

	var center_x := (min_x + max_x) * 0.5
	var span := maxf(max_x - min_x, 32.0)
	var target_zoom := ENGAGE_ZOOM if engaging else MARCH_ZOOM
	var target_offset := ENGAGE_CAMERA_OFFSET if engaging else MARCH_CAMERA_OFFSET
	if span > 72.0 and not engaging:
		var wide := clampf(1.0 - (span - 72.0) / 120.0, 0.82, 1.0)
		target_zoom = Vector2(wide, wide)

	var target_x := clampf(center_x - target_offset, 0.0, 120.0)
	_camera.position.x = lerpf(_camera.position.x, target_x, 0.08)
	_camera_zoom = _camera_zoom.lerp(target_zoom, 0.06)
	_camera.zoom = _camera_zoom


func _spawn_overhead(entity_id: String, is_ally: bool) -> void:
	if _overhead_nodes.has(entity_id):
		return
	var hud: Control = OverheadHudScene.instantiate()
	_overhead_layer.add_child(hud)
	_overhead_nodes[entity_id] = hud
	if hud.has_method("setup"):
		hud.setup(entity_id, is_ally)


func _refresh_overhead_positions() -> void:
	for entity_id in _ally_nodes.keys():
		var doll: Node2D = _ally_nodes[entity_id]
		if doll == null:
			continue
		var anchor: Vector2 = doll.global_position
		if doll.has_method("get_anchor_position"):
			anchor = doll.get_anchor_position()
		_unit_positions[entity_id] = anchor
		if _overhead_nodes.has(entity_id):
			var hud: Control = _overhead_nodes[entity_id]
			if hud and hud.has_method("set_anchor_global"):
				hud.set_anchor_global(anchor)
	for entity_id in _enemy_nodes.keys():
		var enemy_doll: Node2D = _enemy_nodes[entity_id]
		if enemy_doll == null or not is_instance_valid(enemy_doll):
			continue
		var enemy_anchor: Vector2 = enemy_doll.global_position
		if enemy_doll.has_method("get_anchor_position"):
			enemy_anchor = enemy_doll.get_anchor_position()
		_unit_positions[entity_id] = enemy_anchor
		if _overhead_nodes.has(entity_id):
			var ehud: Control = _overhead_nodes[entity_id]
			if ehud and ehud.has_method("set_anchor_global"):
				ehud.set_anchor_global(enemy_anchor)
