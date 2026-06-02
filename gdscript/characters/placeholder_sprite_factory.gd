class_name PlaceholderSpriteFactory
extends RefCounted

## Procedural 64×64 placeholder sprites when PNG assets are missing.

const GENERATED_DIR := "res://assets/sprites/characters/generated/"

static func build_visual_for_roster(roster_id: String) -> CharacterVisualResource:
	var base := color_for_roster(roster_id)
	var res := CharacterVisualResource.new()
	res.body_texture = _load_or_generate_roster(roster_id, "body", base)
	res.outfit_normal = _load_or_generate_roster(roster_id, "outfit", base.darkened(0.85))
	res.outfit_damaged = _load_or_generate_roster(roster_id, "outfit_damaged", base.lightened(0.2))
	res.shadow_texture = _load_or_generate_roster(roster_id, "shadow", Color(0, 0, 0, 0.35))
	res.head_accessory = _load_or_generate_roster(roster_id, "head", base.lightened(0.35))
	res.tint = Color.WHITE
	return res


static func build_portrait_for_roster(roster_id: String, damaged: bool = false) -> Texture2D:
	var cache_key := "portrait_damaged" if damaged else "portrait"
	var path := "%sroster_%s_%s.png" % [GENERATED_DIR, roster_id, cache_key]
	if ResourceLoader.exists(path):
		var cached: Texture2D = load(path)
		if cached:
			return cached

	var visual := build_visual_for_roster(roster_id)
	var img := Image.create(128, 128, false, Image.FORMAT_RGBA8)
	img.fill(Color(0.12, 0.14, 0.18, 1.0))
	_compose_portrait_layer(img, visual.body_texture, 32, 36)
	var outfit_tex: Texture2D = visual.outfit_damaged if damaged else visual.outfit_normal
	_compose_portrait_layer(img, outfit_tex, 32, 36)
	_compose_portrait_layer(img, visual.head_accessory, 36, 16)
	if damaged:
		for y in range(128):
			for x in range(128):
				var c := img.get_pixel(x, y)
				if c.a > 0.01:
					img.set_pixel(x, y, Color(c.r * 1.08, c.g * 0.82, c.b * 0.82, c.a))
	return ImageTexture.create_from_image(img)


static func build_portrait_for_unit(unit_id: String, damaged: bool = false) -> Texture2D:
	var visual := build_visual_for_unit(unit_id)
	var img := Image.create(128, 128, false, Image.FORMAT_RGBA8)
	img.fill(Color(0.12, 0.14, 0.18, 1.0))
	_compose_portrait_layer(img, visual.body_texture, 32, 36)
	var outfit_tex: Texture2D = visual.outfit_damaged if damaged else visual.outfit_normal
	_compose_portrait_layer(img, outfit_tex, 32, 36)
	_compose_portrait_layer(img, visual.head_accessory, 36, 16)
	if damaged:
		for y in range(128):
			for x in range(128):
				var c := img.get_pixel(x, y)
				if c.a > 0.01:
					img.set_pixel(x, y, Color(c.r * 1.08, c.g * 0.82, c.b * 0.82, c.a))
	return ImageTexture.create_from_image(img)


static func _compose_portrait_layer(target: Image, tex: Texture2D, offset_x: int, offset_y: int) -> void:
	if tex == null:
		return
	var src := tex.get_image()
	if src == null:
		return
	src = src.duplicate()
	src.resize(64, 64, Image.INTERPOLATE_NEAREST)
	target.blit_rect(src, Rect2i(0, 0, 64, 64), Vector2i(offset_x, offset_y))


static func build_visual_for_unit(unit_id: String) -> CharacterVisualResource:
	var res := CharacterVisualResource.new()
	res.body_texture = _load_or_generate(unit_id, "body", _body_color(unit_id))
	res.outfit_normal = _load_or_generate(unit_id, "outfit", _outfit_color(unit_id))
	res.outfit_damaged = _load_or_generate(unit_id, "outfit_damaged", _damaged_color(unit_id))
	res.shadow_texture = _load_or_generate(unit_id, "shadow", Color(0, 0, 0, 0.35))
	res.head_accessory = _load_or_generate(unit_id, "head", _head_color(unit_id))
	res.tint = Color.WHITE
	return res


