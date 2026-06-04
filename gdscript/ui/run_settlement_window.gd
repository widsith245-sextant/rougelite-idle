extends Window

## Run settlement summary + golden chest claim.

const WINDOW_SIZE := Vector2i(400, 320)

@onready var _grade_label: Label = %GradeLabel
@onready var _stats_label: Label = %StatsLabel
@onready var _reward_label: Label = %RewardLabel
@onready var _chest_label: Label = %ChestLabel
@onready var _claim_button: Button = %ClaimButton


func _ready() -> void:
	title = "奇境结算"
	size = WINDOW_SIZE
	min_size = WINDOW_SIZE
	max_size = WINDOW_SIZE
	borderless = true
	unresizable = true
	visible = false
	SatelliteWindow.configure(self, true)
	close_requested.connect(_on_claim_pressed)
	if _claim_button:
		_claim_button.pressed.connect(_on_claim_pressed)


func show_settlement(data: Dictionary) -> void:
	var grade: String = str(data.get("grade", "C"))
	var chest: String = str(data.get("chest_quality", "common"))
	var rooms: int = int(data.get("rooms_cleared", 0))
	var kills: int = int(data.get("total_kills", 0))
	var damage: float = float(data.get("damage_taken", 0))
	var lowest: float = float(data.get("lowest_hp_percent", 1.0))
	var elapsed: float = float(data.get("elapsed_seconds", 0))
	var deaths: int = int(data.get("deaths", 0))
	var gold: int = int(data.get("gold_grant", 0))
	var exp: float = float(data.get("exp_grant", 0))
	var success: bool = bool(data.get("success", false))

	if _grade_label:
		_grade_label.text = "评级: %s%s" % [grade, " · 通关" if success else " · 失败"]
	if _stats_label:
		_stats_label.text = "房间 %d · 击杀 %d · 承伤 %.0f · 最低HP %.0f%% · 死亡 %d · 用时 %.0fs" % [
			rooms, kills, damage, lowest * 100.0, deaths, elapsed,
		]
	if _reward_label:
		_reward_label.text = "金币 +%d · 经验 +%.0f" % [gold, exp]
	if _chest_label:
		_chest_label.text = "黄金宝箱: %s" % chest
	if _claim_button:
		_claim_button.text = "领取宝箱"

	popup_centered()
	visible = true
	show()
	SatelliteWindow.ensure_transient_parent(self)


func hide_window() -> void:
	hide()
	visible = false


func _on_claim_pressed() -> void:
	var mgr := get_node_or_null("/root/RunSettlementWindowManager")
	if mgr and mgr.has_method("ClaimSettlement"):
		mgr.call("ClaimSettlement")
	_play_chest_reveal()
	hide_window()


func _play_chest_reveal() -> void:
	var root := get_tree().root.get_node_or_null("GameRoot")
	if root == null:
		return
	var overlay := root.get_node_or_null("ChestRevealOverlay")
	if overlay == null or not overlay.has_method("play_reveal"):
		return
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	var pending: Variant = loot.get("ActivePendingQuality")
	var quality: String = str(pending) if pending != null else "common"
	var item_data := {
		"quality": quality,
		"display_name": "奇境黄金宝箱",
		"item_level": 1,
		"affixes": [],
	}
	overlay.call("play_reveal", item_data)
