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
var _pending_stats_unit_id: String = "ally_a"


func open_popup(popup_id: int) -> void:
	var window: Window = _get_or_create_window(popup_id)
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


func open_character_stats(unit_id: String = "") -> void:
	if not unit_id.is_empty():
		_pending_stats_unit_id = unit_id
	open_popup(PopupId.CHARACTER_STATS)


func open_run_card_pick() -> void:
	var window: Window = _get_or_create_window(PopupId.RUN_CARD_PICK)
	window.size = RUN_CARD_PICK_SIZE
	window.min_size = RUN_CARD_PICK_SIZE
	window.max_size = RUN_CARD_PICK_SIZE
	window.popup_title = POPUP_TITLES.get(PopupId.RUN_CARD_PICK, "选择增益卡牌")
	if window.has_method("set_close_enabled"):
		window.call("set_close_enabled", false)
	window.show_popup()
	var content_root: Control = window.get_content_root()
	if content_root.get_child_count() > 0:
		var content: Node = content_root.get_child(0)
		if content.has_method("refresh"):
			content.refresh()


func close_run_card_pick() -> void:
	close_popup(PopupId.RUN_CARD_PICK)
	var window: Window = _windows.get(PopupId.RUN_CARD_PICK, null)
	if window and window.has_method("set_close_enabled"):
		window.call("set_close_enabled", true)


func open_run_relic_pick() -> void:
	var window: Window = _get_or_create_window(PopupId.RUN_RELIC_PICK)
	window.size = RUN_RELIC_PICK_SIZE
	window.min_size = RUN_RELIC_PICK_SIZE
	window.max_size = RUN_RELIC_PICK_SIZE
	window.popup_title = POPUP_TITLES.get(PopupId.RUN_RELIC_PICK, "选择遗物")
	if window.has_method("set_close_enabled"):
		window.call("set_close_enabled", false)
	window.show_popup()
	var content_root: Control = window.get_content_root()
	if content_root.get_child_count() > 0:
		var content: Node = content_root.get_child(0)
		if content.has_method("refresh"):
			content.refresh()


func close_run_relic_pick() -> void:
	close_popup(PopupId.RUN_RELIC_PICK)
	var window: Window = _windows.get(PopupId.RUN_RELIC_PICK, null)
	if window and window.has_method("set_close_enabled"):
		window.call("set_close_enabled", true)


func close_popup(popup_id: int) -> void:
	if _windows.has(popup_id):
		_windows[popup_id].hide_popup()


func toggle_popup(popup_id: int) -> void:
	var window: Window = _get_or_create_window(popup_id)
	if window.visible:
		window.hide_popup()
	else:
		open_popup(popup_id)


func _get_or_create_window(popup_id: int) -> Window:
	if _windows.has(popup_id):
		return _windows[popup_id]

	var window: Window = WindowBaseScene.instantiate()
	window.popup_title = POPUP_TITLES.get(popup_id, "Panel")

	get_tree().root.add_child(window)
	# popup_window_base._ready 会调用 SatelliteWindow.configure，避免重复配置。

	var content_root: Control = window.get_content_root()
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

	_windows[popup_id] = window
	return window
