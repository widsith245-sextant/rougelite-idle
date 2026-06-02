extends Control

## Per-character skill tree + slot equip.

const ItemCellScene := preload("res://scenes/ui/components/item_grid_cell.tscn")
const SkillTreeCellScript := preload("res://gdscript/ui/skill_tree_cell.gd")

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
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("SkillsChanged"):
		event_bus.connect("SkillsChanged", refresh)
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
		var btn: Button = SkillTreeCellScript.new()
		btn.custom_minimum_size = Vector2(72, 36)
		var prefix := "主" if str(data.get("node_type", "")) == "active" else "被"
		var req_lv: int = int(data.get("required_level", 1))
		btn.text = "%s Lv%d %s" % [prefix, req_lv, str(data.get("display_name", "?"))]
		btn.roster_id = _current_roster_id
		btn.node_id = node_id
		btn.node_type = str(data.get("node_type", "active"))
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
		_add_skill_slot(slot_row, slot_key, equipped, mgr, "主%d" % (i + 1))

	for i in range(max_active, 2):
		var lock_l := Label.new()
		lock_l.text = "主动%d: 🔒" % [i + 1]
		lock_l.add_theme_font_size_override("font_size", 10)
		_slot_box.add_child(lock_l)

	var pass_row := HBoxContainer.new()
	_slot_box.add_child(pass_row)
	if passive_ok:
		_add_skill_slot(pass_row, "passive_0", equipped, mgr, "被")
	else:
		var pass_cell: PanelContainer = ItemCellScene.instantiate()
		pass_cell.cell_size = Vector2(56, 40)
		pass_cell.setup_slot("🔒")
		pass_row.add_child(pass_cell)


func _add_skill_slot(parent: Node, slot_key: String, equipped: Dictionary, mgr: Node, empty_label: String) -> void:
	var cell: PanelContainer = ItemCellScene.instantiate()
	cell.cell_size = Vector2(56, 40)
	cell.drag_kind = "skill_slot"
	cell.set_meta("slot_key", slot_key)
	cell.set_meta("roster_id", _current_roster_id)
	cell.cell_pressed.connect(_on_skill_slot_pressed.bind(slot_key))
	cell.cell_dropped.connect(_on_skill_slot_dropped.bind(slot_key))
	parent.add_child(cell)
	var sid: String = str(equipped.get(slot_key, ""))
	if sid.is_empty() or sid == "—":
		cell.setup_slot(empty_label)
		return
	cell.setup_item(sid.substr(0, mini(4, sid.length())), 0, 1, "rare")
	if mgr and mgr.has_method("FindNodeIdForSkill"):
		var node_id: Variant = mgr.call("FindNodeIdForSkill", _current_roster_id, sid)
		if node_id != null and str(node_id) != "":
			cell.set_meta("node_id", str(node_id))


func _update_detail() -> void:
	if _detail == null:
		return
	if _selected_node_id.is_empty():
		_detail.text = "选择技能节点以查看详情 | 拖拽或点击插槽装配"
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


func _on_skill_slot_dropped(slot_key: String, _target_index: int, payload: Dictionary) -> void:
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr == null:
		return
	var kind: String = str(payload.get("drag_kind", ""))
	if kind == "skill_node":
		var node_id: String = str(payload.get("node_id", ""))
		if node_id.is_empty():
			return
		_selected_node_id = node_id
		_equip_to_slot(slot_key)
	elif kind == "skill_slot":
		var src_slot: String = str(payload.get("slot_key", ""))
		if src_slot.is_empty() or src_slot == slot_key:
			return
		var ok: bool = mgr.call("TrySwapEquipped", _current_roster_id, src_slot, slot_key)
		if not ok and _detail:
			var err := str(mgr.call("GetLastEquipError")) if mgr.has_method("GetLastEquipError") else ""
			_detail.text = "交换失败：%s" % err
		else:
			var party := get_node_or_null("/root/PartyManager")
			if party:
				party.call("ApplySquadToCombat")
			refresh()


func _on_equip_pressed() -> void:
	if _selected_node_id.is_empty():
		return
	_equip_to_slot(_selected_slot_key)


func _equip_to_slot(slot_key: String) -> void:
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
	if node_type == "passive":
		slot_key = "passive_0"
	elif not slot_key.begins_with("active_"):
		slot_key = "active_0"
	var ok: bool = mgr.call("TryEquipSkill", _current_roster_id, _selected_node_id, slot_key)
	if not ok:
		var err := ""
		if mgr.has_method("GetLastEquipError"):
			err = str(mgr.call("GetLastEquipError"))
		if _detail:
			_detail.text = "装配失败：%s" % (err if not err.is_empty() else "未知原因")
		return
	var party := get_node_or_null("/root/PartyManager")
	if party:
		party.call("ApplySquadToCombat")
	refresh()
