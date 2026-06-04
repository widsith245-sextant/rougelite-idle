extends PanelContainer
class_name EffectTooltipPopup

const EffectGlossaryLoaderScript := preload("res://gdscript/ui/effect_glossary_loader.gd")

static var _instance: EffectTooltipPopup

@onready var _label: RichTextLabel = %TooltipLabel


static func instance() -> EffectTooltipPopup:
	if _instance == null:
		var scene := load("res://scenes/ui/components/effect_tooltip_popup.tscn")
		_instance = scene.instantiate()
	return _instance


static func show_for(effect_id: String, pile: int, intensity: float, global_pos: Vector2) -> void:
	var popup := instance()
	if popup.get_parent() == null:
		var root: Window = Engine.get_main_loop().root
		root.add_child(popup)
	popup.visible = true
	popup._label.text = EffectGlossaryLoaderScript.build_tooltip_bbcode(effect_id, pile, intensity)
	popup.global_position = global_pos + Vector2(8, 8)
	popup.move_to_front()


static func hide_popup() -> void:
	if _instance != null:
		_instance.visible = false
