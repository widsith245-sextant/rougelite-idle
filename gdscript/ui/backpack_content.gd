extends Control

## Backpack: unit selector, stats compare, per-unit equip, shared bag.

const ItemCellScene := preload("res://scenes/ui/components/item_grid_cell.tscn")
const GRID_COLUMNS := 8
const BAG_CAPACITY := 48
const CELL_SIZE := Vector2(48, 48)
const SLOT_TYPES := [
	"Weapon", "Armor", "Helmet", "Gloves", "Boots", "HeadAccessory", "BackAccessory", "Trinket",
]
const SLOT_LABELS := ["武", "甲", "头", "手", "靴", "饰", "背", "奇"]
const ALL_UNIT_IDS := ["ally_a", "ally_b", "ally_c"]
const IDENTIFY_DELAY := 0.55
const REVEAL_WAIT := 0.9

@onready var _unit_selector: OptionButton = %UnitSelector
@onready var _level_label: Label = %LevelLabel
@onready var _exp_bar: ProgressBar = %ExpBar
@onready var _exp_label: Label = %ExpLabel
@onready var _content_tab: TabBar = %ContentTabBar
@onready var _equip_panel: Control = %EquipPanel
@onready var _chest_panel: Control = %ChestPanel
@onready var _skill_panel: Control = %SkillPanel
@onready var _slot_grid: GridContainer = %SlotGrid
@onready var _bag_grid: GridContainer = %BagGrid
@onready var _chest_grid: GridContainer = %ChestGrid
@onready var _skill_lines: VBoxContainer = %SkillLines
@onready var _detail_label: Label = %DetailLabel
@onready var _progress_label: Label = %ProgressLabel
@onready var _equip_button: Button = %EquipButton
@onready var _unequip_button: Button = %UnequipButton
@onready var _identify_button: Button = %IdentifyButton
@onready var _batch_identify_button: Button = %BatchIdentifyButton
@onready var _salvage_button: Button = %SalvageButton
@onready var _unlock_button: Button = %UnlockButton
@onready var _inspect_button: Button = %InspectButton
@onready var _doll_host: Control = %DollHost
@onready var _stats_compare: Control = %StatsCompare

var _active_unit_ids: Array = []
var _selected_unit_id: String = "ally_a"
var _selected_bag_index: int = -1
var _selected_slot_index: int = -1
var _preview_doll: Node2D
var _cells_built := false
var _tooltip_root: PanelContainer
var _tooltip_title: Label
var _tooltip_body: Label
var _batch_identifying := false


func _ready() -> void:
	if _unit_selector:
		_unit_selector.item_selected.connect(_on_unit_selected)
	_content_tab.tab_changed.connect(_on_content_tab_changed)
	_equip_button.pressed.connect(_on_equip_pressed)
	_unequip_button.pressed.connect(_on_unequip_pressed)
	_identify_button.pressed.connect(_on_identify_pressed)
	if _batch_identify_button:
		_batch_identify_button.pressed.connect(_on_batch_identify_pressed)
	_salvage_button.pressed.connect(_on_salvage_pressed)
	if _unlock_button:
		_unlock_button.pressed.connect(_on_unlock_pressed)
	if _inspect_button:
		_inspect_button.pressed.connect(_on_inspect_pressed)
	if _doll_host:
		_doll_host.gui_input.connect(_on_doll_gui_input)
	_connect_event_bus()
	call_deferred("_deferred_init")


func _connect_event_bus() -> void:
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus == null:
		return
	if event_bus.has_signal("LootInventoryChanged"):
		event_bus.connect("LootInventoryChanged", refresh)
	if event_bus.has_signal("EquipmentChanged"):
		event_bus.connect("EquipmentChanged", func(_uid: String) -> void: refresh())
	if event_bus.has_signal("StatsChanged"):
		event_bus.connect("StatsChanged", func(_uid: String) -> void:
			if _stats_compare and _stats_compare.has_method("refresh"):
				_stats_compare.refresh(_selected_unit_id)
		)
	if event_bus.has_signal("SquadChanged"):
		event_bus.connect("SquadChanged", refresh)
	if event_bus.has_signal("RosterLevelChanged"):
		event_bus.connect("RosterLevelChanged", func(_rid: String) -> void: refresh())
	if event_bus.has_signal("SkillsChanged"):
		event_bus.connect("SkillsChanged", func() -> void:
			if _skill_panel and _skill_panel.visible:
				_refresh_skill_tab()
		)


