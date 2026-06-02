extends Control

## Per-character skill tree + slot equip.

const ItemCellScene := preload("res://scenes/ui/components/item_grid_cell.tscn")

@onready var _roster_tab: TabBar = %RosterTabBar
@onready var _tree_grid: GridContainer = %TreeGrid
@onready var _slot_box: VBoxContainer = %SlotBox
@onready var _detail: Label = %DetailLabel
@onready var _equip_btn: Button = %EquipButton

var _selected_node_id: String = ""
var _current_roster_id: String = "vanguard_a"
var _selected_slot_key: String = "active_0"
var _tab_roster_ids: Array = []


func _ready() -> void:
	if _roster_tab:
		_roster_tab.tab_changed.connect(_on_roster_tab_changed)
	if _equip_btn:
		_equip_btn.pressed.connect(_on_equip_pressed)
	call_deferred("refresh")


func refresh() -> void:
	_rebuild_roster_tabs_if_needed()
	_refresh_tree()
	_refresh_slots()
	_update_detail()


func _rebuild_roster_tabs_if_needed() -> void:
	if _roster_tab == null:
		return
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var snap: Variant = party.call("GetRosterSnapshot")
	if typeof(snap) != TYPE_ARRAY:
		return

	var unlocked: Array = []
	for entry in snap:
		var data: Dictionary = entry
		if bool(data.get("unlocked", false)):
			unlocked.append(str(data.get("roster_id", "")))

	if unlocked == _tab_roster_ids and _roster_tab.tab_count > 0:
		return

	_tab_roster_ids = unlocked.duplicate()
	while _roster_tab.tab_count > 0:
		_roster_tab.remove_tab(0)
	for rid in _tab_roster_ids:
		var title := str(rid).substr(0, 4)
		for entry in snap:
			var data: Dictionary = entry
			if str(data.get("roster_id", "")) == rid:
				title = str(data.get("display_name", title)).substr(0, 4)
				break
		_roster_tab.add_tab(title)

	if _tab_roster_ids.is_empty():
		return

	var pick := 0
	for i in _tab_roster_ids.size():
		if str(_tab_roster_ids[i]) == _current_roster_id:
			pick = i
			break
	_roster_tab.current_tab = pick
	_current_roster_id = str(_tab_roster_ids[pick])


func _on_roster_tab_changed(tab: int) -> void:
	if tab < 0 or tab >= _tab_roster_ids.size():
		return
	_current_roster_id = str(_tab_roster_ids[tab])
	_selected_node_id = ""
	_refresh_tree()
	_refresh_slots()
	_update_detail()


func _refresh_tree() -> void:
	if _tree_grid == null:
		return
	for child in _tree_grid.get_children():
		child.queue_free()
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr == null:
		return
	var snap: Variant = mgr.call("GetTreeSnapshot", _current_roster_id)
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var node_id: String = str(data.get("id", ""))
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(72, 36)
		var prefix := "主" if str(data.get("node_type", "")) == "active" else "被"
		var req_lv: int = int(data.get("required_level", 1))
		btn.text = "%s Lv%d %s" % [prefix, req_lv, str(data.get("display_name", "?"))]
		if bool(data.get("unlocked", false)):
			btn.modulate = Color(0.85, 1.0, 0.85)
			btn.pressed.connect(_on_select_node.bind(node_id))
		elif bool(data.get("can_unlock", false)):
			btn.modulate = Color(0.85, 0.95, 1.0)
			btn.pressed.connect(_on_unlock_node.bind(node_id))
		elif not bool(data.get("level_ok", true)):
			btn.disabled = true
			btn.text += " 🔒"
		else:
			btn.disabled = true
		_tree_grid.add_child(btn)


