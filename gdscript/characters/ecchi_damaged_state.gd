extends Node

## Outfit swap, blush shader, and subtle shake when HP drops below 30%.

const BLUSH_SHADER := preload("res://assets/shaders/ecchi_blush.gdshader")

@export var outfit_normal: Texture2D
@export var outfit_damaged: Texture2D

@onready var _outfit: Sprite2D = $"../Outfit"
@onready var _body_base: Sprite2D = $"../Body_Base"

var _active := false
var _shake_tween: Tween
var _blush_material: ShaderMaterial


func _ready() -> void:
	if _body_base:
		_blush_material = ShaderMaterial.new()
		_blush_material.shader = BLUSH_SHADER
		_blush_material.set_shader_parameter("blush_strength", 0.0)


func configure(normal: Texture2D, damaged: Texture2D) -> void:
	outfit_normal = normal
	outfit_damaged = damaged
	if _outfit and outfit_normal:
		_outfit.texture = outfit_normal


func set_active(active: bool) -> void:
	if _active == active:
		return
	_active = active
	if _outfit and outfit_damaged and outfit_normal:
		_outfit.texture = outfit_damaged if active else outfit_normal
	if _body_base and _blush_material:
		_body_base.material = _blush_material if active else null
		if active:
			_blush_material.set_shader_parameter("blush_strength", 0.45)
	if active:
		_start_shake()
	else:
		_stop_shake()


func _start_shake() -> void:
	if _body_base == null:
		return
	if _shake_tween:
		_shake_tween.kill()
	var base_x := _body_base.position.x
	_shake_tween = create_tween()
	_shake_tween.set_loops()
	_shake_tween.tween_property(_body_base, "position:x", base_x + 0.5, 0.05)
	_shake_tween.tween_property(_body_base, "position:x", base_x - 0.5, 0.05)


func _stop_shake() -> void:
	if _shake_tween:
		_shake_tween.kill()
		_shake_tween = null
