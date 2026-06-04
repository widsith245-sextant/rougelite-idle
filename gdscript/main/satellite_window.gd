class_name SatelliteWindow
extends RefCounted

## Shared setup for OS sub-windows (broadcast, portrait, popups).

const BACKGROUND_COLOR := Color(0.0784314, 0.0941176, 0.121569, 1.0)
const HEADER_BG_COLOR := Color(0.1, 0.12, 0.15, 1.0)

static var _transient_links: Dictionary = {}


static func configure(win: Window, borderless: bool = true, always_on_top: bool = false) -> void:
	if win == null:
		return
	win.borderless = borderless
	win.always_on_top = always_on_top
	win.transient = true
	_apply_transient_parent.call_deferred(win)


static func _apply_transient_parent(win: Window) -> void:
	if not is_instance_valid(win):
		return
	var main_win := get_main_window(win)
	if main_win == null:
		return
	var win_id := win.get_window_id()
	var main_id := main_win.get_window_id()
	if win_id == main_id:
		return
	if int(_transient_links.get(win_id, -1)) == main_id:
		return
	DisplayServer.window_set_transient(win_id, main_id)
	_transient_links[win_id] = main_id


static func apply_dark_background(parent: Control, color: Color = BACKGROUND_COLOR) -> ColorRect:
	var bg := ColorRect.new()
	bg.name = "Background"
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.color = color
	parent.add_child(bg)
	parent.move_child(bg, 0)
	return bg


static func make_header_stylebox() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = HEADER_BG_COLOR
	style.content_margin_left = 8.0
	style.content_margin_top = 4.0
	style.content_margin_right = 8.0
	style.content_margin_bottom = 4.0
	return style


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


static func place_popup_beside_main(win: Window, from: Node, gap: int = 8) -> void:
	if win == null:
		return
	var main_rect := get_main_window_rect(from)
	if main_rect.size == Vector2i.ZERO:
		win.popup_centered()
		return
	var x := main_rect.position.x - win.size.x - gap
	var y := main_rect.position.y
	if x < 0:
		x = main_rect.position.x + main_rect.size.x + gap
	win.position = Vector2i(x, y)
