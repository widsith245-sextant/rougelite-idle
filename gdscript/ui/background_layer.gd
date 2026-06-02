extends CanvasLayer

## Dual-theme background: training (gray) vs wonderland (purple gradient).

const TRAINING_BG := Color("#14181f")
const WONDERLAND_TOP := Color("#120818")
const WONDERLAND_BOTTOM := Color("#1a1030")
const WONDERLAND_BAND := Color(0.45, 0.22, 0.62, 0.35)

@onready var _placeholder: ColorRect = $ScrollRoot/Placeholder
@onready var _grid: ColorRect = $ScrollRoot/GridOverlay
@onready var _band: ColorRect = $ScrollRoot/WonderBand
@onready var _hint: Label = $ScrollRoot/Hint

var _theme_mode: String = "training"
var _theme_tween: Tween


func _ready() -> void:
	if _hint:
		_hint.visible = false
	_apply_training_visuals()
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("RunVisualModeChanged"):
		event_bus.connect("RunVisualModeChanged", _on_run_visual_mode_changed)


func set_theme_mode(mode: String) -> void:
	if mode == _theme_mode:
		return
	_theme_mode = mode
	if _theme_tween and _theme_tween.is_valid():
		_theme_tween.kill()
	if mode == "wonderland":
		_tween_to_wonderland()
	else:
		_tween_to_training()


func _on_run_visual_mode_changed(mode: String) -> void:
	set_theme_mode(mode)


func _apply_training_visuals() -> void:
	if _placeholder:
		_placeholder.color = TRAINING_BG
	if _grid:
		_grid.visible = true
		_grid.color = Color(0.18, 0.2, 0.24, 0.25)
	if _band:
		_band.visible = false


func _apply_wonderland_visuals() -> void:
	if _placeholder:
		_placeholder.color = WONDERLAND_BOTTOM
	if _grid:
		_grid.visible = false
	if _band:
		_band.visible = true
		_band.color = WONDERLAND_BAND


func _tween_to_training() -> void:
	if _placeholder == null:
		return
	_theme_tween = create_tween()
	_theme_tween.set_parallel(true)
	_theme_tween.tween_property(_placeholder, "color", TRAINING_BG, 0.3)
	if _grid:
		_theme_tween.tween_callback(func() -> void:
			_grid.visible = true
		)
	if _band:
		_theme_tween.tween_property(_band, "modulate:a", 0.0, 0.3)
		_theme_tween.chain().tween_callback(func() -> void:
			_band.visible = false
			_band.modulate.a = 1.0
		)


func _tween_to_wonderland() -> void:
	if _placeholder == null:
		return
	if _band:
		_band.visible = true
		_band.modulate.a = 0.0
	_theme_tween = create_tween()
	_theme_tween.set_parallel(true)
	_theme_tween.tween_property(_placeholder, "color", WONDERLAND_BOTTOM, 0.3)
	if _grid:
		_theme_tween.tween_callback(func() -> void:
			_grid.visible = false
		)
	if _band:
		_theme_tween.tween_property(_band, "modulate:a", 1.0, 0.3)
