class_name CharacterBase
extends Node2D

## Modular paper-doll character. Single AnimationPlayer drives all layers.

signal ecchi_damaged_entered
signal ecchi_damaged_exited

const ECCHI_HP_THRESHOLD := 0.30
const CharacterScene := preload("res://scenes/characters/character_base.tscn")

@onready var _shadow: Sprite2D = $Shadow
@onready var _back_accessory: Sprite2D = $Back_Accessory
@onready var _body_base: Sprite2D = $Body_Base
@onready var _outfit: Sprite2D = $Outfit
@onready var _head_accessory: Sprite2D = $Head_Accessory
@onready var _anim: AnimationPlayer = $AnimationPlayer
@onready var _ecchi_state: Node = $EcchiDamagedState
@onready var _hit_flash: Node = $HitFlashController

var _entity_id: String = ""
var _max_hp: float = 100.0
var _current_hp: float = 100.0
var _is_ecchi_damaged := false
var _visual: CharacterVisualResource
var _move_tween: Tween


func _ready() -> void:
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus:
		if event_bus.has_signal("UnitHpChanged"):
			event_bus.connect("UnitHpChanged", _on_unit_hp_changed)
		if event_bus.has_signal("DamageDealt"):
			event_bus.connect("DamageDealt", _on_damage_dealt)
		if event_bus.has_signal("CombatActionStarted"):
			if not event_bus.is_connected("CombatActionStarted", _on_combat_action_started):
				event_bus.connect("CombatActionStarted", _on_combat_action_started)
	_ensure_animations()
	if _anim.has_animation("idle"):
		_anim.play("idle")


static func spawn(parent: Node, unit_id: String, pos_x: float = 40.0) -> Node2D:
	var node: Node2D = CharacterScene.instantiate()
	parent.add_child(node)
	node.call_deferred("setup", unit_id, pos_x)
	return node


func setup(unit_id: String, pos_x: float = 40.0) -> void:
	_entity_id = unit_id
	position.x = pos_x
	position.y = -32.0
	apply_visual(_load_visual(unit_id))
	_sync_hp_from_manager()
	var stats_node := get_node_or_null("StatsComponent")
	if stats_node and stats_node.has_method("bind_unit"):
		stats_node.bind_unit(unit_id)


func bind_unit(entity_id: String, max_hp: float) -> void:
	_entity_id = entity_id
	_max_hp = max_hp
	_current_hp = max_hp


func apply_visual(visual: CharacterVisualResource) -> void:
	_visual = visual
	if _visual == null:
		return
	if _visual.shadow_texture:
		_shadow.texture = _visual.shadow_texture
	if _visual.body_texture:
		_body_base.texture = _visual.body_texture
	if _visual.outfit_normal:
		_outfit.texture = _visual.outfit_normal
	if _visual.head_accessory:
		_head_accessory.texture = _visual.head_accessory
	if _visual.back_accessory:
		_back_accessory.texture = _visual.back_accessory
	if _ecchi_state and _ecchi_state.has_method("configure"):
		_ecchi_state.configure(_visual.outfit_normal, _visual.outfit_damaged)


func move_to_x(target_x: float, duration: float = 0.2) -> void:
	if is_equal_approx(position.x, target_x):
		return
	if _move_tween and _move_tween.is_valid():
		_move_tween.kill()
	_move_tween = create_tween()
	_move_tween.set_trans(Tween.TRANS_CUBIC)
	_move_tween.set_ease(Tween.EASE_OUT)
	_move_tween.tween_property(self, "position:x", target_x, duration)


func move_to_slot(slot_index: int, duration: float = 0.2) -> void:
	move_to_x(slot_index * 32.0, duration)


func play_reposition_trail() -> void:
	modulate.a = 0.65
	var tween := create_tween()
	tween.tween_property(self, "modulate:a", 1.0, 0.2)


func apply_hit_stop(duration: float) -> void:
	if _anim:
		_anim.speed_scale = 0.0
	var tween := create_tween()
	tween.tween_interval(duration)
	tween.tween_callback(func() -> void:
		if _anim:
			_anim.speed_scale = 1.0
	)


func get_anchor_position() -> Vector2:
	return global_position + Vector2(0, -20)


