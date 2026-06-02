extends Control

## Base class for bottom drawer panels. Slides from Y:150 to Y:20 without dialogs.

const PANEL_HIDDEN_Y := 150
const PANEL_VISIBLE_Y := 20
const SLIDE_DURATION := 0.25

signal panel_closed

@export var panel_title: String = ""

@onready var _title_label: Label = %TitleLabel
@onready var _close_button: Button = %CloseButton
@onready var _panel_root: Control = %PanelRoot

var _tween: Tween


func _ready() -> void:
	if _title_label:
		_title_label.text = panel_title
	if _close_button:
		_close_button.pressed.connect(_on_close_pressed)
	_panel_root.position.y = PANEL_HIDDEN_Y
	visible = false


func open() -> void:
	visible = true
	_animate_to(PANEL_VISIBLE_Y)


func close() -> void:
	_animate_to(PANEL_HIDDEN_Y, true)


func _animate_to(target_y: float, hide_after := false) -> void:
	if _tween:
		_tween.kill()
	_tween = create_tween()
	_tween.set_trans(Tween.TRANS_CUBIC)
	_tween.set_ease(Tween.EASE_OUT)
	_tween.tween_property(_panel_root, "position:y", target_y, SLIDE_DURATION)
	if hide_after:
		_tween.tween_callback(_finish_close)


func _finish_close() -> void:
	visible = false
	panel_closed.emit()


func _on_close_pressed() -> void:
	close()
