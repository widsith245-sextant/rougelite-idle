extends CanvasLayer

## Skill toast + character portrait when CombatActionStarted fires.

const DISPLAY_DURATION := 0.75
const TEXTS_PATH := "res://data/tables/ui/skill_cast_texts.json"
const UNIT_DISPLAY := {
	"ally_a": "先锋剑士",
	"ally_b": "狙击手",
	"ally_c": "大法师",
}

@onready var _root: Control = %OverlayRoot
@onready var _toast_panel: PanelContainer = %ToastPanel
@onready var _skill_label: Label = %SkillLabel
@onready var _flavor_label: Label = %FlavorLabel
@onready var _portrait_host: Control = %PortraitHost
@onready var _unit_label: Label = %UnitLabel

var _skill_texts: Dictionary = {}
var _portrait_doll: Node2D
var _hide_tween: Tween
var _gate_acquired := false


func _ready() -> void:
	layer = 4
	_load_texts()
	_root.visible = false
	_root.modulate.a = 0.0
	_hide_portrait_frame()

	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus == null:
		return
	if event_bus.has_signal("CombatActionStarted"):
		event_bus.connect("CombatActionStarted", _on_combat_action_started)
	if event_bus.has_signal("MarchStateChanged"):
		event_bus.connect("MarchStateChanged", _on_march_state_changed)


func _on_march_state_changed(is_marching: bool) -> void:
	if is_marching:
		_hide_portrait_frame()


func _hide_portrait_frame() -> void:
	var portrait_frame := _root.get_node_or_null("PortraitFrame")
	if portrait_frame:
		portrait_frame.visible = false
	_clear_portrait()


func _load_texts() -> void:
	if not FileAccess.file_exists(TEXTS_PATH):
		return
	var file := FileAccess.open(TEXTS_PATH, FileAccess.READ)
	if file == null:
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) == TYPE_DICTIONARY:
		_skill_texts = parsed


func _on_combat_action_started(actor_id: String) -> void:
	var gate := get_node_or_null("/root/UiFxGate")
	if gate and gate.has_method("is_busy") and gate.call("is_busy"):
		return
	if gate and gate.has_method("try_acquire"):
		if not gate.call("try_acquire"):
			return
		_gate_acquired = true
	var is_ally := actor_id.begins_with("ally_")
	var combat := get_node_or_null("/root/CombatManager")
	if combat == null:
		_release_gate()
		return

	var info: Variant = combat.call("GetUnitBattleDisplay", actor_id)
	if typeof(info) != TYPE_DICTIONARY:
		_release_gate()
		return

	var skill_id: String = str(info.get("skill_id", ""))
	if skill_id.is_empty():
		_release_gate()
		return

	var skill_entry: Dictionary = _skill_texts.get(skill_id, {})
	var skill_name: String = str(skill_entry.get("name", skill_id))
	var flavor: String = str(skill_entry.get("flavor", ""))
	var unit_name: String = str(info.get("display_name", UNIT_DISPLAY.get(actor_id, actor_id)))

	_show_cast(actor_id, unit_name, skill_name, flavor, is_ally)


func _show_cast(unit_id: String, unit_name: String, skill_name: String, flavor: String, show_portrait: bool) -> void:
	if _hide_tween:
		_release_gate()
		_hide_tween.kill()

	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.get("IsMarching"):
		show_portrait = false

	_skill_label.text = skill_name
	_flavor_label.text = flavor
	_flavor_label.visible = not flavor.is_empty()
	_unit_label.text = unit_name
	_unit_label.visible = show_portrait

	var portrait_frame := _root.get_node_or_null("PortraitFrame")
	if portrait_frame:
		portrait_frame.visible = show_portrait
	if show_portrait:
		_refresh_portrait(unit_id)
	else:
		_clear_portrait()

	_root.visible = true
	_root.modulate.a = 1.0
	_toast_panel.scale = Vector2(0.85, 0.85)
	var pop := create_tween()
	pop.set_trans(Tween.TRANS_BACK)
	pop.set_ease(Tween.EASE_OUT)
	pop.tween_property(_toast_panel, "scale", Vector2.ONE, 0.12)

	_hide_tween = create_tween()
	_hide_tween.tween_interval(DISPLAY_DURATION)
	_hide_tween.tween_property(_root, "modulate:a", 0.0, 0.2)
	_hide_tween.tween_callback(func() -> void:
		_root.visible = false
		_release_gate()
	)


func _clear_portrait() -> void:
	if _portrait_host == null:
		return
	for child in _portrait_host.get_children():
		child.queue_free()
	_portrait_doll = null


func _refresh_portrait(unit_id: String) -> void:
	if _portrait_host == null:
		return
	_clear_portrait()
	_portrait_doll = CharacterBase.spawn(_portrait_host, unit_id, 0.0)
	if _portrait_doll:
		_portrait_doll.position = Vector2(28, 44)
		_portrait_doll.scale = Vector2(1.1, 1.1)
		if _portrait_doll.has_method("play_attack"):
			_portrait_doll.play_attack()


func _release_gate() -> void:
	if not _gate_acquired:
		return
	_gate_acquired = false
	var gate := get_node_or_null("/root/UiFxGate")
	if gate and gate.has_method("release"):
		gate.call("release")
