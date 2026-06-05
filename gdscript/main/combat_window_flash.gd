extends Control

## Full-viewport red flash when allies attack.

@onready var _flash: ColorRect = $Flash


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if _flash:
		_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_flash.color = Color(1.0, 0.12, 0.12, 0.0)
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("CombatActionStarted"):
		event_bus.connect("CombatActionStarted", _on_combat_action_started)


func _on_combat_action_started(actor_id: String) -> void:
	if not actor_id.begins_with("ally"):
		return
	if _flash == null:
		return
	var tween := create_tween()
	var flash_on := Color(1.0, 0.12, 0.12, 0.35)
	var flash_off := Color(1.0, 0.12, 0.12, 0.0)
	_flash.color = flash_on
	tween.tween_property(_flash, "color", flash_off, 0.12)
