extends Node

## Enforces borderless 400x150 desktop chrome on game start.


func _ready() -> void:
	var win := get_window()
	if win == null:
		return
	win.borderless = true
	win.size = Vector2i(400, 150)
	win.min_size = Vector2i(400, 150)
	win.max_size = Vector2i(400, 150)
	if OS.get_name() == "Windows":
		DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_EXTEND_TO_TITLE, false)