func _refresh_unit_selector() -> void:
	if _unit_selector == null:
		return
	var prev := _selected_unit_id
	_unit_selector.clear()
	_active_unit_ids.clear()
	var party := get_node_or_null("/root/PartyManager")
	for uid in ALL_UNIT_IDS:
		var title := _unit_selector_title(uid, party)
		_unit_selector.add_item(title, _active_unit_ids.size())
		_active_unit_ids.append(uid)
	if _active_unit_ids.is_empty():
		_active_unit_ids.append("ally_a")
		_unit_selector.add_item("先锋", 0)
	var pick := 0
	for i in _active_unit_ids.size():
		if str(_active_unit_ids[i]) == prev:
			pick = i
			break
	_unit_selector.select(pick)
	_selected_unit_id = str(_active_unit_ids[pick])


func _roster_id_for_unit(unit_id: String) -> String:
	var party := get_node_or_null("/root/PartyManager")
	if party and party.has_method("GetRosterIdForUnit"):
		return str(party.call("GetRosterIdForUnit", unit_id))
	return ""


func _unit_selector_title(unit_id: String, party: Node) -> String:
	var name := unit_id
	if party:
		name = str(party.call("GetDisplayNameForUnit", unit_id))
	var roster_id: String = _roster_id_for_unit(unit_id)
	if party and not roster_id.is_empty():
		var bench: Variant = party.call("GetBenchSnapshot")
		if typeof(bench) == TYPE_ARRAY:
			for entry in bench:
				var data: Dictionary = entry
				if str(data.get("roster_id", "")) != roster_id:
					continue
				name = str(data.get("display_name", name))
				var state: String = str(data.get("state", ""))
				if state == "locked":
					name += " [未解锁]"
				elif state == "bench":
					name += " [待命]"
				break
	return name


func _on_unit_selected(index: int) -> void:
	if index < 0 or index >= _active_unit_ids.size():
		return
	_selected_unit_id = str(_active_unit_ids[index])
	_selected_bag_index = -1
	_selected_slot_index = -1
	_setup_preview_doll()
	refresh()


func _deferred_init() -> void:
	if not is_node_ready():
		return
	_setup_item_tooltip()
	_ensure_cells_built()
	_refresh_unit_selector()
	_setup_preview_doll()
	_on_content_tab_changed(_content_tab.current_tab if _content_tab else 0)
	refresh()


func _ensure_cells_built() -> void:
	if _cells_built:
		return
	_build_slot_cells()
	_build_bag_cells()
	_cells_built = true


func _selected_unit() -> String:
	return _selected_unit_id


func refresh() -> void:
	if not is_node_ready():
		call_deferred("refresh")
		return
	_ensure_cells_built()
	_refresh_unit_selector()
	_refresh_equipped_slots()
	_refresh_bag_items()
	_refresh_chest_items()
	_refresh_skill_tab()
	_refresh_roster_exp()
	_refresh_progress()
	if _stats_compare and _stats_compare.has_method("refresh"):
		_stats_compare.refresh(_selected_unit_id)
	if _preview_doll and is_instance_valid(_preview_doll) and _preview_doll.has_method("play_idle"):
		_preview_doll.play_idle()


func _setup_preview_doll() -> void:
	if _doll_host == null:
		return
	for child in _doll_host.get_children():
		child.queue_free()
	_preview_doll = CharacterBase.spawn(_doll_host, _selected_unit_id, 40.0)
	if _preview_doll:
		_preview_doll.position = Vector2(36, 52)
		_preview_doll.scale = Vector2(0.9, 0.9)


