class_name ClassSkillsLoader
extends RefCounted

const SKILLS_PATH := "res://data/tables/character/class_skills.json"
const CAST_TEXTS_PATH := "res://data/tables/ui/skill_cast_texts.json"


static func class_id_for_unit(unit_id: String) -> String:
	var party := _get_party()
	if party:
		var cid: Variant = party.call("GetClassIdForUnit", unit_id)
		if str(cid) != "":
			return str(cid)
	return _fallback_class(unit_id)


static func display_name_for_unit(unit_id: String) -> String:
	var party := _get_party()
	if party:
		var name: Variant = party.call("GetDisplayNameForUnit", unit_id)
		if str(name) != "":
			return str(name)
	return unit_id


static func build_skill_lines_for_class(class_id: String) -> PackedStringArray:
	if class_id.is_empty():
		return PackedStringArray()
	var skills_data := _load_skills()
	var cast_texts := _load_cast_texts()
	var lines: PackedStringArray = []
	for entry in skills_data.get("classes", []):
		if str(entry.get("classId", "")) != class_id:
			continue
		lines.append("【主动】")
		for skill in entry.get("actives", []):
			lines.append(_format_skill(skill, cast_texts, true))
		lines.append("【被动】")
		for skill in entry.get("passives", []):
			lines.append(_format_skill(skill, cast_texts, false))
		break
	return lines


static func build_skill_lines(unit_id: String) -> PackedStringArray:
	return build_skill_lines_for_class(class_id_for_unit(unit_id))


static func _get_party() -> Node:
	var tree := Engine.get_main_loop()
	if tree is SceneTree:
		return (tree as SceneTree).root.get_node_or_null("/root/PartyManager")
	return null


static func _fallback_class(unit_id: String) -> String:
	match unit_id:
		"ally_a":
			return "Vanguard_01"
		"ally_b":
			return "Sniper_01"
		"ally_c":
			return "Mage_01"
		_:
			return ""


static func _format_skill(skill: Dictionary, cast_texts: Dictionary, is_active: bool) -> String:
	var sid: String = str(skill.get("id", ""))
	var entry: Dictionary = cast_texts.get(sid, {})
	var name: String = str(entry.get("name", sid))
	var mult: float = float(skill.get("skillMultiplier", 1.0))
	var tags: Array = skill.get("moveTags", [])
	var tag_text := ""
	for tag in tags:
		if typeof(tag) != TYPE_DICTIONARY:
			continue
		var kind: String = str(tag.get("kind", ""))
		var dist: float = float(tag.get("distance", 0))
		if kind == "Charge":
			tag_text += " Charge %d" % int(dist)
		elif kind == "Retreat":
			tag_text += " 后退 %d" % int(dist)
	if not is_active:
		var trigger: String = str(skill.get("triggerType", ""))
		var slot: int = int(skill.get("targetSlot", -1))
		if trigger == "OnPointBlank":
			return "· %s (距敌≤%d, ×%.1f)" % [name, slot, mult]
		return "· %s (%s ×%.1f)" % [name, trigger, mult]
	if tag_text.is_empty():
		return "· %s (×%.1f)" % [name, mult]
	return "· %s (×%.1f%s)" % [name, mult, tag_text]


static func _load_skills() -> Dictionary:
	return _load_json(SKILLS_PATH)


static func _load_cast_texts() -> Dictionary:
	return _load_json(CAST_TEXTS_PATH)


static func _load_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}
