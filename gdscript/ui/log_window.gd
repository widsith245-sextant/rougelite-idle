extends Window

## Satellite log panel docked below the main game window.

const WINDOW_SIZE := Vector2i(400, 160)

@onready var _log_text: RichTextLabel = %LogText
@onready var _close_button: Button = %CloseButton

var _last_main_pos := Vector2i(-99999, -99999)


func _ready() -> void:
	title = "日志"
	size = WINDOW_SIZE
	min_size = WINDOW_SIZE
	max_size = WINDOW_SIZE
	borderless = true
	unresizable = true
	visible = false
	SatelliteWindow.configure(self, true)
	close_requested.connect(hide_log)
	if _close_button:
		_close_button.pressed.connect(hide_log)
	var logger := get_node_or_null("/root/GameLogger")
	if logger and logger.has_signal("LogLineAdded"):
		logger.connect("LogLineAdded", _on_log_line_added)
	call_deferred("_load_recent")


func _process(_delta: float) -> void:
	var main := get_tree().root.get_node_or_null("GameRoot")
	if main == null or not visible:
		return
	var main_win := main.get_window()
	if main_win and main_win.position != _last_main_pos:
		_position_below_main()


func show_log() -> void:
	_position_below_main()
	_load_recent()
	visible = true
	show()
	SatelliteWindow.ensure_transient_parent(self)


func hide_log() -> void:
	hide()
	visible = false


func _on_log_line_added(line: String) -> void:
	_append_line(line)


func _load_recent() -> void:
	if _log_text == null:
		return
	_log_text.clear()
	var logger := get_node_or_null("/root/GameLogger")
	if logger == null:
		return
	var lines: Variant = logger.call("GetRecentLines", 80)
	if typeof(lines) != TYPE_ARRAY:
		return
	for entry in lines:
		_append_line(str(entry))


func _append_line(line: String) -> void:
	if _log_text == null or line.is_empty():
		return
	_log_text.append_text(line + "\n")
	var line_count := _log_text.get_line_count()
	if line_count > 0:
		_log_text.scroll_to_line(line_count - 1)


func _position_below_main() -> void:
	var main := get_tree().root.get_node_or_null("GameRoot")
	if main == null:
		return
	var main_win := main.get_window()
	if main_win == null:
		return
	var pos := main_win.position
	var main_size := main_win.size
	var screen := DisplayServer.window_get_current_screen()
	var usable := DisplayServer.screen_get_usable_rect(screen)
	var gap := 4
	var below_y := pos.y + main_size.y + gap
	if below_y + WINDOW_SIZE.y <= usable.position.y + usable.size.y:
		position = Vector2i(pos.x, below_y)
	else:
		position = Vector2i(pos.x, pos.y - WINDOW_SIZE.y - gap)
	_last_main_pos = pos
