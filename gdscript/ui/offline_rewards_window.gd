extends Window

## Satellite offline rewards panel (preview before claim).

const WINDOW_SIZE := Vector2i(360, 240)

@onready var _title_label: Label = %TitleLabel
@onready var _elapsed_label: Label = %ElapsedLabel
@onready var _gold_label: Label = %GoldLabel
@onready var _exp_label: Label = %ExpLabel
@onready var _hint_label: Label = %HintLabel
@onready var _claim_button: Button = %ClaimButton
@onready var _later_button: Button = %LaterButton


func _ready() -> void:
	title = "离线收益"
	size = WINDOW_SIZE
	min_size = WINDOW_SIZE
	max_size = WINDOW_SIZE
	borderless = true
	unresizable = true
	visible = false
	SatelliteWindow.configure(self, true)
	close_requested.connect(_on_later_pressed)
	if _claim_button:
		_claim_button.pressed.connect(_on_claim_pressed)
	if _later_button:
		_later_button.pressed.connect(_on_later_pressed)


func show_pending(data: Dictionary) -> void:
	if _title_label:
		_title_label.text = "离线挂机收益"
	if _elapsed_label:
		_elapsed_label.text = "离线时长: %s" % str(data.get("elapsed_label", "—"))
	if _gold_label:
		_gold_label.text = "金币: +%d" % int(data.get("gold", 0))
	if _exp_label:
		_exp_label.text = "经验: +%.0f" % float(data.get("experience", 0))
	if _hint_label:
		_hint_label.text = "公式: 金币=秒×0.5×等级 · 经验=秒×0.2×等级"
	SatelliteWindow.place_popup_beside_main(self, self)
	visible = true
	show()
	call_deferred("_after_show")


func _after_show() -> void:
	SatelliteWindow.ensure_transient_parent(self)
	var wid := get_window_id()
	if wid >= 0:
		DisplayServer.window_move_to_foreground(wid)


func hide_window() -> void:
	hide()
	visible = false


func _on_claim_pressed() -> void:
	var mgr := get_node_or_null("/root/OfflineRewardsWindowManager")
	hide_window()
	if mgr and mgr.has_method("ClaimPending"):
		mgr.call_deferred("ClaimPending")


func _on_later_pressed() -> void:
	var mgr := get_node_or_null("/root/OfflineRewardsWindowManager")
	if mgr and mgr.has_method("DismissPending"):
		mgr.call("DismissPending")
	else:
		hide_window()