func _load_visual(unit_id: String) -> CharacterVisualResource:
	var party := get_node_or_null("/root/PartyManager")
	if party and party.has_method("GetRosterIdForUnit"):
		var roster_id := str(party.call("GetRosterIdForUnit", unit_id))
		if not roster_id.is_empty():
			var roster_path := "res://resources/characters/%s_visual.tres" % roster_id
			if ResourceLoader.exists(roster_path):
				var roster_loaded: CharacterVisualResource = load(roster_path)
				if roster_loaded and roster_loaded.body_texture:
					return roster_loaded
			return PlaceholderSpriteFactory.build_visual_for_roster(roster_id)

	var path := "res://resources/characters/%s_visual.tres" % unit_id
	if ResourceLoader.exists(path):
		var loaded: CharacterVisualResource = load(path)
		if loaded and loaded.body_texture:
			return loaded
	return PlaceholderSpriteFactory.build_visual_for_unit(unit_id)


func _sync_hp_from_manager() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null or _entity_id.is_empty():
		return
	var hp_data: Variant = combat.call("GetUnitHp", _entity_id)
	if hp_data is Dictionary:
		set_hp(hp_data.get("current", _max_hp), hp_data.get("max", _max_hp))


func _on_unit_hp_changed(entity_id: String, current_hp: float, max_hp: float) -> void:
	if entity_id != _entity_id:
		return
	set_hp(current_hp, max_hp)


func _on_damage_dealt(_source_id: String, target_id: String, _amount: float) -> void:
	if target_id != _entity_id:
		return
	play_hit()


func _on_combat_action_started(actor_id: String) -> void:
	if actor_id != _entity_id:
		return
	play_attack()


func set_hp(current_hp: float, max_hp: float = -1.0) -> void:
	_current_hp = current_hp
	if max_hp > 0.0:
		_max_hp = max_hp
	_update_ecchi_state()


func _update_ecchi_state() -> void:
	var ratio := _current_hp / _max_hp if _max_hp > 0.0 else 0.0
	var should_damage := ratio < ECCHI_HP_THRESHOLD and ratio > 0.0
	if should_damage == _is_ecchi_damaged:
		return
	_is_ecchi_damaged = should_damage
	if _ecchi_state and _ecchi_state.has_method("set_active"):
		_ecchi_state.set_active(_is_ecchi_damaged)
	if _is_ecchi_damaged:
		ecchi_damaged_entered.emit()
	else:
		ecchi_damaged_exited.emit()


func play_idle() -> void:
	if _anim.has_animation("idle"):
		_anim.play("idle")
		if _anim:
			_anim.speed_scale = 1.0


func play_walk() -> void:
	if _anim.has_animation("idle"):
		_anim.play("idle")
	if _anim:
		_anim.speed_scale = 1.6


func play_attack() -> void:
	var tween := create_tween()
	var start_x := position.x
	tween.tween_property(self, "position:x", start_x + 6.0, 0.1)
	tween.tween_property(self, "position:x", start_x, 0.12)
	tween.tween_callback(play_idle)


func play_hit() -> void:
	if _hit_flash and _hit_flash.has_method("flash"):
		_hit_flash.flash()
	if _anim.has_animation("hit"):
		_anim.play("hit")
		_anim.queue("idle")


func _ensure_animations() -> void:
	if _anim.has_animation("idle"):
		return
	var lib := AnimationLibrary.new()
	lib.add_animation("idle", _make_idle_anim())
	lib.add_animation("attack", _make_attack_anim())
	lib.add_animation("hit", _make_hit_anim())
	_anim.add_animation_library("", lib)


func _make_idle_anim() -> Animation:
	var anim := Animation.new()
	anim.length = 1.2
	anim.loop_mode = Animation.LOOP_LINEAR
	var track_idx := anim.add_track(Animation.TYPE_VALUE)
	anim.track_set_path(track_idx, NodePath("Body_Base:position:y"))
	anim.track_insert_key(track_idx, 0.0, 0.0)
	anim.track_insert_key(track_idx, 0.6, -1.5)
	anim.track_insert_key(track_idx, 1.2, 0.0)
	return anim


func _make_attack_anim() -> Animation:
	var anim := Animation.new()
	anim.length = 0.25
	var track_idx := anim.add_track(Animation.TYPE_VALUE)
	anim.track_set_path(track_idx, NodePath(":position:x"))
	var start_x := position.x
	anim.track_insert_key(track_idx, 0.0, start_x)
	anim.track_insert_key(track_idx, 0.12, start_x + 6.0)
	anim.track_insert_key(track_idx, 0.25, start_x)
	return anim


func _make_hit_anim() -> Animation:
	var anim := Animation.new()
	anim.length = 0.15
	var track_idx := anim.add_track(Animation.TYPE_VALUE)
	anim.track_set_path(track_idx, NodePath("Body_Base:modulate:a"))
	anim.track_insert_key(track_idx, 0.0, 1.0)
	anim.track_insert_key(track_idx, 0.07, 0.65)
	anim.track_insert_key(track_idx, 0.15, 1.0)
	return anim