func _refresh_slots() -> void:
	if _slot_box == null:
		return
	for child in _slot_box.get_children():
		child.queue_free()
	var db := get_node_or_null("/root/DbManager")
	var max_active := 1
	var passive_ok := false
	if db:
		max_active = int(db.get("MaxActiveSkillSlots"))
		passive_ok = bool(db.get("PassiveSlotsUnlocked"))
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	var equipped: Dictionary = {}
	if mgr:
		var snap: Variant = mgr.call("GetEquippedSnapshot", _current_roster_id)
		if typeof(snap) == TYPE_DICTIONARY:
			equipped = snap

	var slot_row := HBoxContainer.new()
	slot_row.add_theme_constant_override("separation", 4)
	_slot_box.add_child(slot_row)

	for i in max_active:
		var slot_key := "active_%d" % i
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = Vector2(56, 40)
		cell.drag_kind = "skill_slot"
		cell.cell_index = i
		cell.cell_pressed.connect(_on_skill_slot_pressed.bind(slot_key))
		slot_row.add_child(cell)
		var sid: String = str(equipped.get(slot_key, ""))
		if sid.is_empty() or sid == "—":
			cell.setup_slot("主%d" % (i + 1))
		else:
			cell.setup_item(sid.substr(0, mini(4, sid.length())), 0, 1, "rare")

	for i in range(max_active, 2):
		var lock_l := Label.new()
		lock_l.text = "主动%d: 🔒" % [i + 1]
		lock_l.add_theme_font_size_override("font_size", 10)
		_slot_box.add_child(lock_l)

	var pass_row := HBoxContainer.new()
	_slot_box.add_child(pass_row)
	var pass_cell: PanelContainer = ItemCellScene.instantiate()
	pass_cell.cell_size = Vector2(56, 40)
	pass_cell.cell_index = 0
	pass_cell.drag_kind = "skill_slot"
	pass_cell.cell_pressed.connect(_on_skill_slot_pressed.bind("passive_0"))
	pass_row.add_child(pass_cell)
	if passive_ok:
		var pid: String = str(equipped.get("passive_0", ""))
		if pid.is_empty() or pid == "—":
			pass_cell.setup_slot("被")
		else:
			pass_cell.setup_item(pid.substr(0, mini(4, pid.length())), 0, 1, "epic")
	else:
		pass_cell.setup_slot("🔒")


func _update_detail() -> void:
	if _detail == null:
		return
	if _selected_node_id.is_empty():
		_detail.text = "选择技能节点以查看详情 | 点击插槽选择装配位"
		return
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr == null:
		return
	var snap: Variant = mgr.call("GetTreeSnapshot", _current_roster_id)
	for entry in snap:
		var data: Dictionary = entry
		if str(data.get("id", "")) == _selected_node_id:
			_detail.text = "%s | %s | skill:%s | 装配→%s" % [
				data.get("display_name", "?"),
				data.get("node_type", ""),
				data.get("skill_id", ""),
				_selected_slot_key,
			]
			return


func _on_select_node(node_id: String) -> void:
	_selected_node_id = node_id
	_update_detail()


func _on_unlock_node(node_id: String) -> void:
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr:
		mgr.call("TryUnlockNode", _current_roster_id, node_id)
	refresh()


func _on_skill_slot_pressed(slot_key: String) -> void:
	_selected_slot_key = slot_key
	if not _selected_node_id.is_empty():
		_on_equip_pressed()
	else:
		_update_detail()


func _on_equip_pressed() -> void:
	if _selected_node_id.is_empty():
		return
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr == null:
		return
	var snap: Variant = mgr.call("GetTreeSnapshot", _current_roster_id)
	var node_type := "active"
	for entry in snap:
		var data: Dictionary = entry
		if str(data.get("id", "")) == _selected_node_id:
			node_type = str(data.get("node_type", "active"))
			break
	var slot_key := _selected_slot_key
	if node_type == "passive":
		slot_key = "passive_0"
	elif not slot_key.begins_with("active_"):
		slot_key = "active_0"
	mgr.call("TryEquipSkill", _current_roster_id, _selected_node_id, slot_key)
	var party := get_node_or_null("/root/PartyManager")
	if party:
		party.call("ApplySquadToCombat")
	refresh()
