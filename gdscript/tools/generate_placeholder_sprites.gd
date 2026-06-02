@tool
extends EditorScript

## One-time editor utility: writes PNG placeholders to assets/sprites/characters/generated/

const OUTPUT_DIR := "res://assets/sprites/characters/generated/"
const UNIT_IDS := ["ally_a", "ally_b", "ally_c"]


func _run() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT_DIR))
	for unit_id in UNIT_IDS:
		var visual := PlaceholderSpriteFactory.build_visual_for_unit(unit_id)
		_save_texture(visual.body_texture, "%s%s_body.png" % [OUTPUT_DIR, unit_id])
		_save_texture(visual.outfit_normal, "%s%s_outfit.png" % [OUTPUT_DIR, unit_id])
		_save_texture(visual.outfit_damaged, "%s%s_outfit_damaged.png" % [OUTPUT_DIR, unit_id])
		_save_texture(visual.head_accessory, "%s%s_head.png" % [OUTPUT_DIR, unit_id])
		_save_texture(visual.shadow_texture, "%s%s_shadow.png" % [OUTPUT_DIR, unit_id])
	print("Placeholder sprites written to ", OUTPUT_DIR)


func _save_texture(tex: Texture2D, path: String) -> void:
	if tex == null:
		return
	var img := tex.get_image()
	if img:
		img.save_png(ProjectSettings.globalize_path(path))
