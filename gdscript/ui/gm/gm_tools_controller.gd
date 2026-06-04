extends CanvasLayer

## Global F8 toggle for GM combat panel (CanvasLayer 10, above HUD).

@onready var _panel: Control = %GmCombatPanel
@onready var _dimmer: ColorRect = %Dimmer

var _enabled := true


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_enabled = _read_gm_enabled()
	if _panel:
		_panel.visible = false
	if _dimmer:
		_dimmer.visible = false
	set_process_unhandled_input(_enabled)


func _read_gm_enabled() -> bool:
	var logger := get_node_or_null("/root/GameLogger")
	if logger and logger.has_method("GetGmToolsEnabled"):
		return bool(logger.call("GetGmToolsEnabled"))
	return true


func toggle_panel() -> void:
	if _panel == null:
		return
	var show := not _panel.visible
	_panel.visible = show
	if _dimmer:
		_dimmer.visible = show
	if show:
		_panel.move_to_front()


func _unhandled_input(event: InputEvent) -> void:
	if not _enabled or _panel == null:
		return
	if event.is_action_pressed("toggle_gm"):
		toggle_panel()
		get_viewport().set_input_as_handled()
