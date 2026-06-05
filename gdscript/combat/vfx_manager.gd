extends Node2D

## Brief attack fly line from source to target.

const FlyLineScene := preload("res://scenes/combat/damage_fly_line.tscn")

const CATEGORY_COLORS := {
	"physical": Color(1.0, 0.95, 0.82, 0.85),
	"magical": Color(0.62, 0.55, 1.0, 0.9),
	"mark": Color(1.0, 0.66, 0.28, 0.9),
	"shield": Color(0.35, 0.9, 1.0, 0.9),
	"retaliate": Color(1.0, 0.35, 0.35, 0.9),
	"heal": Color(0.35, 0.95, 0.45, 0.9),
}

const DamageNumberScene := preload("res://scenes/combat/damage_number.tscn")
const STAGGER_DELAY := 0.08

var _pending_spawns: Array = []
var _stagger_timer := 0.0
var _sequence_counters: Dictionary = {}
var _damage_overlay: Node = null
var _fly_overlay: Node = null
var _camera: Camera2D = null


func configure_damage_overlay(layer: Node) -> void:
	_damage_overlay = layer


func configure_fly_overlay(layer: Node) -> void:
	_fly_overlay = layer if layer != null else self


func configure_camera(camera: Camera2D) -> void:
	_camera = camera


func reset_sequence_counters() -> void:
	_sequence_counters.clear()


func purge_pending_for_target(target_id: String) -> void:
	if target_id.is_empty():
		return
	var kept: Array = []
	for entry in _pending_spawns:
		if entry is Dictionary:
			var opts: Dictionary = entry.get("opts", {})
			if str(opts.get("target_id", "")) == target_id:
				continue
		kept.append(entry)
	_pending_spawns = kept


func _process(delta: float) -> void:
	if _pending_spawns.is_empty():
		return

	_stagger_timer -= delta
	if _stagger_timer > 0.0:
		return

	var next: Dictionary = _pending_spawns.pop_front()
	var amount: float = next.get("amount", 0.0)
	var anchor = next.get("anchor", null)
	var opts: Dictionary = next.get("opts", {})
	var spawn_pos: Variant = next.get("spawn_pos", null)

	if anchor != null and is_instance_valid(anchor):
		spawn_damage_number_on_target(amount, anchor, opts)
	elif spawn_pos is Vector2:
		spawn_damage_number(amount, spawn_pos, opts)
	elif opts.has("canvas_pos") and opts["canvas_pos"] is Vector2:
		spawn_damage_number(amount, opts["canvas_pos"], opts)
	elif next.has("pos"):
		spawn_damage_number(amount, next.get("pos", global_position), opts)
	_stagger_timer = STAGGER_DELAY


func spawn_damage_fly_line(source: Node2D, target: Node2D, category: String = "physical") -> void:
	if source == null or target == null or not is_instance_valid(source) or not is_instance_valid(target):
		return
	var parent: Node = _fly_overlay if _fly_overlay != null else self
	var line: Node2D = FlyLineScene.instantiate()
	parent.add_child(line)
	var from_pos := CombatCoords.doll_to_canvas_pos(source, _camera)
	var to_pos := CombatCoords.doll_to_canvas_pos(target, _camera)
	var color: Color = CATEGORY_COLORS.get(category, CATEGORY_COLORS.physical)
	if line.has_method("setup"):
		line.setup(from_pos, to_pos, color)


func spawn_damage_number_staggered(amount: float, anchor_node: Node2D, opts: Dictionary = {}) -> void:
	if anchor_node == null or not is_instance_valid(anchor_node):
		return
	var target_id := str(opts.get("target_id", ""))
	var seq := int(_sequence_counters.get(target_id, 0))
	opts["sequence_index"] = seq
	_sequence_counters[target_id] = seq + 1
	var spawn_pos := CombatCoords.doll_anchor_pos(anchor_node)
	var canvas_pos := CombatCoords.doll_to_canvas_pos(anchor_node, _camera)
	opts["canvas_pos"] = canvas_pos
	_pending_spawns.append({
		"amount": amount,
		"anchor": anchor_node,
		"spawn_pos": spawn_pos,
		"opts": opts,
	})
	if _pending_spawns.size() == 1 and _stagger_timer <= 0.0:
		_stagger_timer = 0.0


func spawn_damage_number_on_target(amount: float, anchor_node: Node2D, opts: Dictionary = {}) -> void:
	var spread := int(opts.get("sequence_index", 0)) * 12
	var offset := Vector2(spread + randf_range(-4, 4), 0.0)
	var canvas_pos: Vector2 = opts.get("canvas_pos", CombatCoords.doll_to_canvas_pos(anchor_node, _camera))

	var node: Control = DamageNumberScene.instantiate()
	opts["follow_offset"] = offset
	opts["camera"] = _camera
	if anchor_node != null and is_instance_valid(anchor_node):
		opts["follow_anchor"] = anchor_node
	else:
		opts["canvas_pos"] = canvas_pos

	var parent: Node = _damage_overlay if _damage_overlay != null else self
	parent.add_child(node)
	node.global_position = canvas_pos + CombatCoords.HUD_DAMAGE_OFFSET + offset
	if node.has_method("setup"):
		node.setup(amount, opts)


func spawn_damage_number(amount: float, world_pos: Vector2, opts: Dictionary = {}) -> void:
	var spread := int(opts.get("sequence_index", 0)) * 12
	var offset := Vector2(spread + randf_range(-4, 4), 0.0)
	var canvas_pos := CombatCoords.canvas_pos_from_world(world_pos, _camera)
	opts["canvas_pos"] = canvas_pos
	opts["follow_offset"] = offset
	opts["camera"] = _camera

	var node: Control = DamageNumberScene.instantiate()
	var parent: Node = _damage_overlay if _damage_overlay != null else self
	parent.add_child(node)
	node.global_position = canvas_pos + CombatCoords.HUD_DAMAGE_OFFSET + offset
	if node.has_method("setup"):
		node.setup(amount, opts)
