class_name UiLabelsLoader

## Loads slot/stat display names from data/tables/ui/*.json (full Chinese labels for UI).

const SLOT_PATH := "res://data/tables/ui/slot_labels.json"
const STAT_PATH := "res://data/tables/ui/stat_labels.json"

const STAT_COMPARE_KEYS: Array[String] = [
	"Level", "MaxHp", "Dps", "Damage", "AtkSpeed", "AtkRange", "MoveSpeed", "CritRate",
]

static var _slots_loaded := false
static var _slot_by_id: Dictionary = {}
static var _stats_loaded := false
static var _stat_by_key: Dictionary = {}


static func get_slot_display_name(slot_id: String) -> String:
	_ensure_slots()
	var entry: Variant = _slot_by_id.get(slot_id)
	if entry is Dictionary:
		return str(entry.get("displayName", slot_id))
	return slot_id


static func get_slot_icon_path(slot_id: String) -> String:
	_ensure_slots()
	var entry: Variant = _slot_by_id.get(slot_id)
	if entry is Dictionary:
		var path: Variant = entry.get("iconPath")
		if path != null and str(path) != "":
			return str(path)
	return ""


static func get_stat_display_name(stat_key: String) -> String:
	_ensure_stats()
	var entry: Variant = _stat_by_key.get(stat_key)
	if entry is Dictionary:
		return str(entry.get("displayName", stat_key))
	if _stat_by_key.has(stat_key) and typeof(_stat_by_key[stat_key]) == TYPE_STRING:
		return str(_stat_by_key[stat_key])
	return stat_key


static func get_stat_compare_keys() -> Array[String]:
	return STAT_COMPARE_KEYS.duplicate()


static func _ensure_slots() -> void:
	if _slots_loaded:
		return
	_slots_loaded = true
	if not FileAccess.file_exists(SLOT_PATH):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(SLOT_PATH))
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	for entry in parsed.get("slots", []):
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		var id := str(entry.get("id", ""))
		if not id.is_empty():
			_slot_by_id[id] = entry


static func _ensure_stats() -> void:
	if _stats_loaded:
		return
	_stats_loaded = true
	if not FileAccess.file_exists(STAT_PATH):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(STAT_PATH))
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	var stats: Variant = parsed.get("stats", parsed)
	if typeof(stats) != TYPE_DICTIONARY:
		return
	for key in stats.keys():
		var val: Variant = stats[key]
		if typeof(val) == TYPE_DICTIONARY:
			_stat_by_key[str(key)] = val
		else:
			_stat_by_key[str(key)] = { "displayName": str(val) }
