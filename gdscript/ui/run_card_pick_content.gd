extends Control

## Three-choice run card picker (mandatory during pause).

const CARD_BORDER := {
	"card_atk_10": Color(1.0, 0.55, 0.45),
	"card_hp_15": Color(0.45, 0.85, 0.65),
	"card_heal_20": Color(0.55, 0.85, 1.0),
	"card_spd_12": Color(0.95, 0.85, 0.45),
	"card_crit_8": Color(0.85, 0.65, 1.0),
	"card_gold_run": Color(1.0, 0.85, 0.35),
}

@onready var _cards_row: HBoxContainer = %CardsRow
@onready var _hint: Label = %HintLabel


func _ready() -> void:
	if _hint:
		_hint.text = "选择一张卡牌以继续战斗"
	call_deferred("refresh")


func refresh() -> void:
	_clear_cards()
	var run_cards := get_node_or_null("/root/RunCardManager")
	if run_cards == null or not run_cards.has_method("GetPendingOffersSnapshot"):
		return
	var offers: Variant = run_cards.call("GetPendingOffersSnapshot")
	if typeof(offers) != TYPE_ARRAY:
		return
	for entry in offers:
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		_add_card_button(entry)


func _clear_cards() -> void:
	if _cards_row == null:
		return
	for child in _cards_row.get_children():
		child.queue_free()


func _add_card_button(data: Dictionary) -> void:
	var card_id: String = str(data.get("id", ""))
	var card_name: String = str(data.get("name", "卡牌"))
	var desc: String = str(data.get("desc", ""))

	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.12, 0.1, 0.16, 0.95)
	style.border_width_left = 2
	style.border_width_top = 2
	style.border_width_right = 2
	style.border_width_bottom = 2
	style.border_color = CARD_BORDER.get(card_id, Color(0.7, 0.55, 0.95))
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
	name_label.text = card_name
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
	pick_btn.text = "选择"
	pick_btn.pressed.connect(_on_card_picked.bind(card_id))
	vbox.add_child(pick_btn)

	_cards_row.add_child(panel)


func _on_card_picked(card_id: String) -> void:
	var run_cards := get_node_or_null("/root/RunCardManager")
	if run_cards and run_cards.has_method("ApplyCard"):
		run_cards.call("ApplyCard", card_id)
	var popup := get_tree().root.get_node_or_null("GameRoot/PopupManager")
	if popup and popup.has_method("close_run_card_pick"):
		popup.call("close_run_card_pick")
