extends Control

## Drag source for roster portrait (doll host overlay).

var roster_id: String = ""


func _get_drag_data(_at_position: Vector2) -> Variant:
	if roster_id.is_empty():
		return null
	return {
		"drag_kind": "roster",
		"roster_id": roster_id,
		"source": "doll",
		"cell_index": -1,
	}
