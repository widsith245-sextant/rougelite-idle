extends Node

## Enforces borderless 400x150 desktop chrome on game start.

const MAIN_SIZE := Vector2i(400, 150)
const SCREEN_MARGIN := 16


func _ready() -> void:
	var win := get_window()
	if win == null:
		return
	win.borderless = true
	win.size = MAIN_SIZE
	win.min_size = MAIN_SIZE
	win.max_size = MAIN_SIZE
	if OS.get_name() == "Windows":
		DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_EXTEND_TO_TITLE, false)
	_place_main_window()


func reset_position() -> void:
	_place_default_position()


func _place_main_window() -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("TryRestoreWindowPosition"):
		if settings.call("TryRestoreWindowPosition"):
			return
	_place_default_position()


func _place_default_position() -> void:
	var win := get_window()
	if win == null:
		return
	var screen := DisplayServer.window_get_current_screen()
	var usable := DisplayServer.screen_get_usable_rect(screen)
	var x := usable.position.x + usable.size.x - MAIN_SIZE.x - SCREEN_MARGIN
	var y := usable.position.y + usable.size.y - MAIN_SIZE.y - SCREEN_MARGIN
	x = maxi(x, usable.position.x + SCREEN_MARGIN)
	y = maxi(y, usable.position.y + SCREEN_MARGIN)
	win.position = Vector2i(x, y)
