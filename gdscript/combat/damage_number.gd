extends Control

## Floating damage number popup (Control, CombatCoords-aligned).

@export var float_distance := 36.0
@export var duration := 0.6

var _label: Label
var _category := "physical"
var _follow_anchor: Node2D
var _follow_offset := Vector2.ZERO
var _float_y := 0.0
var _camera: Camera2D
var _fixed_canvas_pos: Vector2 = Vector2.INF


func setup(amount: float, opts: Dictionary = {}) -> void:
	_category = str(opts.get("category", "physical"))
	var is_crit := bool(opts.get("is_crit", false))
	var anchor = opts.get("follow_anchor", null)
	if anchor != null and is_instance_valid(anchor):
		_follow_anchor = anchor
	_camera = opts.get("camera") as Camera2D
	if opts.has("follow_offset"):
		_follow_offset = opts["follow_offset"]
	if opts.has("canvas_pos"):
		_fixed_canvas_pos = opts["canvas_pos"]
	elif _follow_anchor != null and is_instance_valid(_follow_anchor):
		_fixed_canvas_pos = CombatCoords.doll_to_canvas_pos(_follow_anchor, _camera)

	mouse_filter = Control.MOUSE_FILTER_IGNORE
	custom_minimum_size = Vector2(48, 20)

	_label = Label.new()
	_label.text = _format_text(amount, opts)
	_label.add_theme_font_size_override("font_size", 16 if is_crit else 14)
	_label.modulate = _color_for_category(_category, is_crit)
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_label)

	_sync_follow_position()

	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(self, "_float_y", -float_distance, duration)
	tween.tween_property(_label, "modulate:a", 0.0, duration)
	tween.chain().tween_callback(queue_free)


func _process(_delta: float) -> void:
	if _follow_anchor != null and not is_instance_valid(_follow_anchor):
		_follow_anchor = null
	_sync_follow_position()


func _sync_follow_position() -> void:
	var canvas_pos: Vector2
	if _follow_anchor != null and is_instance_valid(_follow_anchor):
		canvas_pos = CombatCoords.doll_to_canvas_pos(_follow_anchor, _camera)
	elif _fixed_canvas_pos != Vector2.INF:
		canvas_pos = _fixed_canvas_pos
	else:
		return
	global_position = canvas_pos + CombatCoords.HUD_DAMAGE_OFFSET + _follow_offset + Vector2(0, _float_y)


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
