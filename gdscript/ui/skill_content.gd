extends Control

## Per-character skill tree + slot equip.

const SkillTreeCellScript := preload("res://gdscript/ui/skill_tree_cell.gd")
const SkillSlotPanelScript := preload("res://gdscript/ui/skill_slot_panel.gd")
const EffectGlossaryLoaderScript := preload("res://gdscript/ui/effect_glossary_loader.gd")
const EffectTooltipPopupScript := preload("res://gdscript/ui/effect_tooltip_popup.gd")

@onready var _roster_tab: TabBar = %RosterTabBar
@onready var _tree_grid: GridContainer = %TreeGrid
@onready var _slot_box: VBoxContainer = %SlotBox
@onready var _detail: RichTextLabel = %DetailLabel
@onready var _equip_btn: Button = %EquipButton

var _selected_node_id: String = ""
var _current_roster_id: String = "vanguard_a"
var _selected_slot_key: String = "active_0"
var _tab_roster_ids: Array = []
var _slot_panel: VBoxContainer
var _hover_effect_id: String = ""
var _hover_pile: int = 1
var _hover_intensity: float = 0.0


func _ready() -> void:
	if _roster_tab:
		_roster_tab.tab_changed.connect(_on_roster_tab_changed)
	if _equip_btn:
		_equip_btn.pressed.connect(_on_equip_pressed)
	if _detail:
		_detail.meta_clicked.connect(_on_detail_meta_clicked)
		_detail.meta_hover_started.connect(_on_detail_meta_hover_started)
		_detail.meta_hover_ended.connect(_on_detail_meta_hover_ended)
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
		var title := str(rid)
		for entry in snap:
			var data: Dictionary = entry
			if str(data.get("roster_id", "")) == rid:
				title = str(data.get("display_name", title))
				break
		if title.length() > 6:
			title = title.substr(0, 6)
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

	var active_nodes: Array = []
	var passive_nodes: Array = []
	for entry in snap:
		var data: Dictionary = entry
		if str(data.get("node_type", "")) == "passive":
			passive_nodes.append(data)
		else:
			active_nodes.append(data)

	_add_tree_section("主动节点")
	for data in active_nodes:
		_add_tree_button(data)
	_add_tree_section("被动节点")
	for data in passive_nodes:
		_add_tree_button(data)


func _add_tree_section(title: String) -> void:
	var lbl := Label.new()
	lbl.text = title
	lbl.add_theme_font_size_override("font_size", 10)
	lbl.modulate = Color(0.75, 0.8, 0.88)
	_tree_grid.add_child(lbl)


func _add_tree_button(data: Dictionary) -> void:
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
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr and mgr.has_method("GetSkillDisplaySnapshot"):
		var display: Dictionary = mgr.call("GetSkillDisplaySnapshot", _current_roster_id, node_id)
		btn.set_effect_badges(display.get("applied_effects", []))
	_tree_grid.add_child(btn)


func _refresh_slots() -> void:
	if _slot_box == null:
		return
	for child in _slot_box.get_children():
		child.queue_free()

	_slot_panel = SkillSlotPanelScript.new()
	_slot_panel.cell_size = Vector2(52, 36)
	_slot_panel.slot_pressed.connect(_on_skill_slot_pressed)
	_slot_panel.slot_dropped.connect(_on_skill_slot_dropped)
	_slot_box.add_child(_slot_panel)
	_slot_panel.setup(_current_roster_id, true)


func _update_detail() -> void:
	if _detail == null:
		return
	if _selected_node_id.is_empty():
		_detail.text = "选择技能节点后点击插槽或「装配到槽位」"
		return
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr == null or not mgr.has_method("GetSkillDisplaySnapshot"):
		return
	var snap: Dictionary = mgr.call("GetSkillDisplaySnapshot", _current_roster_id, _selected_node_id)
	var body := EffectGlossaryLoaderScript.build_skill_detail_bbcode(snap)
	_detail.text = "%s\n装配目标：%s" % [body, _selected_slot_key]


func _on_detail_meta_clicked(meta: Variant) -> void:
	pass


func _on_detail_meta_hover_started(meta: Variant) -> void:
	var effect_id := str(meta)
	if effect_id.is_empty():
		return
	_hover_effect_id = effect_id
	var mgr := get_node_or_null("/root/CharacterSkillManager")
	if mgr and mgr.has_method("GetSkillDisplaySnapshot"):
		var snap: Dictionary = mgr.call("GetSkillDisplaySnapshot", _current_roster_id, _selected_node_id)
		for effect in snap.get("applied_effects", []):
			if effect is Dictionary and str(effect.get("effect_id", "")) == effect_id:
				_hover_pile = int(effect.get("pile", 1))
				_hover_intensity = float(effect.get("intensity", 0.0))
				break
	EffectTooltipPopupScript.show_for(effect_id, _hover_pile, _hover_intensity, _detail.get_global_mouse_position())


func _on_detail_meta_hover_ended(_meta: Variant) -> void:
	EffectTooltipPopupScript.hide_popup()


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
		if not slot_key.begins_with("passive_"):
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
