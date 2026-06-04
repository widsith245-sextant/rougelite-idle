extends Node2D

## Spawns combat VFX and damage numbers with stagger for BD chains.
## Damage numbers parent to unit dolls so they follow movement and camera pan.

const DamageNumberScene := preload("res://scenes/combat/damage_number.tscn")
const STAGGER_DELAY := 0.08
const ANCHOR_Y_OFFSET := -20.0

var _pending_spawns: Array = []
var _stagger_timer := 0.0
var _sequence_counters: Dictionary = {}


func reset_sequence_counters() -> void:
	_sequence_counters.clear()


func _process(delta: float) -> void:
	if _pending_spawns.is_empty():
		return

	_stagger_timer -= delta
	if _stagger_timer > 0.0:
		return

	var next: Dictionary = _pending_spawns.pop_front()
	var amount: float = next.get("amount", 0.0)
	var anchor: Node2D = next.get("anchor", null)
	var opts: Dictionary = next.get("opts", {})
	if anchor != null and is_instance_valid(anchor):
		spawn_damage_number_on_target(amount, anchor, opts)
	elif next.has("pos"):
		spawn_damage_number(amount, next.get("pos", global_position), opts)
	_stagger_timer = STAGGER_DELAY


func spawn_damage_number_staggered(amount: float, anchor_node: Node2D, opts: Dictionary = {}) -> void:
	if anchor_node == null or not is_instance_valid(anchor_node):
		return
	var target_id := str(opts.get("target_id", ""))
	var seq := int(_sequence_counters.get(target_id, 0))
	opts["sequence_index"] = seq
	_sequence_counters[target_id] = seq + 1
	_pending_spawns.append({ "amount": amount, "anchor": anchor_node, "opts": opts })
	if _pending_spawns.size() == 1 and _stagger_timer <= 0.0:
		_stagger_timer = 0.0


func spawn_damage_number_on_target(amount: float, anchor_node: Node2D, opts: Dictionary = {}) -> void:
	if anchor_node == null or not is_instance_valid(anchor_node):
		return
	var node := DamageNumberScene.instantiate()
	var spread := int(opts.get("sequence_index", 0)) * 12
	node.position = Vector2(
		spread + randf_range(-4, 4),
		ANCHOR_Y_OFFSET + randf_range(-6, 0),
	)
	anchor_node.add_child(node)
	if node.has_method("setup"):
		node.setup(amount, opts)


func spawn_damage_number(amount: float, world_pos: Vector2, opts: Dictionary = {}) -> void:
	var node := DamageNumberScene.instantiate()
	var spread := int(opts.get("sequence_index", 0)) * 12
	node.global_position = world_pos + Vector2(spread + randf_range(-4, 4), randf_range(-12, -6))
	add_child(node)
	if node.has_method("setup"):
		node.setup(amount, opts)
