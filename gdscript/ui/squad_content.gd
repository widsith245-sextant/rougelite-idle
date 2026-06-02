extends Control

## Squad management popup — same density as backpack.

const ItemCellScene := preload("res://scenes/ui/components/item_grid_cell.tscn")
const RosterDragScript := preload("res://gdscript/ui/roster_drag_source.gd")
const CELL_SIZE := Vector2(48, 48)
const SLOT_CELL_SIZE := Vector2(64, 64)

@onready var _slot_row: HBoxContainer = %SlotRow
@onready var _bench_grid: GridContainer = %BenchGrid
@onready var _roster_tab: TabBar = %RosterTabBar
@onready var _doll_host: Control = %DollHost
@onready var _stats_label: Label = %StatsLabel
@onready var _footer: Label = %FooterLabel
@onready var _apply_button: Button = %ApplyButton

var _selected_roster_id: String = ""
var _selected_slot_index: int = -1
var _preview_doll: Node2D


func _ready() -> void:
	if _roster_tab:
		_roster_tab.tab_changed.connect(_on_roster_tab_changed)
	if _apply_button:
		_apply_button.pressed.connect(_on_apply_pressed)
	if _doll_host and _doll_host.get_script() == null:
		_doll_host.set_script(RosterDragScript)
	call_deferred("refresh")


func refresh() -> void:
	_build_slot_row()
	_refresh_bench()
	_refresh_footer()
	if _selected_roster_id.is_empty():
		_select_first_roster()


func _build_slot_row() -> void:
	if _slot_row == null:
		return
	for child in _slot_row.get_children():
		child.queue_free()
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var snap: Variant = party.call("GetActiveSquadSnapshot")
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var slot_i: int = int(data.get("slot_index", 0))
		var unlocked: bool = bool(data.get("slot_unlocked", false))
		var filled: bool = bool(data.get("filled", false))
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = SLOT_CELL_SIZE
		cell.cell_index = slot_i
		cell.drag_kind = "squad_slot"
		cell.cell_pressed.connect(_on_slot_cell_pressed)
		cell.cell_dropped.connect(_on_squad_slot_dropped)
		_slot_row.add_child(cell)
		if not unlocked:
			cell.setup_slot("🔒")
		elif filled:
			var rid: String = str(data.get("roster_id", ""))
			cell.roster_id = rid
			cell.setup_roster(str(data.get("display_name", "?")), rid)
		else:
			cell.setup_slot("空")


func _refresh_bench() -> void:
	if _bench_grid == null:
		return
	for child in _bench_grid.get_children():
		child.queue_free()
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var show_locked := _roster_tab != null and _roster_tab.current_tab == 1
	var snap: Variant = party.call("GetBenchSnapshot")
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var state: String = str(data.get("state", "locked"))
		if show_locked:
			if state != "locked":
				continue
		elif state != "bench":
			continue
		var cell: PanelContainer = ItemCellScene.instantiate()
		cell.cell_size = CELL_SIZE
		var roster_id: String = str(data.get("roster_id", ""))
		cell.drag_kind = "bench"
		cell.roster_id = roster_id
		cell.cell_pressed.connect(_on_bench_cell_pressed.bind(roster_id))
		cell.cell_dropped.connect(_on_squad_slot_dropped)
		_bench_grid.add_child(cell)
		if state == "locked":
			cell.setup_slot("锁")
		else:
			cell.setup_roster(str(data.get("display_name", "?")), roster_id)


func _refresh_footer() -> void:
	if _footer == null:
		return
	var party := get_node_or_null("/root/PartyManager")
	var active_n := 0
	var max_n := 1
	if party:
		max_n = int(party.get("MaxActiveSlots"))
		var snap: Variant = party.call("GetActiveSquadSnapshot")
		if typeof(snap) == TYPE_ARRAY:
			for entry in snap:
				var data: Dictionary = entry
				if bool(data.get("filled", false)) and bool(data.get("slot_unlocked", false)):
					active_n += 1
	_footer.text = "编组 %d/%d | 拖拽头像上阵/换位" % [active_n, max_n]


