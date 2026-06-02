extends Node2D

## Short ghost trail for reposition skills.

func play_at(host: Node2D) -> void:
	if host == null:
		return
	var ghost := Sprite2D.new()
	if host is CharacterBase:
		var body: Sprite2D = host.get_node_or_null("Body_Base")
		if body and body.texture:
			ghost.texture = body.texture
	ghost.global_position = host.global_position
	ghost.modulate.a = 0.5
	get_tree().current_scene.add_child(ghost)
	var tween := create_tween()
	tween.tween_property(ghost, "modulate:a", 0.0, 0.2)
	tween.tween_callback(ghost.queue_free)
