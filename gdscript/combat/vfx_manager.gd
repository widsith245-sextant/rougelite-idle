extends Node2D

## Spawns combat VFX and damage numbers with stagger for BD chains.

const DamageNumberScene := preload("res://scenes/combat/damage_number.tscn")
const STAGGER_DELAY := 0.08

var _pending_spawns: Array = []
var _stagger_timer := 0.0
var _sequence_counters: Dictionary = {}


func _process(delta: float) -> void:
	if _pending_spawns.is_empty():
		return

	_stagger_timer -= delta
	if _stagger_timer > 0.0:
		return

	var next: Dictionary = _pending_spawns.pop_front()
	var amount: float = next.get("amount", 0.0)
	var pos: Vector2 = next.get("pos", global_position)
	var opts: Dictionary = next.get("opts", {})
	spawn_damage_number(amount, pos, opts)
	_stagger_timer = STAGGER_DELAY


func spawn_damage_number_staggered(amount: float, world_pos: Vector2, opts: Dictionary = {}) -> void:
	var target_id := str(opts.get("target_id", ""))
	var seq := int(_sequence_counters.get(target_id, 0))
	opts["sequence_index"] = seq
	_sequence_counters[target_id] = seq + 1
	_pending_spawns.append({ "amount": amount, "pos": world_pos, "opts": opts })
	if _pending_spawns.size() == 1 and _stagger_timer <= 0.0:
		_stagger_timer = 0.0


func spawn_damage_number(amount: float, world_pos: Vector2, opts: Dictionary = {}) -> void:
	var node := DamageNumberScene.instantiate()
	var spread := int(opts.get("sequence_index", 0)) * 12
	node.global_position = world_pos + Vector2(spread + randf_range(-4, 4), randf_range(-12, -6))
	add_child(node)
	if node.has_method("setup"):
		node.setup(amount, opts)
