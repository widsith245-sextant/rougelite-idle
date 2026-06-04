extends PanelContainer

## F8 GM panel: combat debug, effect lab, scenarios, VFX preview.

@onready var _log: Label = %LogLabel
@onready var _effect_option: OptionButton = %EffectOption
@onready var _target_option: OptionButton = %TargetOption
@onready var _scenario_option: OptionButton = %ScenarioOption
var _scenario_ids: Array = []
@onready var _pile_spin: SpinBox = %PileSpin
@onready var _intensity_spin: SpinBox = %IntensitySpin
@onready var _category_option: OptionButton = %CategoryOption

var _fast_mode := false


func _ready() -> void:
	%BtnClose.pressed.connect(_on_close_pressed)
	%BtnRefresh.pressed.connect(_refresh_log)
	%BtnRestart.pressed.connect(_on_restart)
	%BtnFillGauge.pressed.connect(_on_fill_gauge)
	%BtnForceA.pressed.connect(_on_force.bind("ally_a"))
	%BtnFast.pressed.connect(_on_toggle_fast)
	%BtnApplyEffect.pressed.connect(_on_apply_effect)
	%BtnClearEffects.pressed.connect(_on_clear_effects)
	%BtnTriggerDamaged.pressed.connect(_on_trigger.bind("OnDamaged"))
	%BtnTriggerMove.pressed.connect(_on_trigger.bind("OnMoveEnd"))
	%BtnTriggerSwap.pressed.connect(_on_trigger.bind("OnForceSwap"))
	%BtnTriggerTick.pressed.connect(_on_trigger.bind("OnTimeElapsed"))
	%BtnLoadScenario.pressed.connect(_on_load_scenario)
	%BtnPreviewVfx.pressed.connect(_on_preview_vfx)
	%BtnPrintEffects.pressed.connect(_on_print_effects)
	_populate_dropdowns()
	call_deferred("_refresh_log")


func _populate_dropdowns() -> void:
	_effect_option.clear()
	var cm := get_node_or_null("/root/CombatManager")
	if cm and cm.has_method("GetAllEffectIds"):
		for effect_id in cm.call("GetAllEffectIds"):
			_effect_option.add_item(str(effect_id))

	_target_option.clear()
	for unit_id in ["ally_a", "ally_b", "ally_c", "enemy_1", "enemy_2"]:
		_target_option.add_item(unit_id)

	_scenario_option.clear()
	_scenario_ids.clear()
	if cm and cm.has_method("GetScenarioList"):
		for entry in cm.call("GetScenarioList"):
			if entry is Dictionary:
				var data: Dictionary = entry
				_scenario_ids.append(str(data.get("id", "")))
				_scenario_option.add_item(str(data.get("label", data.get("id", ""))))

	_category_option.clear()
	for cat in ["physical", "magical", "mark", "shield", "retaliate", "heal"]:
		_category_option.add_item(cat)


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


func _on_apply_effect() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm == null or _effect_option.item_count == 0:
		return
	var effect_id := _effect_option.get_item_text(_effect_option.selected)
	var target_id := _target_option.get_item_text(_target_option.selected)
	cm.call(
		"DebugApplyEffect",
		target_id,
		effect_id,
		int(_pile_spin.value),
		float(_intensity_spin.value),
	)
	_refresh_log()


func _on_clear_effects() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		var target_id := _target_option.get_item_text(_target_option.selected)
		cm.call("DebugClearEffects", target_id)
	_refresh_log()


func _on_trigger(kind: String) -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		var target_id := _target_option.get_item_text(_target_option.selected)
		cm.call("DebugEmitTrigger", kind, target_id)
	_refresh_log()


func _on_load_scenario() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm == null or _scenario_ids.is_empty():
		return
	var scenario_id := str(_scenario_ids[_scenario_option.selected])
	cm.call("DebugLoadScenario", scenario_id)
	_refresh_log()


func _on_preview_vfx() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm:
		var category := _category_option.get_item_text(_category_option.selected)
		cm.call("DebugPreviewDamageNumber", category, 42.0, 300.0)


func _on_print_effects() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm == null:
		return
	var target_id := _target_option.get_item_text(_target_option.selected)
	var snap: Variant = cm.call("GetUnitEffectsSnapshot", target_id)
	_log.text = JSON.stringify(snap)


func _refresh_log() -> void:
	var cm := get_node_or_null("/root/CombatManager")
	if cm == null:
		_log.text = "CombatManager 未找到"
		return
	if not cm.has_method("GetDebugSnapshot"):
		_log.text = "Debug API 不可用"
		return
	var snap: Dictionary = cm.call("GetDebugSnapshot")
	var lines: PackedStringArray = []
	lines.append("队列:%s resolving:%s" % [snap.get("queue_count", 0), snap.get("is_resolving", false)])
	var units: Array = snap.get("units", [])
	for u in units:
		if u is Dictionary:
			var data: Dictionary = u
			lines.append("%s g:%.0f hp:%.0f" % [
				data.get("id", ""),
				float(data.get("gauge", 0.0)),
				float(data.get("hp", 0.0)),
			])
	_log.text = "\n".join(lines)


func _on_close_pressed() -> void:
	var layer := get_parent()
	if layer and layer.has_method("toggle_panel"):
		layer.call("toggle_panel")
	else:
		visible = false
