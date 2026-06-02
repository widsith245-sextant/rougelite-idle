extends Node2D

## Floating damage number popup.

@export var float_distance := 36.0
@export var duration := 0.6

var _label: Label


func setup(amount: float) -> void:
	_label = Label.new()
	_label.text = str(int(amount))
	_label.add_theme_font_size_override("font_size", 14)
	add_child(_label)

	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(self, "position:y", position.y - float_distance, duration)
	tween.tween_property(_label, "modulate:a", 0.0, duration)
	tween.chain().tween_callback(queue_free)
