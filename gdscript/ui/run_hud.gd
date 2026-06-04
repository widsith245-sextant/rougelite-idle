extends Control

## Run HUD: room progress, queue peek, active cards, abandon.

const WONDERLAND_ACCENT := Color(0.82, 0.62, 1.0)

@onready var _panel: PanelContainer = %Panel
@onready var _title: Label = %TitleLabel
@onready var _progress: Label = %ProgressLabel
@onready var _queue: Label = %QueueLabel
@onready var _cards: Label = %CardsLabel
@onready var _detail_button: Button = %DetailButton
@onready var _abandon_button: Button = %AbandonButton
@onready var _rest_button: Button = %RestButton

var _visible_run: bool = false


func _ready() -> void:
	visible = false
	if _detail_button:
		_detail_button.pressed.connect(_on_detail_pressed)
	if _abandon_button:
		_abandon_button.pressed.connect(_on_abandon_pressed)
	if _rest_button:
		_rest_button.pressed.connect(_on_rest_pressed)
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus:
		if event_bus.has_signal("RunSessionChanged"):
			event_bus.connect("RunSessionChanged", _on_run_session_changed)
		if event_bus.has_signal("RunCardPickOffered"):
			event_bus.connect("RunCardPickOffered", _on_card_pick_offered)
		if event_bus.has_signal("RunRelicPickOffered"):
			event_bus.connect("RunRelicPickOffered", _on_relic_pick_offered)
	call_deferred("refresh")


func refresh() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run == null:
		_hide()
		return
	var snap: Variant = run.call("GetSnapshot")
	if typeof(snap) != TYPE_DICTIONARY:
		_hide()
		return
	var data: Dictionary = snap
	var state: String = str(data.get("state", "Idle"))
	var active := state not in ["Idle", "RunComplete", "RunFailed"]
	_visible_run = active
	visible = active
	if not active:
		return

	var room_index: int = int(data.get("room_index", 0))
	var room_total: int = int(data.get("room_total", 0))
	var room_type: String = str(data.get("room_type", ""))
	var awaiting: bool = bool(data.get("awaiting_action", false))

	if _title:
		_title.text = "奇境"
		_title.add_theme_color_override("font_color", WONDERLAND_ACCENT)
	if _progress:
		_progress.text = "%d/%d · %s" % [
			room_index + 1,
			room_total,
			_room_type_label(room_type),
		]
	if _queue:
		_queue.text = _format_queue(run)
	if _cards:
		_cards.text = _format_cards(run)
	if _rest_button:
		_rest_button.visible = awaiting and room_type == "rest"


func _hide() -> void:
	_visible_run = false
	visible = false


func _format_queue(run: Node) -> String:
	if not run.has_method("GetRoomQueueSnapshot"):
		return ""
	var queue: Variant = run.call("GetRoomQueueSnapshot")
	if typeof(queue) != TYPE_ARRAY:
		return ""
	var parts: PackedStringArray = []
	for entry in queue:
		if typeof(entry) != TYPE_DICTIONARY:
			continue
		var row: Dictionary = entry
		var current := bool(row.get("current", false))
		var t: String = _room_type_short(str(row.get("type", "")))
		parts.append("[%s]" % t if current else t)
	return " ".join(parts)


func _format_cards(run: Node) -> String:
	var parts: PackedStringArray = []
	if run.has_method("GetActiveRelicsSnapshot"):
		var relics: Variant = run.call("GetActiveRelicsSnapshot")
		if typeof(relics) == TYPE_ARRAY and not relics.is_empty():
			var relic_names: PackedStringArray = []
			for entry in relics:
				if typeof(entry) != TYPE_DICTIONARY:
					continue
				relic_names.append(str(entry.get("name", "?")))
			parts.append("遗物: " + ", ".join(relic_names))
	if run.has_method("GetActiveCardsSnapshot"):
		var cards: Variant = run.call("GetActiveCardsSnapshot")
		if typeof(cards) == TYPE_ARRAY and not cards.is_empty():
			var names: PackedStringArray = []
			for entry in cards:
				if typeof(entry) != TYPE_DICTIONARY:
					continue
				names.append(str(entry.get("name", "?")))
			parts.append("卡牌: " + ", ".join(names))
	if parts.is_empty():
		return "构筑: —"
	return " · ".join(parts)


func _on_detail_pressed() -> void:
	var popup := get_tree().root.get_node_or_null("GameRoot/PopupManager")
	if popup and popup.has_method("open_popup"):
		popup.call("open_popup", 4)


func _on_abandon_pressed() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("AbandonRun"):
		run.call("AbandonRun")
	refresh()


func _on_rest_pressed() -> void:
	var run := get_node_or_null("/root/RunSessionManager")
	if run and run.has_method("ApplyRestHeal"):
		run.call("ApplyRestHeal")
	refresh()


func _on_run_session_changed(_state: String, _room_index: int, _room_total: int, _room_type: String) -> void:
	refresh()


func _on_card_pick_offered() -> void:
	var popup := get_tree().root.get_node_or_null("GameRoot/PopupManager")
	if popup and popup.has_method("open_run_card_pick"):
		popup.call("open_run_card_pick")


func _on_relic_pick_offered() -> void:
	var popup := get_tree().root.get_node_or_null("GameRoot/PopupManager")
	if popup and popup.has_method("open_run_relic_pick"):
		popup.call("open_run_relic_pick")


func _room_type_label(room_type: String) -> String:
	match room_type:
		"combat": return "战斗"
		"rest": return "休息"
		"reward": return "奖励"
		_: return "—"


func _room_type_short(room_type: String) -> String:
	match room_type:
		"combat": return "战"
		"rest": return "休"
		"reward": return "奖"
		_: return "?"
