extends CanvasLayer

## Placeholder illustration overlay when clicking backpack doll / avatar.

@onready var _panel: PanelContainer = %Panel
@onready var _name_label: Label = %NameLabel
@onready var _class_label: Label = %ClassLabel
@onready var _portrait_rect: TextureRect = %PortraitRect
@onready var _hint_label: Label = %HintLabel

var _hide_tween: Tween


func _ready() -> void:
	layer = 5
	visible = false
	process_mode = Node.PROCESS_MODE_DISABLED
	if _panel:
		_panel.gui_input.connect(_on_panel_input)


func show_portrait(unit_id: String) -> void:
	if _hide_tween and _hide_tween.is_valid():
		_hide_tween.kill()

	var party := get_node_or_null("/root/PartyManager")
	var display_name := unit_id
	var class_id := ""
	if party:
		display_name = str(party.call("GetDisplayNameForUnit", unit_id))
		class_id = str(party.call("GetClassIdForUnit", unit_id))

	if _name_label:
		_name_label.text = display_name
	if _class_label:
		_class_label.text = class_id if not class_id.is_empty() else "—"
	if _hint_label:
		_hint_label.text = "点击关闭"

	var visual := PlaceholderSpriteFactory.build_visual_for_unit(unit_id)
	if _portrait_rect and visual and visual.body_texture:
		_portrait_rect.texture = visual.body_texture

	process_mode = Node.PROCESS_MODE_INHERIT
	visible = true
	if _panel:
		_panel.modulate.a = 0.0
		_panel.scale = Vector2(0.85, 0.85)
	var tw := create_tween()
	tw.tween_property(_panel, "modulate:a", 1.0, 0.12)
	tw.parallel().tween_property(_panel, "scale", Vector2.ONE, 0.15).set_trans(Tween.TRANS_BACK)


func _finish() -> void:
	if _hide_tween and _hide_tween.is_valid():
		_hide_tween.kill()
	visible = false
	process_mode = Node.PROCESS_MODE_DISABLED


func _on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		_finish()
		get_viewport().set_input_as_handled()