func _setup_item_tooltip() -> void:
	_tooltip_root = PanelContainer.new()
	_tooltip_root.visible = false
	_tooltip_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_tooltip_root.z_index = 100
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.09, 0.12, 0.95)
	style.border_color = Color(0.55, 0.45, 0.25, 1)
	style.set_border_width_all(2)
	_tooltip_root.add_theme_stylebox_override("panel", style)
	_tooltip_root.custom_minimum_size = Vector2(180, 120)
	add_child(_tooltip_root)
	var vbox := VBoxContainer.new()
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	vbox.offset_left = 6
	vbox.offset_top = 4
	vbox.offset_right = -6
	vbox.offset_bottom = -4
	_tooltip_root.add_child(vbox)
	_tooltip_title = Label.new()
	_tooltip_title.add_theme_font_size_override("font_size", 11)
	_tooltip_title.add_theme_color_override("font_color", Color(0.95, 0.85, 0.35))
	vbox.add_child(_tooltip_title)
	var sep := HSeparator.new()
	vbox.add_child(sep)
	_tooltip_body = Label.new()
	_tooltip_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_tooltip_body.add_theme_font_size_override("font_size", 9)
	vbox.add_child(_tooltip_body)


func _build_slot_cells() -> void:
	if _slot_grid == null:
		return
	for i in SLOT_LABELS.size():
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = CELL_SIZE
		cell.cell_index = -1 - i
		cell.drag_kind = "equip_slot"
		cell.slot_type_name = SLOT_TYPES[i]
		cell.cell_pressed.connect(_on_slot_cell_pressed)
		cell.cell_hovered.connect(_on_cell_hovered)
		cell.cell_dropped.connect(_on_equip_slot_dropped)
		_slot_grid.add_child(cell)
		cell.setup_slot(SLOT_LABELS[i])


func _build_bag_cells() -> void:
	if _bag_grid == null:
		return
	for i in BAG_CAPACITY:
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = CELL_SIZE
		cell.cell_index = i
		cell.drag_kind = "bag"
		cell.cell_pressed.connect(_on_bag_cell_pressed)
		cell.cell_hovered.connect(_on_cell_hovered)
		cell.cell_dropped.connect(_on_bag_cell_dropped)
		_bag_grid.add_child(cell)


func _quality_label(quality: String) -> String:
	match quality:
		"rare":
			return "稀有"
		"epic":
			return "史诗"
		_:
			return "普通"


func _refresh_equipped_slots() -> void:
	if _slot_grid == null:
		return
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var equipped: Array = loot.call("GetEquippedSnapshot", _selected_unit_id)
	var by_slot: Dictionary = {}
	for entry in equipped:
		var data: Dictionary = entry
		by_slot[str(data.get("equipped_slot", ""))] = data
	for i in _slot_grid.get_child_count():
		var cell: PanelContainer = _slot_grid.get_child(i)
		var slot_name: String = SLOT_TYPES[i] if i < SLOT_TYPES.size() else ""
		if by_slot.has(slot_name):
			var data: Dictionary = by_slot[slot_name]
			cell.setup_item(
				str(data.get("display_name", "?")),
				int(data.get("item_level", 0)),
				1,
				str(data.get("quality", "common"))
			)
		else:
			cell.setup_slot(SLOT_LABELS[i])


func _refresh_bag_items() -> void:
	if _bag_grid == null:
		return
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var items: Array = loot.call("GetIdentifiedItemsSnapshot")
	for i in _bag_grid.get_child_count():
		var cell: PanelContainer = _bag_grid.get_child(i)
		if i < items.size():
			var data: Dictionary = items[i]
			cell.setup_item(
				str(data.get("display_name", "?")),
				int(data.get("item_level", 0)),
				1,
				str(data.get("quality", "common"))
			)
		else:
			cell.clear_cell()


func _refresh_chest_items() -> void:
	if _chest_grid == null:
		return
	for child in _chest_grid.get_children():
		child.queue_free()
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var chests: Array = loot.call("GetUnidentifiedChestsSnapshot")
	if chests.is_empty():
		var hint := Label.new()
		hint.text = "暂无宝箱（战斗掉落会先入左上气泡）"
		hint.add_theme_font_size_override("font_size", 10)
		hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_chest_grid.add_child(hint)
		return
	for entry in chests:
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = CELL_SIZE
		_chest_grid.add_child(cell)
		cell.setup_chest(str(entry.get("display_name", "宝箱")), str(entry.get("quality", "common")))