static func color_for_roster(roster_id: String) -> Color:
	match roster_id:
		"vanguard_a": return Color(0.2, 0.6, 0.9)
		"sniper_b": return Color(0.2, 0.75, 0.5)
		"mage_c": return Color(0.85, 0.45, 0.2)
		"support_d": return Color(0.35, 0.85, 0.75)
		"berserker_e": return Color(0.9, 0.28, 0.28)
		_: return Color(0.5, 0.5, 0.55)


static func quality_for_roster(roster_id: String) -> String:
	match roster_id:
		"vanguard_a": return "common"
		"sniper_b": return "rare"
		"mage_c": return "epic"
		"support_d": return "rare"
		"berserker_e": return "epic"
		_: return "common"


static func _load_or_generate_roster(roster_id: String, layer: String, color: Color) -> Texture2D:
	var path := "%sroster_%s_%s.png" % [GENERATED_DIR, roster_id, layer]
	if ResourceLoader.exists(path):
		var tex: Texture2D = load(path)
		if tex:
			return tex
	return _make_texture(layer, color)


static func _load_or_generate(unit_id: String, layer: String, color: Color) -> Texture2D:
	var path := "%s%s_%s.png" % [GENERATED_DIR, unit_id, layer]
	if ResourceLoader.exists(path):
		var tex: Texture2D = load(path)
		if tex:
			return tex
	return _make_texture(layer, color)


static func _make_texture(layer: String, color: Color) -> ImageTexture:
	var img := Image.create(64, 64, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	match layer:
		"body":
			_fill_rect(img, Rect2i(16, 20, 32, 36), color)
		"outfit", "outfit_damaged":
			_fill_rect(img, Rect2i(20, 28, 24, 24), color)
		"head":
			_fill_rect(img, Rect2i(22, 8, 20, 20), color)
		"shadow":
			for y in range(64):
				for x in range(64):
					var dx := float(x - 32) / 20.0
					var dy := float(y - 56) / 4.0
					if dx * dx + dy * dy <= 1.0:
						img.set_pixel(x, y, color)
		_:
			_fill_rect(img, Rect2i(8, 8, 48, 48), color)

	var tex := ImageTexture.create_from_image(img)
	return tex


static func _fill_rect(img: Image, rect: Rect2i, color: Color) -> void:
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if x >= 0 and y >= 0 and x < img.get_width() and y < img.get_height():
				img.set_pixel(x, y, color)


static func _body_color(unit_id: String) -> Color:
	match unit_id:
		"ally_a": return Color(0.2, 0.6, 0.9)
		"ally_b": return Color(0.2, 0.75, 0.5)
		"ally_c": return Color(0.85, 0.45, 0.2)
		_: return Color(0.5, 0.5, 0.55)


static func _outfit_color(unit_id: String) -> Color:
	match unit_id:
		"ally_a": return Color(0.15, 0.45, 0.7)
		"ally_b": return Color(0.15, 0.6, 0.4)
		"ally_c": return Color(0.7, 0.35, 0.15)
		_: return Color(0.35, 0.35, 0.4)


static func _damaged_color(unit_id: String) -> Color:
	match unit_id:
		"ally_a": return Color(0.9, 0.4, 0.2, 0.85)
		"ally_b": return Color(0.9, 0.5, 0.3, 0.85)
		"ally_c": return Color(0.9, 0.3, 0.3, 0.85)
		_: return Color(0.9, 0.35, 0.35, 0.85)


static func _head_color(unit_id: String) -> Color:
	match unit_id:
		"ally_a": return Color(0.9, 0.8, 0.6)
		"ally_b": return Color(0.8, 0.9, 0.7)
		"ally_c": return Color(0.95, 0.8, 0.6)
		_: return Color(0.85, 0.75, 0.65)
