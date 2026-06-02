extends Control

const POPUP_TEXTS_PATH := "res://data/tables/ui/popup_texts.json"
const DEBUG_SETTINGS_PATH := "res://data/tables/meta/debug_settings.json"

@onready var _title: Label = %Title
@onready var _progress: Label = %ProgressLabel
@onready var _info: Label = %Info
@onready var _status: Label = %StatusLabel
@onready var _enter_button: Button = %EnterButton
@onready var _rest_panel: HBoxContainer = %RestPanel
@onready var _rest_button: Button = %RestButton
@onready var _reward_panel: VBoxContainer = %RewardPanel
@onready var _reward_gold: Button = %RewardGold
@onready var _reward_heal: Button = %RewardHeal
@onready var _reward_exp: Button = %RewardExp
@onready var _abandon_button: Button = %AbandonButton
@onready var _ticket_label: Label = %TicketLabel


func _ready() -> void:
	_apply_texts()
	if _enter_button:
		_enter_button.pressed.connect(_on_enter_pressed)
	if _rest_button:
		_rest_button.pressed.connect(_on_rest_pressed)
	if _reward_gold:
		_reward_gold.pressed.connect(_on_reward_pressed.bind(0))
	if _reward_heal:
		_reward_heal.pressed.connect(_on_reward_pressed.bind(1))
	if _reward_exp:
		_reward_exp.pressed.connect(_on_reward_pressed.bind(2))
	if _abandon_button:
		_abandon_button.pressed.connect(_on_abandon_pressed)
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("RunSessionChanged"):
		event_bus.connect("RunSessionChanged", _on_run_session_changed)
	call_deferred("refresh")


func refresh() -> void:
	_apply_texts()
	_sync_from_run()


func _apply_texts() -> void:
	var data := _load_section("wonderland")
	if data.is_empty():
		return
	if _title:
		_title.text = str(data.get("title", "奇境 Run"))
	if _info:
		_info.text = str(data.get("info", "线性 5–8 房间 · 通关结算 Meta 奖励"))
	if _enter_button and not _is_run_active():
		_enter_button.text = str(data.get("enterLabel", "开始 Run"))
		_enter_button.disabled = false
	if _ticket_label:
		if _skip_wonderland_ticket():
			_ticket_label.text = str(data.get("ticketLabel", "测试模式：免门票"))
		else:
			var prog := get_node_or_null("/root/ProgressionManager")
			var tickets := 0
			if prog:
				var snap: Variant = prog.call("GetHudSnapshot")
				if snap is Dictionary:
					tickets = int(snap.get("wonderland_tickets", 0))
			_ticket_label.text = "持有门票: %d" % tickets


func _sync_from_run() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run == null:
		_set_idle_ui()
		return
	var snap: Variant = run.call("GetSnapshot")
	if typeof(snap) != TYPE_DICTIONARY:
		_set_idle_ui()
		return
	var data: Dictionary = snap
	var state: String = str(data.get("state", "Idle"))
	var room_index: int = int(data.get("room_index", 0))
	var room_total: int = int(data.get("room_total", 0))
	var room_type: String = str(data.get("room_type", ""))
	var awaiting: bool = bool(data.get("awaiting_action", false))

	if _progress:
		if room_total > 0:
			_progress.text = "房间 %d/%d · %s" % [room_index + 1, room_total, _room_type_label(room_type)]
		else:
			_progress.text = "房间 —/—"

	if _status:
		_status.text = "状态: %s" % _state_label(state)

	var active := state not in ["Idle", "RunComplete", "RunFailed"]
	if _enter_button:
		_enter_button.visible = not active
		_enter_button.disabled = active
	if _abandon_button:
		_abandon_button.visible = active
	if _rest_panel:
		_rest_panel.visible = active and awaiting and room_type == "rest"
	if _reward_panel:
		_reward_panel.visible = false


func _set_idle_ui() -> void:
	if _progress:
		_progress.text = "房间 —/—"
	if _status:
		_status.text = "状态: 待机"
	if _enter_button:
		_enter_button.visible = true
		_enter_button.disabled = false
	if _abandon_button:
		_abandon_button.visible = false
	if _rest_panel:
		_rest_panel.visible = false
	if _reward_panel:
		_reward_panel.visible = false


func _is_run_active() -> bool:
	var run := get_node_or_null("/root/RunSessionManager")
	if run == null:
		return false
	var snap: Variant = run.call("GetSnapshot")
	if typeof(snap) != TYPE_DICTIONARY:
		return false
	var state: String = str(snap.get("state", "Idle"))
	return state not in ["Idle", "RunComplete", "RunFailed"]


func _on_enter_pressed() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("StartRun"):
		var ok: bool = bool(run.call("StartRun"))
		if ok:
			var popup_mgr := get_tree().root.get_node_or_null("GameRoot/PopupManager")
			if popup_mgr and popup_mgr.has_method("close_popup"):
				popup_mgr.call("close_popup", 4)
	refresh()


func _on_rest_pressed() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("ApplyRestHeal"):
		run.call("ApplyRestHeal")
	refresh()


func _on_reward_pressed(choice: int) -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("ApplyRewardChoice"):
		run.call("ApplyRewardChoice", choice)
	refresh()


func _on_abandon_pressed() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("AbandonRun"):
		run.call("AbandonRun")
	refresh()


func _on_run_session_changed(_state: String, _room_index: int, _room_total: int, _room_type: String) -> void:
	refresh()


func _room_type_label(room_type: String) -> String:
	match room_type:
		"combat": return "战斗"
		"rest": return "休息"
		"reward": return "奖励"
		_: return "—"


func _state_label(state: String) -> String:
	match state:
		"InRoom": return "进行中"
		"RoomCleared": return "房间已通过"
		"RunComplete": return "通关！Meta 已结算"
		"RunFailed": return "失败 · 50% 金币"
		_: return "待机"


func _load_section(section: String) -> Dictionary:
	if not FileAccess.file_exists(POPUP_TEXTS_PATH):
		return {}
	var file := FileAccess.open(POPUP_TEXTS_PATH, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}
	return parsed.get(section, {})


func _skip_wonderland_ticket() -> bool:
	if not FileAccess.file_exists(DEBUG_SETTINGS_PATH):
		return true
	var file := FileAccess.open(DEBUG_SETTINGS_PATH, FileAccess.READ)
	if file == null:
		return true
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if typeof(parsed) != TYPE_DICTIONARY:
		return true
	return bool(parsed.get("skipWonderlandTicket", true))