func _refresh_skill_tab() -> void:
	if _skill_lines == null:
		return
	for child in _skill_lines.get_children():
		child.queue_free()

	var roster_id := _roster_id_for_unit(_selected_unit_id)
	var party := get_node_or_null("/root/PartyManager")
	var display_name := ClassSkillsLoader.display_name_for_unit(_selected_unit_id)
	if party:
		display_name = str(party.call("GetDisplayNameForUnit", _selected_unit_id))

	var header := Label.new()
	header.text = "%s — 技能" % display_name
	header.add_theme_font_size_override("font_size", 11)
	_skill_lines.add_child(header)

	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", 8)
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_skill_lines.add_child(body)

	var equipped_box := VBoxContainer.new()
	equipped_box.custom_minimum_size = Vector2(100, 0)
	equipped_box.add_theme_constant_override("separation", 2)
	body.add_child(equipped_box)
	var eq_title := Label.new()
	eq_title.text = "已装配"
	eq_title.add_theme_font_size_override("font_size", 10)
	equipped_box.add_child(eq_title)

	var mgr := get_node_or_null("/root/CharacterSkillManager")
	var db := get_node_or_null("/root/DbManager")
	var max_active := 1
	if db:
		max_active = int(db.get("MaxActiveSkillSlots"))
	var equipped: Dictionary = {}
	if mgr and not roster_id.is_empty():
		var es: Variant = mgr.call("GetEquippedSnapshot", roster_id)
		if typeof(es) == TYPE_DICTIONARY:
			equipped = es
	for i in max_active:
		var sid: String = str(equipped.get("active_%d" % i, "—"))
		var lbl := Label.new()
		lbl.text = "主动%d: %s" % [i + 1, sid]
		lbl.add_theme_font_size_override("font_size", 9)
		equipped_box.add_child(lbl)
	var pass_sid: String = str(equipped.get("passive_0", "—"))
	var pass_l := Label.new()
	pass_l.text = "被动: %s" % pass_sid
	pass_l.add_theme_font_size_override("font_size", 9)
	equipped_box.add_child(pass_l)

	var tree_box := VBoxContainer.new()
	tree_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tree_box.add_theme_constant_override("separation", 2)
	body.add_child(tree_box)
	var tree_title := Label.new()
	tree_title.text = "技能树"
	tree_title.add_theme_font_size_override("font_size", 10)
	tree_box.add_child(tree_title)

	if mgr == null or roster_id.is_empty():
		var hint := Label.new()
		hint.text = "无技能树数据"
		hint.add_theme_font_size_override("font_size", 9)
		tree_box.add_child(hint)
	else:
		var snap: Variant = mgr.call("GetTreeSnapshot", roster_id)
		if typeof(snap) == TYPE_ARRAY:
			for entry in snap:
				var data: Dictionary = entry
				var req_lv: int = int(data.get("required_level", 1))
				var prefix := "✓" if bool(data.get("unlocked", false)) else ("○" if bool(data.get("can_unlock", false)) else "🔒")
				var line := Label.new()
				line.text = "%s Lv%d %s" % [prefix, req_lv, str(data.get("display_name", "?"))]
				line.add_theme_font_size_override("font_size", 9)
				if bool(data.get("can_unlock", false)):
					var node_id: String = str(data.get("id", ""))
					line.mouse_filter = Control.MOUSE_FILTER_STOP
					line.gui_input.connect(func(event: InputEvent) -> void:
						if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
							mgr.call("TryUnlockNode", roster_id, node_id)
							refresh()
					)
				tree_box.add_child(line)

	var open_btn := Button.new()
	open_btn.text = "打开完整技能树 →"
	open_btn.add_theme_font_size_override("font_size", 9)
	open_btn.pressed.connect(_open_skill_popup)
	_skill_lines.add_child(open_btn)

	for line in ClassSkillsLoader.build_skill_lines(_selected_unit_id):
		var detail := Label.new()
		detail.text = line
		detail.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		detail.add_theme_font_size_override("font_size", 8)
		detail.modulate = Color(0.75, 0.78, 0.85)
		_skill_lines.add_child(detail)


func _open_skill_popup() -> void:
	var popup_mgr := get_node_or_null("/root/GameRoot/PopupManager")
	if popup_mgr and popup_mgr.has_method("open_popup"):
		popup_mgr.call("open_popup", 2)


