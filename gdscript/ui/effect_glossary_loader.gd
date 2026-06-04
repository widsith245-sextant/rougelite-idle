extends RefCounted
class_name EffectGlossaryLoader

const GLOSSARY_PATH := "res://data/tables/ui/effect_glossary.json"
const DISPLAY_PATH := "res://data/tables/ui/skill_display.json"

static var _glossary: Dictionary = {}
static var _token_labels: Dictionary = {}


static func ensure_loaded() -> void:
	if not _glossary.is_empty():
		return
	var glossary_text := FileAccess.get_file_as_string(GLOSSARY_PATH)
	if not glossary_text.is_empty():
		var parsed: Variant = JSON.parse_string(glossary_text)
		if parsed is Dictionary:
			_glossary = parsed.get("effects", {})
	var display_text := FileAccess.get_file_as_string(DISPLAY_PATH)
	if not display_text.is_empty():
		var parsed_display: Variant = JSON.parse_string(display_text)
		if parsed_display is Dictionary:
			_token_labels = parsed_display.get("tokenLabels", {})


static func get_icon(effect_id: String) -> String:
	ensure_loaded()
	var entry: Dictionary = _glossary.get(effect_id, {})
	return str(entry.get("icon", "•"))


static func get_name(effect_id: String) -> String:
	ensure_loaded()
	var entry: Dictionary = _glossary.get(effect_id, {})
	return str(entry.get("name", effect_id))


static func get_summary(effect_id: String) -> String:
	ensure_loaded()
	var entry: Dictionary = _glossary.get(effect_id, {})
	return str(entry.get("summary", ""))


static func token_label(token: String) -> String:
	ensure_loaded()
	return str(_token_labels.get(token, token))


static func build_effect_bbcode(effect_id: String, pile: int, intensity: float) -> String:
	ensure_loaded()
	var entry: Dictionary = _glossary.get(effect_id, {})
	var template := str(entry.get("richText", get_icon(effect_id)))
	var text := template.replace("{pile}", str(pile)).replace("{intensity}", str(intensity))
	text = text.replace("{shield}", str(intensity))
	return "[url=%s]%s[/url]" % [effect_id, text]


static func build_skill_detail_bbcode(snapshot: Dictionary) -> String:
	if snapshot.is_empty():
		return "选择技能节点后点击插槽或「装配到槽位」"
	var lines: PackedStringArray = []
	var title := str(snapshot.get("display_name", "?"))
	var node_type := "主动" if str(snapshot.get("node_type", "")) == "active" else "被动"
	lines.append("[b]%s[/b]（%s）" % [title, node_type])
	var tokens: Array = snapshot.get("description_tokens", [])
	for token in tokens:
		lines.append("[i]%s[/i]" % token_label(str(token)))
	var effects: Array = snapshot.get("applied_effects", [])
	if not effects.is_empty():
		lines.append("")
		for effect in effects:
			if effect is Dictionary:
				var data: Dictionary = effect
				var effect_id := str(data.get("effect_id", ""))
				if effect_id.is_empty():
					continue
				lines.append(build_effect_bbcode(
					effect_id,
					int(data.get("pile", 1)),
					float(data.get("intensity", 0.0)),
				))
	return "\n".join(lines)


static func build_tooltip_bbcode(effect_id: String, pile: int, intensity: float) -> String:
	ensure_loaded()
	var entry: Dictionary = _glossary.get(effect_id, {})
	var title := str(entry.get("name", effect_id))
	var summary := str(entry.get("summary", ""))
	var detail := str(entry.get("richText", ""))
	detail = detail.replace("{pile}", str(pile)).replace("{intensity}", str(intensity))
	detail = detail.replace("{shield}", str(intensity))
	return "[b]%s[/b]\n%s\n%s" % [title, summary, detail]
