extends Control

## Three-choice run relic picker (reward room).

const RELIC_BORDER := {
	"relic_sharp_edge": Color(1.0, 0.55, 0.45),
	"relic_iron_heart": Color(0.45, 0.85, 0.65),
	"relic_blood_vial": Color(0.55, 0.85, 1.0),
	"relic_swift_boots": Color(0.95, 0.85, 0.45),
	"relic_eagle_eye": Color(0.85, 0.65, 1.0),
	"relic_gold_charm": Color(1.0, 0.85, 0.35),
}

@onready var _relics_row: HBoxContainer = %RelicsRow
@onready var _hint: Label = %HintLabel


func _ready() -> void:
	if _hint:
		_hint.text = "选择一件遗物以继续探索"
	call_deferred("refresh")


func refresh() -> void:
	_clear_relics()
	var relic_mgr := get_node_or_null("/root/RunRelicManager")
	if relic_mgr == null or not relic_mgr.has_method("GetPendingOffersSnapshot"):
		return
	var offers: Variant = relic_mgr.call("GetPendingOffersSnapshot")
	if typeof(offers) != TYPE_ARRAY:
		return
	for entry in offers:
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		_add_relic_button(entry)


func _clear_relics() -> void:
	if _relics_row == null:
		return
	for child in _relics_row.get_children():
		child.queue_free()


func _add_relic_button(data: Dictionary) -> void:
	var relic_id: String = str(data.get("id", ""))
	var relic_name: String = str(data.get("name", "遗物"))
	var desc: String = str(data.get("desc", ""))

	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.12, 0.18, 0.95)
	style.border_width_left = 2
	style.border_width_top = 2
	style.border_width_right = 2
	style.border_width_bottom = 2
	style.border_color = RELIC_BORDER.get(relic_id, Color(0.75, 0.6, 0.35))
	style.corner_radius_top_left = 4
	style.corner_radius_top_right = 4
	style.corner_radius_bottom_right = 4
	style.corner_radius_bottom_left = 4
	panel.add_theme_stylebox_override("panel", style)
	panel.custom_minimum_size = Vector2(180, 220)
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 8)
	panel.add_child(vbox)

	var name_label := Label.new()
	name_label.text = relic_name
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.add_theme_font_size_override("font_size", 16)
	vbox.add_child(name_label)

	var desc_label := Label.new()
	desc_label.text = desc
	desc_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc_label.custom_minimum_size = Vector2(160, 80)
	desc_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc_label.add_theme_font_size_override("font_size", 11)
	vbox.add_child(desc_label)

	var pick_btn := Button.new()
	pick_btn.text = "选取"
	pick_btn.pressed.connect(_on_relic_picked.bind(relic_id))
	vbox.add_child(pick_btn)

	_relics_row.add_child(panel)


func _on_relic_picked(relic_id: String) -> void:
	var relic_mgr := get_node_or_null("/root/RunRelicManager")
	if relic_mgr and relic_mgr.has_method("ApplyRelic"):
		relic_mgr.call("ApplyRelic", relic_id)
	var popup := get_tree().root.get_node_or_null("GameRoot/PopupManager")
	if popup and popup.has_method("close_run_relic_pick"):
		popup.call("close_run_relic_pick")
