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
	always_on_top = true
	exclusive = false
	transient = true
	unresizable = false
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
	popup_centered()
	visible = true
	show()


func hide_popup() -> void:
	hide()


func set_close_enabled(enabled: bool) -> void:
	_close_allowed = enabled
	if _close_button:
		_close_button.visible = enabled
		_close_button.disabled = not enabled


func get_content_root() -> Control:
	return _content_root


func _on_close_requested() -> void:
	if not _close_allowed:
		return
	hide_popup()
