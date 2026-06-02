extends Control

## Top 4px dual health bar: ally (green, left) vs enemy (red, right).

@onready var _ally_bar: ColorRect = %AllyBar
@onready var _enemy_bar: ColorRect = %EnemyBar

var _ally_ratio := 1.0
var _enemy_ratio := 1.0


func _ready() -> void:
	visible = true
	custom_minimum_size = Vector2(400, 4)
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("UnitHpChanged"):
		event_bus.connect("UnitHpChanged", _on_unit_hp_changed)
	_refresh_from_manager()


func _on_unit_hp_changed(_entity_id: String, _current_hp: float, _max_hp: float) -> void:
	_refresh_from_manager()


func _refresh_from_manager() -> void:
	var combat_manager := get_node_or_null("/root/CombatManager")
	if combat_manager == null:
		_refresh_bars()
		return

	set_ally_hp_ratio(combat_manager.call("GetAllyHpRatio"))
	set_enemy_hp_ratio(combat_manager.call("GetEnemyHpRatio"))


func set_ally_hp_ratio(ratio: float) -> void:
	_ally_ratio = clampf(ratio, 0.0, 1.0)
	_refresh_bars()


func set_enemy_hp_ratio(ratio: float) -> void:
	_enemy_ratio = clampf(ratio, 0.0, 1.0)
	_refresh_bars()


func _refresh_bars() -> void:
	var half_width := size.x * 0.5
	_ally_bar.size = Vector2(half_width * _ally_ratio, size.y)
	_enemy_bar.size = Vector2(half_width * _enemy_ratio, size.y)
	_enemy_bar.position.x = size.x - _enemy_bar.size.x