func _refresh_roster_exp() -> void:
	var roster_id := _roster_id_for_unit(_selected_unit_id)
	var roster_prog := get_node_or_null("/root/RosterProgressionManager")
	if roster_id.is_empty() or roster_prog == null:
		if _level_label:
			_level_label.text = "Lv—"
		if _exp_bar:
			_exp_bar.value = 0
		if _exp_label:
			_exp_label.text = "— / — EXP"
		return
	var snap: Variant = roster_prog.call("GetExpSnapshot", roster_id)
	if typeof(snap) != TYPE_DICTIONARY:
		return
	var data: Dictionary = snap
	var lv: int = int(data.get("level", 1))
	var exp_v: float = float(data.get("exp", 0))
	var req: float = float(data.get("exp_required", 100))
	if _level_label:
		_level_label.text = "Lv%d" % lv
	if _exp_bar:
		_exp_bar.max_value = maxf(req, 1.0)
		_exp_bar.value = exp_v
	if _exp_label:
		_exp_label.text = "%.0f / %.0f EXP" % [exp_v, req]


func _refresh_progress() -> void:
	if _progress_label == null:
		return
	var prog := get_node_or_null("/root/ProgressionManager")
	var gold := 0
	if prog:
		var snap: Variant = prog.call("GetHudSnapshot")
		if typeof(snap) == TYPE_DICTIONARY:
			gold = int(snap.get("gold", 0))
	var loot := get_node_or_null("/root/LootManager")
	var chest_n := 0
	var item_n := 0
	if loot:
		chest_n = loot.call("GetUnidentifiedCount")
		item_n = loot.call("GetIdentifiedCount")
	_progress_label.text = "金%d | 箱%d 装%d/%d" % [gold, chest_n, item_n, BAG_CAPACITY]


func _set_detail(text: String) -> void:
	if _detail_label:
		_detail_label.text = text


func _on_content_tab_changed(tab_index: int) -> void:
	if _equip_panel:
		_equip_panel.visible = tab_index == 0
	if _chest_panel:
		_chest_panel.visible = tab_index == 1
	if _skill_panel:
		_skill_panel.visible = tab_index == 2


func _on_bag_cell_pressed(index: int) -> void:
	_selected_bag_index = index
	_selected_slot_index = -1
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var items: Array = loot.call("GetIdentifiedItemsSnapshot")
	if index < 0 or index >= items.size():
		_set_detail("空格子")
		return
	var data: Dictionary = items[index]
	_set_detail("[%s] %s | iLvl %d | 白值 %.1f | %s" % [
		_quality_label(str(data.get("quality", "common"))),
		data.get("display_name", "?"),
		data.get("item_level", 0),
		data.get("rolled_base_stat", 0.0),
		data.get("slot", ""),
	])


func _on_slot_cell_pressed(index: int) -> void:
	_selected_slot_index = -1 - index
	_selected_bag_index = -1
	var slot_i: int = (-1 - index)
	if slot_i < 0 or slot_i >= SLOT_TYPES.size():
		return
	var slot_name: String = SLOT_TYPES[slot_i]
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var equipped: Array = loot.call("GetEquippedSnapshot", _selected_unit_id)
	for entry in equipped:
		var data: Dictionary = entry
		if str(data.get("equipped_slot", "")) == slot_name:
			_set_detail("已装备: %s (点击「卸下」)" % data.get("display_name", "?"))
			return
	_set_detail("部位 %s 空" % SLOT_LABELS[slot_i])


func _on_equip_pressed() -> void:
	if _selected_bag_index < 0:
		_set_detail("请先选择背包中的装备")
		return
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var ok: bool = loot.call("EquipByBagIndex", _selected_bag_index, _selected_unit_id)
	if ok:
		_selected_bag_index = -1
	_set_detail("已穿上" if ok else "无法穿上（部位冲突或无效）")
	_request_save()
	refresh()


func _on_unequip_pressed() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	if _selected_slot_index >= 0:
		var slot_i: int = _selected_slot_index
		if slot_i < SLOT_TYPES.size():
			loot.call("UnequipBySlotName", SLOT_TYPES[slot_i], _selected_unit_id)
			_set_detail("已卸下 %s" % SLOT_LABELS[slot_i])
			refresh()
			return
	if _selected_bag_index >= 0:
		_set_detail("选中背包格 — 请点左侧部位槽再卸下")
		return
	_set_detail("请先点击左侧装备槽")