func _select_first_roster() -> void:
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var snap: Variant = party.call("GetActiveSquadSnapshot")
	if typeof(snap) == TYPE_ARRAY:
		for entry in snap:
			var data: Dictionary = entry
			if bool(data.get("filled", false)):
				_show_roster(str(data.get("roster_id", "")), str(data.get("unit_id", "ally_a")))
				return
	var bench: Variant = party.call("GetBenchSnapshot")
	for entry in bench:
		var data: Dictionary = entry
		if str(data.get("state", "")) == "bench":
			_show_roster(str(data.get("roster_id", "")), "ally_a")
			return


func _show_roster(roster_id: String, unit_id: String) -> void:
	_selected_roster_id = roster_id
	if _doll_host:
		for child in _doll_host.get_children():
			child.queue_free()
		_preview_doll = CharacterBase.spawn(_doll_host, unit_id, 40.0)
		if _preview_doll:
			_preview_doll.position = Vector2(36, 52)
			_preview_doll.scale = Vector2(0.9, 0.9)
		if _doll_host.get_script() == RosterDragScript:
			_doll_host.set("roster_id", roster_id)
	_refresh_stats_summary(unit_id)


func _refresh_stats_summary(unit_id: String) -> void:
	if _stats_label == null:
		return
	var stats := get_node_or_null("/root/StatsService")
	var roster_prog := get_node_or_null("/root/RosterProgressionManager")
	var lv := 1
	if roster_prog and not _selected_roster_id.is_empty():
		lv = int(roster_prog.call("GetLevel", _selected_roster_id))
	if stats == null:
		_stats_label.text = "Lv%d" % lv
		return
	var snap: Variant = stats.call("GetSnapshot", unit_id)
	if typeof(snap) != TYPE_DICTIONARY:
		return
	var data: Dictionary = snap
	_stats_label.text = "Lv%d | HP %.0f | 攻 %.0f | 速 %.0f" % [
		lv,
		float(data.get("max_hp", 0)),
		float(data.get("damage", 0)),
		float(data.get("move_speed", 0)),
	]


func _on_roster_tab_changed(_tab: int) -> void:
	refresh()


func _on_slot_cell_pressed(slot_index: int) -> void:
	_selected_slot_index = slot_index
	if _selected_roster_id.is_empty():
		return
	_assign_roster(slot_index, _selected_roster_id)


func _on_bench_cell_pressed(roster_id: String) -> void:
	_selected_roster_id = roster_id
	var party := get_node_or_null("/root/PartyManager")
	var unit_id := "ally_a"
	if party:
		var snap: Variant = party.call("GetActiveSquadSnapshot")
		for entry in snap:
			var data: Dictionary = entry
			if str(data.get("roster_id", "")) == roster_id:
				unit_id = str(data.get("unit_id", "ally_a"))
				break
	_show_roster(roster_id, unit_id)
	if _selected_slot_index >= 0 and party:
		_assign_roster(_selected_slot_index, roster_id)


func _assign_roster(slot_index: int, roster_id: String) -> void:
	var party := get_node_or_null("/root/PartyManager")
	if party == null or roster_id.is_empty():
		return
	party.call("AssignRosterToSlot", slot_index, roster_id)
	party.call("ApplySquadToCombat")
	_request_save()
	refresh()


func _on_apply_pressed() -> void:
	var party := get_node_or_null("/root/PartyManager")
	if party:
		party.call("ApplySquadToCombat")
	_request_save()


func _on_squad_slot_dropped(slot_index: int, payload: Dictionary) -> void:
	if str(payload.get("drag_kind", "")) != "roster":
		return
	var roster_id: String = str(payload.get("roster_id", ""))
	if roster_id.is_empty():
		return
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var source: String = str(payload.get("source", ""))
	var from_slot: int = int(payload.get("cell_index", -1))
	if source == "squad_slot" and from_slot >= 0 and from_slot != slot_index:
		party.call("SwapSquadSlots", from_slot, slot_index)
	else:
		party.call("AssignRosterToSlot", slot_index, roster_id)
	party.call("ApplySquadToCombat")
	_request_save()
	refresh()


func _request_save() -> void:
	var bootstrap := get_node_or_null("/root/SaveBootstrap")
	if bootstrap and bootstrap.has_method("RequestSave"):
		bootstrap.call("RequestSave")
