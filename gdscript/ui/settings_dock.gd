extends Control

## Persistent settings button docked at bottom-left (parallel to QuickBar).

const POPUP_SETTINGS := 8

@onready var _settings_button: Button = %SettingsButton


func _ready() -> void:
	if _settings_button:
		_settings_button.pressed.connect(_on_settings_pressed)


func _on_settings_pressed() -> void:
	var popup := get_node_or_null("/root/GameRoot/PopupManager")
	if popup and popup.has_method("toggle_popup"):
		popup.call("toggle_popup", POPUP_SETTINGS)
