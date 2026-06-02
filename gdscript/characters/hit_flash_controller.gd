extends Node

## Brief white flash on Body_Base when taking damage.

const FLASH_SHADER := preload("res://assets/shaders/hit_flash.gdshader")

@onready var _body: Sprite2D = $"../Body_Base"

var _material: ShaderMaterial
var _flash_tween: Tween


func _ready() -> void:
	if _body == null:
		return
	_material = ShaderMaterial.new()
	_material.shader = FLASH_SHADER
	_material.set_shader_parameter("flash_amount", 0.0)
	_body.material = _material


func flash() -> void:
	if _material == null:
		return
	if _flash_tween:
		_flash_tween.kill()
	_flash_tween = create_tween()
	_material.set_shader_parameter("flash_amount", 0.85)
	_flash_tween.tween_method(_set_flash, 0.85, 0.0, 0.18)


func _set_flash(amount: float) -> void:
	if _material:
		_material.set_shader_parameter("flash_amount", amount)
