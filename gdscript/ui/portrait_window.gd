extends Window

## Standalone portrait window (normal / damaged variants).

@onready var _panel: PanelContainer = %Panel
@onready var _name_label: Label = %NameLabel
@onready var _class_label: Label = %ClassLabel
@onready var _portrait_rect: TextureRect = %PortraitRect
@onready var _hint_label: Label = %HintLabel
@onready var _toggle_damaged: Button = %ToggleDamaged

var _hide_tween: Tween
var _unit_id: String = ""
var _damaged: bool = false


func _ready() -> void:
	title = "立绘"
	size = Vector2i(220, 280)
	min_size = Vector2i(220, 280)
	borderless = true
	unresizable = true
	SatelliteWindow.configure(self, true)
	visible = false
	close_requested.connect(_finish)
	if _panel:
		_panel.gui_input.connect(_on_panel_input)
	if _toggle_damaged:
		_toggle_damaged.toggled.connect(_on_toggle_damaged)


func show_portrait(unit_id: String, damaged: bool = false) -> void:
	if _hide_tween and _hide_tween.is_valid():
		_hide_tween.kill()

	_unit_id = unit_id
	_damaged = damaged

	var party := get_node_or_null("/root/PartyManager")
	var display_name := unit_id
	var class_id := ""
	var roster_id := ""
	if party:
		display_name = str(party.call("GetDisplayNameForUnit", unit_id))
		class_id = str(party.call("GetClassIdForUnit", unit_id))
		if party.has_method("GetRosterIdForUnit"):
			roster_id = str(party.call("GetRosterIdForUnit", unit_id))

	if _name_label:
		_name_label.text = display_name
	if _class_label:
		_class_label.text = class_id if not class_id.is_empty() else "—"
	if _hint_label:
		_hint_label.text = "点击关闭 · 切换大破"
	if _toggle_damaged:
		_toggle_damaged.button_pressed = _damaged
		_toggle_damaged.text = "大破" if _damaged else "正常"

	_apply_portrait_texture(roster_id, unit_id)

	popup_centered()
	visible = true
	show()
	SatelliteWindow.ensure_transient_parent(self)
	if _panel:
		_panel.modulate.a = 0.0
		_panel.scale = Vector2(0.85, 0.85)
	var tw := create_tween()
	tw.tween_property(_panel, "modulate:a", 1.0, 0.12)
	tw.parallel().tween_property(_panel, "scale", Vector2.ONE, 0.15).set_trans(Tween.TRANS_BACK)


func _apply_portrait_texture(roster_id: String, unit_id: String) -> void:
	if _portrait_rect == null:
		return
	var tex: Texture2D
	if not roster_id.is_empty():
		tex = PlaceholderSpriteFactory.build_portrait_for_roster(roster_id, _damaged)
	else:
		tex = PlaceholderSpriteFactory.build_portrait_for_unit(unit_id, _damaged)
	_portrait_rect.texture = tex
	_portrait_rect.modulate = Color(1.0, 0.88, 0.88) if _damaged else Color.WHITE


func _on_toggle_damaged(toggled_on: bool) -> void:
	_damaged = toggled_on
	if _toggle_damaged:
		_toggle_damaged.text = "大破" if _damaged else "正常"
	if not _unit_id.is_empty():
		var party := get_node_or_null("/root/PartyManager")
		var roster_id := ""
		if party and party.has_method("GetRosterIdForUnit"):
			roster_id = str(party.call("GetRosterIdForUnit", _unit_id))
		_apply_portrait_texture(roster_id, _unit_id)


func _finish() -> void:
	if _hide_tween and _hide_tween.is_valid():
		_hide_tween.kill()
	hide()
	visible = false


func _on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		_finish()
		get_viewport().set_input_as_handled()
