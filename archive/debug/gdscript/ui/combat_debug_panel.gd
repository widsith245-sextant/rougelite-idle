extends Control

## Combat debug overlay — buttons to inspect and force combat flow.

@onready var _log_label: Label = %LogLabel

var _fast_mode := false


func _ready() -> void:
	%BtnRefresh.pressed.connect(_on_refresh)
	%BtnRestart.pressed.connect(_on_restart)
	%BtnFillGauge.pressed.connect(_on_fill_gauge)
	%BtnForceA.pressed.connect(_on_force.bind("ally_a"))
	%BtnForceB.pressed.connect(_on_force.bind("ally_b"))
	%BtnForceC.pressed.connect(_on_force.bind("ally_c"))
	%BtnFast.pressed.connect(_on_toggle_fast)
	%BtnToggle.pressed.connect(_on_toggle_panel)

	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus:
		if event_bus.has_signal("CombatActionStarted"):
			event_bus.connect("CombatActionStarted", _on_action_started)
		if event_bus.has_signal("DamageDealt"):
			event_bus.connect("DamageDealt", _on_damage_dealt)

	call_deferred("_refresh_log")


func _on_toggle_panel() -> void:
	%Body.visible = not %Body.visible
	%BtnToggle.text = "▼" if %Body.visible else "▲"


func _on_refresh() -> void:
	_refresh_log()


func _on_restart() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		cm.call("DebugRestartEncounter")
	_refresh_log()


func _on_fill_gauge() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		cm.call("DebugFillGauges")
	_refresh_log()


func _on_force(ally_id: String) -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		cm.call("DebugForceAllyTurn", ally_id)
	_refresh_log()


func _on_toggle_fast() -> void:
	_fast_mode = not _fast_mode
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		cm.call("DebugSetFastMode", _fast_mode)
	%BtnFast.text = "快速:开" if _fast_mode else "快速:关"
	_refresh_log()


func _on_action_started(actor_id: String) -> void:
	_append_log("行动开始: %s" % actor_id)


func _on_damage_dealt(source_id: String, target_id: String, amount: float) -> void:
	_append_log("伤害 %s→%s %.0f" % [source_id, target_id, amount])


func _append_log(line: String) -> void:
	var text: String = _log_label.text
	var lines: PackedStringArray = text.split("\n")
	if lines.size() > 6:
		lines = lines.slice(lines.size() - 6)
	lines.append(line)
	_log_label.text = "\n".join(lines)


func _refresh_log() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm == null:
		_log_label.text = "CombatManager 未找到"
		return

	var snap: Dictionary = cm.call("GetDebugSnapshot")
	var lines: PackedStringArray = []
	lines.append("队列:%s  resolving:%s  pending:%s" % [
		str(snap.get("queue_count", 0)),
		str(snap.get("is_resolving", false)),
		str(snap.get("pending_actor", "")),
	])
	lines.append("重生等待:%s  timer:%.1f" % [
		str(snap.get("waiting_respawn", false)),
		float(snap.get("respawn_timer", 0.0)),
	])

	var units: Array = snap.get("units", [])
	for u in units:
		var d: Dictionary = u
		lines.append("%s gauge:%.0f hp:%.0f%s" % [
			str(d.get("id", "")),
			float(d.get("gauge", 0.0)),
			float(d.get("hp", 0.0)),
			" *" if d.get("processing", false) else "",
		])

	_log_label.text = "\n".join(lines)
