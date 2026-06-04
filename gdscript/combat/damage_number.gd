extends Node2D

## Floating damage number popup.

@export var float_distance := 36.0
@export var duration := 0.6

var _label: Label
var _category := "physical"


func setup(amount: float, opts: Dictionary = {}) -> void:
	_category = str(opts.get("category", "physical"))
	var is_crit := bool(opts.get("is_crit", false))
	_label = Label.new()
	_label.text = _format_text(amount, opts)
	_label.add_theme_font_size_override("font_size", 16 if is_crit else 14)
	_label.modulate = _color_for_category(_category, is_crit)
	add_child(_label)

	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(self, "position:y", position.y - float_distance, duration)
	tween.tween_property(_label, "modulate:a", 0.0, duration)
	tween.chain().tween_callback(queue_free)


func _format_text(amount: float, opts: Dictionary) -> String:
	var value := str(int(amount))
	match _category:
		"shield":
			return "吸收 %s" % value
		"heal":
			return "+%s" % value
		"mark":
			return "+%s" % value
		"retaliate":
			return "反 %s" % value
		_:
			if bool(opts.get("is_crit", false)):
				return "%s!" % value
			return value


func _color_for_category(category: String, is_crit: bool) -> Color:
	if is_crit:
		return Color(1.0, 0.92, 0.35, 1.0)
	match category:
		"magical":
			return Color(0.62, 0.55, 1.0, 1.0)
		"mark":
			return Color(1.0, 0.66, 0.28, 1.0)
		"shield":
			return Color(0.35, 0.9, 1.0, 1.0)
		"retaliate":
			return Color(1.0, 0.35, 0.35, 1.0)
		"heal":
			return Color(0.35, 0.95, 0.45, 1.0)
		_:
			return Color(1.0, 0.95, 0.82, 1.0)
