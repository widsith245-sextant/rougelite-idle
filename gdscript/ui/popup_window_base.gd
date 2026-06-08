extends Window

## Native OS popup window 640x480 (not embedded in 400x150 main viewport).

const POPUP_SIZE := Vector2i(640, 480)

@export var popup_title: String = "Panel"

@onready var _title_label: Label = %TitleLabel
@onready var _close_button: Button = %CloseButton
@onready var _content_root: Control = %ContentRoot
@onready var _root: Control = $Root

var _close_allowed := true


func _ready() -> void:
	title = popup_title
	if _title_label:
		_title_label.text = popup_title

	# 独立系统窗口：由 project.godot 的 embed_subwindows=false 控制；
	# 弹窗挂到 get_tree().root（见 popup_manager.gd），勿用已移除的 embedded 属性。
	exclusive = false
	unresizable = false
	SatelliteWindow.configure(self, false, false)
	borderless = false

	size = POPUP_SIZE
	min_size = POPUP_SIZE
	_content_root.custom_minimum_size = Vector2(640, 448)

	close_requested.connect(_on_close_requested)
	if _close_button:
		_close_button.pressed.connect(_on_close_requested)
	visible = false

	_apply_root_layout()


func _apply_root_layout() -> void:
	if _root:
		_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if _content_root:
		_content_root.size_flags_vertical = Control.SIZE_EXPAND_FILL


func show_popup() -> void:
	_apply_root_layout()
	SatelliteWindow.place_popup_beside_main(self, self)
	visible = true
	show()
	SatelliteWindow.ensure_transient_parent(self)
	var wid := get_window_id()
	if wid >= 0:
		DisplayServer.window_move_to_foreground(wid)


func hide_popup() -> void:
	hide()


func set_close_enabled(enabled: bool) -> void:
	_close_allowed = enabled
	if _close_button:
		_close_button.visible = enabled
		_close_button.disabled = not enabled


func get_content_root() -> Control:
	if _content_root != null:
		return _content_root
	return get_node_or_null("%ContentRoot") as Control


func _on_close_requested() -> void:
	if not _close_allowed:
		return
	hide_popup()


func _unhandled_input(event: InputEvent) -> void:
	if not event.is_action_pressed("toggle_gm"):
		return
	var gm := get_tree().root.get_node_or_null("GameRoot/GmToolsLayer")
	if gm and gm.has_method("toggle_panel"):
		gm.call("toggle_panel")
		get_viewport().set_input_as_handled()
