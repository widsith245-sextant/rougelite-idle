extends Control

const LABELS_PATH := "res://data/tables/ui/stat_labels.json"

const SECTIONS := {
	"attack": ["Damage", "Dps", "AtkSpeed", "AtkRange", "CritRate", "CritDamage", "PhysIncrease"],
	"casting": ["Cdr", "CastSpeed", "SkillArea"],
	"defense": ["Defense", "FireResist", "ColdResist", "LightningResist", "ChaosResist", "Dodge", "Block"],
	"sustain": ["HpRegen", "LifeOnHit", "LifeOnKill", "LifeLeech"],
	"mobility": ["MoveSpeed"],
}

const COMPARE_KEYS := ["MoveSpeed"]

@onready var _header_label: Label = %HeaderLabel
@onready var _attack_box: VBoxContainer = %AttackBox
@onready var _casting_box: VBoxContainer = %CastingBox
@onready var _defense_box: VBoxContainer = %DefenseBox
@onready var _sustain_box: VBoxContainer = %SustainBox
@onready var _mobility_box: VBoxContainer = %MobilityBox

var _unit_id: String = "ally_a"
var _label_map: Dictionary = {}


func _ready() -> void:
	_load_labels()
	refresh()


func set_unit_id(unit_id: String) -> void:
	if unit_id.is_empty():
		return
	_unit_id = unit_id


func refresh() -> void:
	var stats := get_node_or_null("/root/StatsService")
	if stats == null:
		return
	var full_snap: Variant = stats.call("GetSnapshot", _unit_id)
	var compare_snap: Variant = stats.call("GetSnapshotWithComparison", _unit_id)
	if typeof(full_snap) != TYPE_DICTIONARY:
		return
	var full: Dictionary = full_snap
	var compare: Dictionary = full
	if typeof(compare_snap) == TYPE_DICTIONARY:
		compare = compare_snap
	_refresh_header(compare)
	_fill_section(_attack_box, SECTIONS.attack, full, false)
	_fill_section(_casting_box, SECTIONS.casting, full, false)
	_fill_section(_defense_box, SECTIONS.defense, full, false)
	_fill_section(_sustain_box, SECTIONS.sustain, full, false)
	_fill_section(_mobility_box, SECTIONS.mobility, compare, true)


func _refresh_header(snap: Dictionary) -> void:
	if _header_label == null:
		return
	var party := get_node_or_null("/root/PartyManager")
	var display := _unit_id
	if party:
		display = str(party.call("GetDisplayNameForUnit", _unit_id))
	var class_id: String = str(snap.get("class_id", ""))
	if class_id.is_empty():
		_header_label.text = display
	else:
		_header_label.text = "%s · %s" % [display, class_id]


func _load_labels() -> void:
	if not FileAccess.file_exists(LABELS_PATH):
		return
	var file := FileAccess.open(LABELS_PATH, FileAccess.READ)
	if file == null:
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return
	var stats_block: Variant = parsed.get("stats", parsed)
	if typeof(stats_block) != TYPE_DICTIONARY:
		return
	for key in stats_block.keys():
		var val: Variant = stats_block[key]
		if typeof(val) == TYPE_DICTIONARY:
			_label_map[str(key)] = str(val.get("displayName", key))
		else:
			_label_map[str(key)] = str(val)


func _fill_section(box: VBoxContainer, keys: Array, snap: Dictionary, use_compare: bool) -> void:
	if box == null:
		return
	for child in box.get_children():
		child.queue_free()
	for key in keys:
		var label := Label.new()
		var display := str(_label_map.get(key, key))
		if use_compare or key in COMPARE_KEYS:
			label.text = "%s: %s" % [display, _format_compare_line(key, snap)]
		else:
			var val: float = float(snap.get(key, 0.0))
			label.text = "%s: %s" % [display, _format_single(key, val)]
		box.add_child(label)


func _format_compare_line(key: String, snap: Dictionary) -> String:
	var base_val: float = float(snap.get("base_%s" % key, 0.0))
	var final_val: float = float(snap.get("final_%s" % key, 0.0))
	var delta: float = float(snap.get("delta_%s" % key, 0.0))
	var b := _format_single(key, base_val)
	var f := _format_single(key, final_val)
	if absf(final_val - base_val) < 0.001:
		return f
	var line := "%s → %s" % [b, f]
	if absf(delta) > 0.001:
		var sign := "+" if delta > 0 else ""
		line += " (%s%s)" % [sign, _format_single(key, delta)]
	return line


func _format_single(key: String, raw: float) -> String:
	if key.ends_with("Rate") or key.ends_with("Resist") or key == "Cdr" or key == "Dodge" or key == "Block":
		return "%.1f%%" % (raw * 100.0)
	if key == "Level":
		return str(int(raw))
	if key in ["MaxHp", "Damage", "Dps"]:
		return "%.0f" % raw
	return "%.1f" % raw
