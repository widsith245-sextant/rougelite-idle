extends Node

## Binds unit id to StatsService snapshot for UI display.

signal stats_changed(snapshot: Dictionary)

@export var unit_id: String = ""

var _poll_timer: float = 0.0


func _ready() -> void:
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus:
		if event_bus.has_signal("StatsChanged"):
			event_bus.connect("StatsChanged", _on_stats_changed)
		if event_bus.has_signal("EquipmentChanged"):
			event_bus.connect("EquipmentChanged", _on_stats_changed)
	refresh()


func _process(delta: float) -> void:
	if unit_id.is_empty():
		return
	_poll_timer -= delta
	if _poll_timer <= 0.0:
		_poll_timer = 0.5
		refresh()


func bind_unit(id: String) -> void:
	unit_id = id
	refresh()


func refresh() -> void:
	if unit_id.is_empty():
		return
	var stats_service := get_node_or_null("/root/StatsService")
	if stats_service == null:
		return
	var snap: Variant = stats_service.call("GetSnapshot", unit_id)
	if snap is Dictionary:
		stats_changed.emit(snap)


func _on_stats_changed(changed_unit_id: String) -> void:
	if changed_unit_id == unit_id or unit_id == "ally_a":
		refresh()
