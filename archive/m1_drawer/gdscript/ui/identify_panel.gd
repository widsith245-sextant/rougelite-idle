extends "res://gdscript/ui/drawer_panel_base.gd"

@onready var _result_label: Label = %ResultLabel


func _ready() -> void:
	panel_title = "鉴定"
	super._ready()
	%IdentifyButton.pressed.connect(_on_identify_pressed)


func _on_identify_pressed() -> void:
	var loot_manager := get_node_or_null("/root/LootManager")
	if loot_manager == null:
		_result_label.text = "LootManager 未就绪"
		return

	var result: Variant = loot_manager.call("IdentifyNextAsDictionary", 1)
	if result == null:
		_result_label.text = "没有待鉴定的宝箱"
		return

	var data: Dictionary = result
	_result_label.text = "获得: %s (iLvl %d, 白值 %.1f)" % [
		data.get("display_name", "?"),
		data.get("item_level", 0),
		data.get("rolled_base_stat", 0.0),
	]
