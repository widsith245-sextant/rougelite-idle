extends Node

enum PopupId {
	BACKPACK,
	SQUAD,
	SKILL,
	CULTIVATION,
	WONDERLAND,
	STAR_CHART = CULTIVATION,
	CHARACTER_STATS = 5,
	STAGE_SELECT = 6,
	RUN_CARD_PICK = 7,
	SETTINGS = 8,
	RUN_RELIC_PICK = 9,
}

const RUN_CARD_PICK_SIZE := Vector2i(640, 320)
const RUN_RELIC_PICK_SIZE := Vector2i(640, 320)

const POPUP_TITLES := {
	PopupId.BACKPACK: "背包",
	PopupId.SQUAD: "编队",
	PopupId.SKILL: "技能",
	PopupId.CULTIVATION: "养成",
	PopupId.WONDERLAND: "奇境",
	PopupId.CHARACTER_STATS: "详细属性",
	PopupId.STAGE_SELECT: "关卡",
	PopupId.RUN_CARD_PICK: "选择增益卡牌",
	PopupId.SETTINGS: "设置",
	PopupId.RUN_RELIC_PICK: "选择遗物",
}

const CONTENT_SCENES := {
	PopupId.BACKPACK: preload("res://scenes/ui/popup/content/backpack_content.tscn"),
	PopupId.SQUAD: preload("res://scenes/ui/popup/content/squad_content.tscn"),
	PopupId.SKILL: preload("res://scenes/ui/popup/content/skill_content.tscn"),
	PopupId.CULTIVATION: preload("res://scenes/ui/popup/content/cultivation_content.tscn"),
	PopupId.WONDERLAND: preload("res://scenes/ui/popup/content/wonderland_content.tscn"),
	PopupId.CHARACTER_STATS: preload("res://scenes/ui/popup/content/detailed_stats_content.tscn"),
	PopupId.STAGE_SELECT: preload("res://scenes/ui/popup/content/stage_select_content.tscn"),
	PopupId.RUN_CARD_PICK: preload("res://scenes/ui/popup/content/run_card_pick_content.tscn"),
	PopupId.SETTINGS: preload("res://scenes/ui/popup/content/settings_content.tscn"),
	PopupId.RUN_RELIC_PICK: preload("res://scenes/ui/popup/content/run_relic_pick_content.tscn"),
}

const WindowBaseScene := preload("res://scenes/ui/popup/popup_window_base.tscn")

var _windows: Dictionary = {}
var _building: Dictionary = {}
var _pending_open: Array = []
var _pending_stats_unit_id: String = "ally_a"


func open_popup(popup_id: int) -> void:
	if _building.get(popup_id, false):
		if popup_id not in _pending_open:
			_pending_open.append(popup_id)
		return
	var window: Window = _get_or_create_window(popup_id)
	if window == null or not window.is_inside_tree():
		if popup_id not in _pending_open:
			_pending_open.append(popup_id)
		return
	_show_popup_window(popup_id, window)


func open_character_stats(unit_id: String = "") -> void:
	if not unit_id.is_empty():
		_pending_stats_unit_id = unit_id
	open_popup(PopupId.CHARACTER_STATS)


func open_run_card_pick() -> void:
	_open_special_popup(PopupId.RUN_CARD_PICK, RUN_CARD_PICK_SIZE, "选择增益卡牌", false)


func close_run_card_pick() -> void:
	close_popup(PopupId.RUN_CARD_PICK)
	var window: Window = _windows.get(PopupId.RUN_CARD_PICK, null)
	if window and window.has_method("set_close_enabled"):
		window.call("set_close_enabled", true)


func open_run_relic_pick() -> void:
	_open_special_popup(PopupId.RUN_RELIC_PICK, RUN_RELIC_PICK_SIZE, "选择遗物", false)


func close_run_relic_pick() -> void:
	close_popup(PopupId.RUN_RELIC_PICK)
	var window: Window = _windows.get(PopupId.RUN_RELIC_PICK, null)
	if window and window.has_method("set_close_enabled"):
		window.call("set_close_enabled", true)


func _open_special_popup(popup_id: int, popup_size: Vector2i, title: String, close_enabled: bool) -> void:
	if _building.get(popup_id, false):
		if popup_id not in _pending_open:
			_pending_open.append(popup_id)
		return
	var window: Window = _get_or_create_window(popup_id)
	if window == null or not window.is_inside_tree():
		if popup_id not in _pending_open:
			_pending_open.append(popup_id)
		return
	window.size = popup_size
	window.min_size = popup_size
	window.max_size = popup_size
	window.popup_title = title
	if window.has_method("set_close_enabled"):
		window.call("set_close_enabled", close_enabled)
	_show_popup_window(popup_id, window)


func close_popup(popup_id: int) -> void:
	if _windows.has(popup_id):
		_windows[popup_id].hide_popup()


func toggle_popup(popup_id: int) -> void:
	if _building.get(popup_id, false):
		if popup_id not in _pending_open:
			_pending_open.append(popup_id)
		return
	if not _windows.has(popup_id):
		open_popup(popup_id)
		return
	var window: Window = _windows[popup_id]
	if window.visible:
		window.hide_popup()
	else:
		open_popup(popup_id)


func _get_or_create_window(popup_id: int) -> Window:
	if _windows.has(popup_id):
		return _windows[popup_id]
	if _building.get(popup_id, false):
		return null

	_building[popup_id] = true
	var window: Window = WindowBaseScene.instantiate()
	window.popup_title = POPUP_TITLES.get(popup_id, "Panel")
	_windows[popup_id] = window
	get_tree().root.call_deferred("add_child", window)
	window.tree_entered.connect(_on_popup_window_entered.bind(popup_id), CONNECT_ONE_SHOT)
	return window


func _on_popup_window_entered(popup_id: int) -> void:
	var window: Window = _windows.get(popup_id, null)
	if window == null or not is_instance_valid(window):
		_building.erase(popup_id)
		_flush_pending()
		return

	var content_root: Control = window.get_content_root()
	if content_root.get_child_count() == 0:
		var content: Control = CONTENT_SCENES[popup_id].instantiate()
		content.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		content.size_flags_vertical = Control.SIZE_EXPAND_FILL
		content_root.add_child(content)
		if content.has_method("refresh"):
			content.tree_entered.connect(func() -> void:
				if popup_id == PopupId.CHARACTER_STATS and content.has_method("set_unit_id"):
					content.call("set_unit_id", _pending_stats_unit_id)
				if content.has_method("refresh"):
					content.refresh()
			, CONNECT_ONE_SHOT)

	_building.erase(popup_id)
	if popup_id in _pending_open:
		_pending_open.erase(popup_id)
		_show_popup_window(popup_id, window)
	_flush_pending()


func _flush_pending() -> void:
	if _pending_open.is_empty():
		return
	var ids: Array = _pending_open.duplicate()
	_pending_open.clear()
	for pid in ids:
		open_popup(pid)


func _show_popup_window(popup_id: int, window: Window) -> void:
	window.show_popup()
	var content_root: Control = window.get_content_root()
	if content_root.get_child_count() > 0:
		var content: Node = content_root.get_child(0)
		if popup_id == PopupId.CHARACTER_STATS and content.has_method("set_unit_id"):
			content.call("set_unit_id", _pending_stats_unit_id)
		if content.has_method("refresh") and content.is_node_ready():
			content.refresh()
		elif content.has_method("refresh"):
			content.call_deferred("refresh")