func _on_identify_pressed() -> void:
	if _batch_identifying:
		return
	_identify_one()


func _on_batch_identify_pressed() -> void:
	if _batch_identifying:
		return
	_run_batch_identify()


func _run_batch_identify() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var count: int = int(loot.call("GetUnidentifiedCount"))
	if count <= 0:
		_set_detail("没有待鉴定的宝箱")
		return

	var log_mgr := get_node_or_null("/root/LogWindowManager")
	if log_mgr and log_mgr.has_method("Show"):
		log_mgr.call("Show")

	_batch_identifying = true
	_set_identify_buttons_enabled(false)
	_set_detail("批量鉴定中… 0/%d" % count)

	var done := 0
	while true:
		if not is_inside_tree():
			break
		var remaining: int = int(loot.call("GetUnidentifiedCount"))
		if remaining <= 0:
			break
		var result: Variant = loot.call("IdentifyNextAsDictionary", _get_identify_stage_level())
		if result == null:
			break
		var data: Dictionary = result
		done += 1
		_set_detail("批量鉴定 %d · [%s] %s" % [
			done,
			_quality_label(str(data.get("quality", "common"))),
			data.get("display_name", "?"),
		])
		await _play_identify_reveal(data)
		await get_tree().create_timer(_get_identify_delay()).timeout

	_batch_identifying = false
	_set_identify_buttons_enabled(true)
	_set_detail("批量鉴定完成 · 共 %d 件" % done)
	refresh()
	if _content_tab and _content_tab.current_tab == 1:
		_content_tab.current_tab = 0
		_on_content_tab_changed(0)


func _identify_one() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var result: Variant = loot.call("IdentifyNextAsDictionary", _get_identify_stage_level())
	if result == null:
		_set_detail("没有待鉴定的宝箱")
	else:
		var data: Dictionary = result
		_set_detail("鉴定获得: [%s] %s (iLvl %d)" % [
			_quality_label(str(data.get("quality", "common"))),
			data.get("display_name", "?"),
			data.get("item_level", 0),
		])
		await _play_identify_reveal(data)
	refresh()
	if _content_tab and _content_tab.current_tab == 1:
		_content_tab.current_tab = 0
		_on_content_tab_changed(0)


func _get_identify_stage_level() -> int:
	var roster := get_node_or_null("/root/RosterProgressionManager")
	var party := get_node_or_null("/root/PartyManager")
	if roster and party and roster.has_method("GetAverageActiveRosterLevel"):
		return int(roster.call("GetAverageActiveRosterLevel", party))
	return 1


func _set_identify_buttons_enabled(enabled: bool) -> void:
	if _identify_button:
		_identify_button.disabled = not enabled
	if _batch_identify_button:
		_batch_identify_button.disabled = not enabled


func _get_identify_delay() -> float:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("GetIdentifyIntervalMultiplier"):
		return IDENTIFY_DELAY * float(settings.call("GetIdentifyIntervalMultiplier"))
	return IDENTIFY_DELAY


func _play_identify_reveal(data: Dictionary):
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("GetIdentifyRevealEnabled"):
		if not bool(settings.call("GetIdentifyRevealEnabled")):
			return
	var overlay := get_tree().root.get_node_or_null("GameRoot/ChestRevealOverlay")
	if overlay == null or not overlay.has_method("play_reveal"):
		await get_tree().create_timer(REVEAL_WAIT).timeout
		return
	if overlay.has_signal("reveal_finished"):
		var finished := overlay.reveal_finished
		overlay.call("play_reveal", data)
		await finished
	else:
		overlay.call("play_reveal", data)
		await get_tree().create_timer(REVEAL_WAIT).timeout


