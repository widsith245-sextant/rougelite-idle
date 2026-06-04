extends VBoxContainer

## Shared 2 active + 1 passive skill slot layout for backpack tab and skill popup.

const ItemCellScene := preload("res://scenes/ui/components/item_grid_cell.tscn")
const MAX_ACTIVE := 2
const MAX_PASSIVE := 1

signal slot_pressed(slot_key: String)
signal slot_dropped(slot_key: String, target_index: int, payload: Dictionary)

var roster_id: String = ""
var interactive: bool = true
var cell_size: Vector2 = Vector2(52, 36)


func setup(p_roster_id: String, p_interactive: bool = true) -> void:
	roster_id = p_roster_id
	interactive = p_interactive
	refresh()


func refresh() -> void:
	for child in get_children():
		child.queue_free()

	var db := get_node_or_null("/root/DbManager")
	var max_active := MAX_ACTIVE
	var max_passive := MAX_PASSIVE
	var passive_ok := true
	if db:
		max_active = int(db.get("MaxActiveSkillSlots"))
		max_passive = int(db.get("MaxPassiveSkillSlots"))
		passive_ok = bool(db.get("PassiveSlotsUnlocked"))

	var mgr := get_node_or_null("/root/CharacterSkillManager")
	var equipped: Dictionary = {}
	var name_by_skill: Dictionary = {}
	if mgr and not roster_id.is_empty():
		var es: Variant = mgr.call("GetEquippedSnapshot", roster_id)
		if typeof(es) == TYPE_DICTIONARY:
			equipped = es
		var tree_snap: Variant = mgr.call("GetTreeSnapshot", roster_id)
		if typeof(tree_snap) == TYPE_ARRAY:
			for entry in tree_snap:
				var data: Dictionary = entry
				var skill_id: String = str(data.get("skill_id", ""))
				if not skill_id.is_empty():
					name_by_skill[skill_id] = str(data.get("display_name", skill_id))

	var active_count := _count_equipped(equipped, "active_", max_active)
	_add_section_title("主动技能  %d/%d" % [active_count, max_active])
	var active_row := _make_row()
	add_child(active_row)
	for i in MAX_ACTIVE:
		var slot_key := "active_%d" % i
		if i < max_active:
			_add_skill_cell(active_row, slot_key, equipped, name_by_skill, mgr, "主%d" % (i + 1))
		else:
			_add_lock_cell(active_row, "主%d" % (i + 1))

	var passive_count := _count_equipped(equipped, "passive_", max_passive) if passive_ok else 0
	var passive_cap := max_passive if passive_ok else 0
	_add_section_title("被动技能  %d/%d" % [passive_count, passive_cap])
	var pass_row := _make_row()
	add_child(pass_row)
	if passive_ok:
		for i in MAX_PASSIVE:
			var slot_key := "passive_%d" % i
			if i < max_passive:
				_add_skill_cell(pass_row, slot_key, equipped, name_by_skill, mgr, "被%d" % (i + 1))
			else:
				_add_lock_cell(pass_row, "被%d" % (i + 1))
	else:
		for i in MAX_PASSIVE:
			_add_lock_cell(pass_row, "被%d" % (i + 1))


func _count_equipped(equipped: Dictionary, prefix: String, limit: int) -> int:
	var count := 0
	for i in limit:
		var sid: String = str(equipped.get("%s%d" % [prefix, i], ""))
		if not sid.is_empty() and sid != "—":
			count += 1
	return count


func _add_section_title(text: String) -> void:
	var lbl := Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", 10)
	lbl.modulate = Color(0.82, 0.86, 0.92)
	add_child(lbl)


func _make_row() -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	return row


func _add_lock_cell(parent: Node, label: String) -> void:
	var cell: PanelContainer = ItemCellScene.instantiate()
	cell.cell_size = cell_size
	cell.setup_slot("🔒")
	cell.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(cell)


func _add_skill_cell(
	parent: Node,
	slot_key: String,
	equipped: Dictionary,
	name_by_skill: Dictionary,
	mgr: Node,
	empty_label: String,
) -> void:
	var cell: PanelContainer = ItemCellScene.instantiate()
	cell.cell_size = cell_size
	parent.add_child(cell)

	if not interactive:
		cell.drag_kind = ""
		cell.mouse_filter = Control.MOUSE_FILTER_IGNORE
	else:
		cell.drag_kind = "skill_slot"
		cell.set_meta("slot_key", slot_key)
		cell.set_meta("roster_id", roster_id)
		cell.cell_pressed.connect(func() -> void:
			slot_pressed.emit(slot_key)
		)
		cell.cell_dropped.connect(func(target_index: int, payload: Dictionary) -> void:
			slot_dropped.emit(slot_key, target_index, payload)
		)

	var sid: String = str(equipped.get(slot_key, ""))
	if sid.is_empty() or sid == "—":
		cell.setup_slot(empty_label)
		return

	var display := str(name_by_skill.get(sid, sid))
	cell.setup_item(display, 0, 1, "rare")
	if interactive and mgr and mgr.has_method("FindNodeIdForSkill"):
		var node_id: Variant = mgr.call("FindNodeIdForSkill", roster_id, sid)
		if node_id != null and str(node_id) != "":
			cell.set_meta("node_id", str(node_id))
