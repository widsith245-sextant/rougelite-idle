extends Control

## Thin stage progress bar above QuickBar with wave markers.

const BAR_HEIGHT := 6.0

@onready var _fill: ColorRect = %Fill
@onready var _markers: Control = %Markers

var _wave_progress: Array[float] = []
var _current_progress: float = 0.0
var _active_wave: int = -1


func _ready() -> void:
	custom_minimum_size = Vector2(400, BAR_HEIGHT)
	_load_wave_markers()
	_connect_bus()
	call_deferred("_sync_layout")


func _connect_bus() -> void:
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus == null:
		return
	if event_bus.has_signal("RunProgressChanged"):
		event_bus.connect("RunProgressChanged", _on_run_progress_changed)
	if event_bus.has_signal("WaveStarted"):
		event_bus.connect("WaveStarted", _on_wave_started)
	if event_bus.has_signal("StageIdChanged"):
		event_bus.connect("StageIdChanged", _on_stage_changed)
	if event_bus.has_signal("RunVisualModeChanged"):
		event_bus.connect("RunVisualModeChanged", _on_stage_changed)


func refresh_markers() -> void:
	_current_progress = 0.0
	_active_wave = -1
	_load_wave_markers()
	_sync_layout()


func _on_stage_changed(_arg: Variant = null) -> void:
	refresh_markers()


func _load_wave_markers() -> void:
	_wave_progress.clear()
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("GetCurrentStageWaveProgresses"):
		var waves: Variant = combat.call("GetCurrentStageWaveProgresses")
		if typeof(waves) == TYPE_ARRAY:
			for p in waves:
				_wave_progress.append(float(p))
	_build_markers()


func _build_markers() -> void:
	if _markers == null:
		return
	for child in _markers.get_children():
		child.queue_free()
	for i in _wave_progress.size():
		var dot := ColorRect.new()
		dot.custom_minimum_size = Vector2(4, 4)
		dot.color = Color(0.85, 0.75, 0.3, 0.9)
		dot.name = "Wave_%d" % i
		_markers.add_child(dot)


func _sync_layout() -> void:
	if _fill == null:
		return
	var width := size.x if size.x > 0 else 400.0
	_fill.size = Vector2(width * _current_progress, BAR_HEIGHT)
	_layout_markers(width)


func _layout_markers(width: float) -> void:
	if _markers == null:
		return
	for i in _markers.get_child_count():
		var dot: ColorRect = _markers.get_child(i)
		if i >= _wave_progress.size():
			continue
		var x := _wave_progress[i] * width - 2.0
		dot.position = Vector2(x, 1.0)
		if i == _active_wave:
			dot.color = Color(1.0, 0.45, 0.35, 1.0)
		elif i < _active_wave:
			dot.color = Color(0.35, 0.85, 0.45, 1.0)
		else:
			dot.color = Color(0.85, 0.75, 0.3, 0.9)


func _on_run_progress_changed(progress: float, wave_index: int, _wave_total: int) -> void:
	_current_progress = clampf(progress, 0.0, 1.0)
	_active_wave = wave_index
	_sync_layout()


func _on_wave_started(wave_index: int) -> void:
	_active_wave = wave_index
	_sync_layout()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_sync_layout()
