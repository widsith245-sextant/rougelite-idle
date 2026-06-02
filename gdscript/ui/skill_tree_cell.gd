extends Button
class_name SkillTreeCell

## Draggable skill tree node button.

var roster_id: String = ""
var node_id: String = ""
var node_type: String = "active"


func _get_drag_data(_at_position: Vector2) -> Variant:
	if node_id.is_empty():
		return null
	var payload := {
		"drag_kind": "skill_node",
		"roster_id": roster_id,
		"node_id": node_id,
		"node_type": node_type,
	}
	set_drag_preview(_make_preview())
	return payload


func _make_preview() -> Control:
	var preview := PanelContainer.new()
	preview.custom_minimum_size = Vector2(72, 36)
	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	preview.add_child(label)
	return preview
