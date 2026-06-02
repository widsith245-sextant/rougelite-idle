extends Control

## Bottom drawer: 5 icon buttons, panels slide up over combat area.

enum DrawerPanel {
	OUTFIT,
	SKILL,
	IDENTIFY,
	STAR_CHART,
	WONDERLAND,
}

const ICON_LABELS := ["换装", "技能", "鉴定", "星图", "奇境"]

@onready var _icon_bar: HBoxContainer = %IconBar
@onready var _panels: Control = %Panels

var _panel_map: Dictionary = {}
var _active_panel: Control


func _ready() -> void:
	_build_icon_buttons()
	_cache_panels()


func _build_icon_buttons() -> void:
	for i in ICON_LABELS.size():
		var button := Button.new()
		button.text = ICON_LABELS[i]
		button.custom_minimum_size = Vector2(72, 24)
		button.pressed.connect(_on_icon_pressed.bind(i))
		_icon_bar.add_child(button)


func _cache_panels() -> void:
	for child in _panels.get_children():
		if child.has_method("open"):
			_panel_map[child.name] = child
			if child.has_signal("panel_closed"):
				child.panel_closed.connect(_on_panel_closed)


func _on_icon_pressed(panel_index: int) -> void:
	var panel_name := _panel_name_for_index(panel_index)
	if not _panel_map.has(panel_name):
		return
	_open_panel(_panel_map[panel_name])


func _panel_name_for_index(index: int) -> String:
	match index:
		DrawerPanel.OUTFIT:
			return "OutfitPanel"
		DrawerPanel.SKILL:
			return "SkillPanel"
		DrawerPanel.IDENTIFY:
			return "IdentifyPanel"
		DrawerPanel.STAR_CHART:
			return "StarChartPanel"
		DrawerPanel.WONDERLAND:
			return "WonderlandPanel"
		_:
			return ""


func _open_panel(panel: Control) -> void:
	if _active_panel and _active_panel != panel and _active_panel.has_method("close"):
		_active_panel.close()
	_active_panel = panel
	panel.open()


func _on_panel_closed() -> void:
	_active_panel = null
