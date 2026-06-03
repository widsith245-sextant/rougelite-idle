extends Node

## Whole-window drag on the main viewport; skips interactive controls.

var _dragging := false
var _drag_offset := Vector2i.ZERO
var _main_win_id: int = 0


func _ready() -> void:
	_main_win_id = DisplayServer.MAIN_WINDOW_ID


func _input(event: InputEvent) -> void:
	if _main_win_id < 0:
		return
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index != MOUSE_BUTTON_LEFT:
			return
		if mb.pressed:
			if _should_start_drag():
				_dragging = true
				var win_pos := DisplayServer.window_get_position(_main_win_id)
				_drag_offset = DisplayServer.mouse_get_position() - win_pos
		elif _dragging:
			_dragging = false
			_maybe_save_position()
	elif event is InputEventMouseMotion and _dragging:
		var new_pos := DisplayServer.mouse_get_position() - _drag_offset
		DisplayServer.window_set_position(new_pos, _main_win_id)


func _should_start_drag() -> bool:
	var vp := get_viewport()
	if vp == null:
		return false
	var hovered := vp.gui_get_hovered_control()
	if hovered == null:
		return true
	return not _hover_blocks_drag(hovered)


func _hover_blocks_drag(control: Control) -> bool:
	var node: Node = control
	while node:
		if node is BaseButton:
			return true
		if node is LineEdit or node is TextEdit or node is Slider or node is SpinBox or node is OptionButton:
			return true
		node = node.get_parent()
	return false


func _maybe_save_position() -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings == null or not settings.has_method("SaveWindowPosition"):
		return
	var pos := DisplayServer.window_get_position(_main_win_id)
	settings.call("SaveWindowPosition", pos.x, pos.y)
