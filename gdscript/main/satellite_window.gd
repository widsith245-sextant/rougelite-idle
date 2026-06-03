class_name SatelliteWindow
extends RefCounted

## Shared setup for OS sub-windows (broadcast, portrait, popups).
## always_on_top and transient are mutually exclusive on Windows.


static func configure(win: Window) -> void:
	if win == null:
		return
	win.always_on_top = false
	win.transient = true
	var main_win := get_main_window(win)
	if main_win and main_win.get_window_id() != win.get_window_id():
		DisplayServer.window_set_transient(win.get_window_id(), main_win.get_window_id())


static func get_main_window(from: Node) -> Window:
	if from == null:
		return null
	var tree := from.get_tree()
	if tree == null:
		return null
	var root := tree.root
	if root is Window:
		return root as Window
	return null


static func get_main_window_rect(from: Node) -> Rect2i:
	var main_win := get_main_window(from)
	if main_win == null:
		return Rect2i()
	return Rect2i(main_win.position, main_win.size)
