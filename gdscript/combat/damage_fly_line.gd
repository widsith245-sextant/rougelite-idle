extends Node2D

## Short-lived attack fly line in canvas space.

@export var duration := 0.18
@export var width := 2.0

var _from := Vector2.ZERO
var _to := Vector2.ZERO
var _color := Color.WHITE
var _elapsed := 0.0


func setup(from_pos: Vector2, to_pos: Vector2, color: Color) -> void:
	_from = from_pos
	_to = to_pos
	_color = color
	_elapsed = 0.0
	set_process(true)
	queue_redraw()


func _process(delta: float) -> void:
	_elapsed += delta
	modulate.a = 1.0 - clampf(_elapsed / duration, 0.0, 1.0)
	queue_redraw()
	if _elapsed >= duration:
		queue_free()


func _draw() -> void:
	draw_line(_from, _to, _color, width)