func _on_cell_hovered(index: int, entered: bool) -> void:
	if _tooltip_root == null:
		return
	if not entered:
		_tooltip_root.visible = false
		return
	var data := _item_data_for_cell(index)
	if data.is_empty():
		_tooltip_root.visible = false
		return
	_tooltip_title.text = str(data.get("display_name", "?"))
	var lines: PackedStringArray = PackedStringArray()
	lines.append("品质: %s | iLvl %d" % [_quality_label(str(data.get("quality", "common"))), int(data.get("item_level", 0))])
	lines.append("部位: %s | 白值 %.1f" % [data.get("slot", ""), float(data.get("rolled_base_stat", 0))])
	lines.append("【固有属性】")
	for affix in data.get("affixes", []):
		if typeof(affix) == TYPE_DICTIONARY:
			var a: Dictionary = affix
			lines.append("• %s +%.1f" % [a.get("display_name", a.get("id", "?")), float(a.get("value", 0))])
	lines.append("【装饰槽】")
	lines.append("• 空槽 (可装饰)")
	_tooltip_body.text = "\n".join(lines)
	_tooltip_root.visible = true
	_tooltip_root.global_position = get_global_mouse_position() + Vector2(12, 8)


func _item_data_for_cell(index: int) -> Dictionary:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return {}
	if index >= 0:
		var items: Array = loot.call("GetIdentifiedItemsSnapshot")
		if index < items.size():
			return items[index]
		return {}
	var slot_i: int = (-1 - index)
	if slot_i < 0 or slot_i >= SLOT_TYPES.size():
		return {}
	var equipped: Array = loot.call("GetEquippedSnapshot", _selected_unit_id)
	for entry in equipped:
		var data: Dictionary = entry
		if str(data.get("equipped_slot", "")) == SLOT_TYPES[slot_i]:
			return data
	return {}


func _on_equip_slot_dropped(slot_index: int, payload: Dictionary) -> void:
	var kind: String = str(payload.get("drag_kind", ""))
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var slot_i: int = (-1 - slot_index)
	if slot_i < 0 or slot_i >= SLOT_TYPES.size():
		return
	if kind == "bag":
		var bag_i: int = int(payload.get("cell_index", -1))
		if bag_i >= 0:
			loot.call("EquipByBagIndex", bag_i, _selected_unit_id)
			_set_detail("已拖拽穿上")
			refresh()
	elif kind == "equip_slot":
		_set_detail("同槽位无需交换")


func _on_bag_cell_dropped(bag_index: int, payload: Dictionary) -> void:
	var kind: String = str(payload.get("drag_kind", ""))
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	if kind == "equip_slot":
		var slot_i: int = (-1 - int(payload.get("cell_index", 0)))
		if slot_i >= 0 and slot_i < SLOT_TYPES.size():
			loot.call("UnequipBySlotName", SLOT_TYPES[slot_i], _selected_unit_id)
			_set_detail("已拖拽卸下")
			refresh()


func _request_save() -> void:
	var bootstrap := get_node_or_null("/root/SaveBootstrap")
	if bootstrap and bootstrap.has_method("RequestSave"):
		bootstrap.call("RequestSave")


func _on_salvage_pressed() -> void:
	if _selected_bag_index < 0:
		_set_detail("请先选择要分解的装备")
		return
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	loot.call("SalvageByIndex", _selected_bag_index)
	_selected_bag_index = -1
	_set_detail("已分解选中装备")
	refresh()


func _on_unlock_pressed() -> void:
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		_set_detail("队伍系统未就绪")
		return
	var result: Variant = party.call("TryDirectUnlockForUnit", _selected_unit_id)
	if typeof(result) != TYPE_DICTIONARY:
		_set_detail("解锁失败")
		return
	var data: Dictionary = result
	_set_detail(str(data.get("message", "解锁完成")))
	if bool(data.get("success", false)):
		refresh()


func _on_inspect_pressed() -> void:
	var popup_mgr := get_node_or_null("/root/GameRoot/PopupManager")
	if popup_mgr == null:
		popup_mgr = get_node_or_null("/root/PopupManager")
	if popup_mgr and popup_mgr.has_method("open_character_stats"):
		popup_mgr.call("open_character_stats", _selected_unit_id)
	else:
		_set_detail("无法打开详细属性")


func _on_doll_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed and mb.button_index == MOUSE_BUTTON_LEFT:
			_show_portrait()


func _show_portrait() -> void:
	var mgr := get_node_or_null("/root/PortraitWindowManager")
	if mgr and mgr.has_method("ShowPortrait"):
		mgr.call("ShowPortrait", _selected_unit_id, false)
	else:
		_set_detail("立绘预览未加载（占位）")
