extends Control

@onready var _remember_window: CheckBox = %RememberWindowCheck
@onready var _reset_window_button: Button = %ResetWindowButton
@onready var _combat_trace: CheckBox = %CombatTraceCheck
@onready var _identify_reveal: CheckBox = %IdentifyRevealCheck
@onready var _interval_option: OptionButton = %IntervalOption
@onready var _open_save_dir_button: Button = %OpenSaveDirButton
@onready var _reset_progress_button: Button = %ResetProgressButton
@onready var _toggle_log_button: Button = %ToggleLogButton
@onready var _portrait_button: Button = %PortraitButton
@onready var _status_label: Label = %StatusLabel


func _ready() -> void:
	_interval_option.clear()
	_interval_option.add_item("0.5×", 0)
	_interval_option.add_item("1.0×", 1)
	_interval_option.add_item("1.5×", 2)
	_interval_option.item_selected.connect(_on_interval_selected)
	_remember_window.toggled.connect(_on_remember_window_toggled)
	_reset_window_button.pressed.connect(_on_reset_window_pressed)
	_combat_trace.toggled.connect(_on_combat_trace_toggled)
	_identify_reveal.toggled.connect(_on_identify_reveal_toggled)
	_open_save_dir_button.pressed.connect(_on_open_save_dir_pressed)
	_reset_progress_button.pressed.connect(_on_reset_progress_pressed)
	if _toggle_log_button:
		_toggle_log_button.pressed.connect(_on_toggle_log_pressed)
	if _portrait_button:
		_portrait_button.pressed.connect(_on_portrait_pressed)
	_apply_snapshot()


func refresh() -> void:
	_apply_snapshot()


func _apply_snapshot() -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings == null or not settings.has_method("GetSnapshot"):
		return
	var snap: Dictionary = settings.call("GetSnapshot")
	_remember_window.button_pressed = bool(snap.get("remember_window_position", true))
	_combat_trace.button_pressed = bool(snap.get("combat_trace_enabled", true))
	_identify_reveal.button_pressed = bool(snap.get("identify_reveal_enabled", true))
	var mul := float(snap.get("identify_interval_multiplier", 1.0))
	if is_equal_approx(mul, 0.5):
		_interval_option.select(0)
	elif is_equal_approx(mul, 1.5):
		_interval_option.select(2)
	else:
		_interval_option.select(1)


func _on_remember_window_toggled(enabled: bool) -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("SetRememberWindowPosition"):
		settings.call("SetRememberWindowPosition", enabled)


func _on_reset_window_pressed() -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("ClearSavedWindowPosition"):
		settings.call("ClearSavedWindowPosition")
	var chrome := get_tree().root.get_node_or_null("GameRoot/WindowChrome")
	if chrome and chrome.has_method("reset_position"):
		chrome.call("reset_position")
	_set_status("主窗位置已重置")


func _on_combat_trace_toggled(enabled: bool) -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("SetCombatTraceEnabled"):
		settings.call("SetCombatTraceEnabled", enabled)


func _on_identify_reveal_toggled(enabled: bool) -> void:
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("SetIdentifyRevealEnabled"):
		settings.call("SetIdentifyRevealEnabled", enabled)


func _on_interval_selected(index: int) -> void:
	var mul := 1.0
	match index:
		0:
			mul = 0.5
		2:
			mul = 1.5
	var settings := get_node_or_null("/root/GameSettingsManager")
	if settings and settings.has_method("SetIdentifyIntervalMultiplier"):
		settings.call("SetIdentifyIntervalMultiplier", mul)


func _on_open_save_dir_pressed() -> void:
	OS.shell_open(ProjectSettings.globalize_path("user://"))
	_set_status("已打开存档目录")


func _on_toggle_log_pressed() -> void:
	var mgr := get_node_or_null("/root/LogWindowManager")
	if mgr and mgr.has_method("Toggle"):
		mgr.call("Toggle")
	_set_status("已切换日志窗口")


func _on_portrait_pressed() -> void:
	var party := get_node_or_null("/root/PartyManager")
	if party == null:
		return
	var unit_id := "ally_a"
	if party.has_method("GetUnitIdForSlot"):
		unit_id = str(party.call("GetUnitIdForSlot", 0))
	var mgr := get_node_or_null("/root/PortraitWindowManager")
	if mgr and mgr.has_method("ShowPortrait"):
		mgr.call("ShowPortrait", unit_id, false)
	_set_status("已打开立绘预览")


func _on_reset_progress_pressed() -> void:
	var dialog := ConfirmationDialog.new()
	dialog.title = "重置进度"
	dialog.dialog_text = "将删除 savegame.json 并重启场景。\n设置文件 settings.json 会保留。"
	dialog.confirmed.connect(_confirm_reset_progress)
	add_child(dialog)
	dialog.popup_centered()


func _confirm_reset_progress() -> void:
	var save := get_node_or_null("/root/SaveManager")
	if save and save.has_method("DeleteSave"):
		save.call("DeleteSave")
	get_tree().reload_current_scene()


func _set_status(text: String) -> void:
	if _status_label:
		_status_label.text = text
