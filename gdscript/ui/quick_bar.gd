extends Control

## Collapsible quick bar anchored to bottom-right.

const COLLAPSED_HEIGHT := 18.0
const EXPANDED_HEIGHT := 30.0
const EXPANDED_WIDTH := 500.0
const TWEEN_DURATION := 0.2

const POPUP_IDS := {
	"backpack": 0,
	"squad": 1,
	"skill": 2,
	"cultivation": 3,
	"wonderland": 4,
	"star_chart": 3,
	"stage": 6,
}

@onready var _bar_anchor: Control = %BarAnchor
@onready var _toggle_button: Button = %ToggleButton
@onready var _expanded_row: HBoxContainer = %ExpandedRow
@onready var _collapse_button: Button = %CollapseButton

var _expanded := false
var _popup_manager: Node


func _ready() -> void:
	_popup_manager = get_node_or_null("/root/GameRoot/PopupManager")

	_toggle_button.pressed.connect(_on_toggle_pressed)
	_collapse_button.pressed.connect(_on_collapse_pressed)
	%BackpackButton.pressed.connect(_open_popup.bind(POPUP_IDS.backpack))
	%SquadButton.pressed.connect(_open_popup.bind(POPUP_IDS.squad))
	%SkillButton.pressed.connect(_open_popup.bind(POPUP_IDS.skill))
	%CultivationButton.pressed.connect(_open_popup.bind(POPUP_IDS.cultivation))
	%WonderlandButton.pressed.connect(_open_popup.bind(POPUP_IDS.wonderland))
	%StageButton.pressed.connect(_open_popup.bind(POPUP_IDS.stage))

	_set_expanded(false, false)


func _on_toggle_pressed() -> void:
	_set_expanded(true, true)


func _on_collapse_pressed() -> void:
	_set_expanded(false, true)


func _open_popup(popup_id: int) -> void:
	if _popup_manager and _popup_manager.has_method("open_popup"):
		_popup_manager.open_popup(popup_id)


func _set_expanded(expanded: bool, animate: bool) -> void:
	_expanded = expanded
	_toggle_button.visible = not expanded
	_expanded_row.visible = expanded

	var target_height := EXPANDED_HEIGHT if expanded else COLLAPSED_HEIGHT
	var target_width := EXPANDED_WIDTH if expanded else _toggle_button.size.x + 8.0
	var target_top := -target_height
	var target_left := -target_width

	if animate:
		var tween := create_tween()
		tween.set_parallel(true)
		tween.set_trans(Tween.TRANS_CUBIC)
		tween.set_ease(Tween.EASE_OUT)
		tween.tween_property(_bar_anchor, "offset_top", target_top, TWEEN_DURATION)
		tween.tween_property(_bar_anchor, "offset_left", target_left, TWEEN_DURATION)
	else:
		_bar_anchor.offset_top = target_top
		_bar_anchor.offset_left = target_left
