extends CanvasLayer

## Top-of-combat broadcast feed for kills, rewards, waves, and run events.

const MAX_LINES := 3
const STAGGER_DELAY := 0.15
const DISPLAY_DURATION := 1.2
const FADE_DURATION := 0.35

@onready var _feed: VBoxContainer = %FeedContainer

var _pending: Array = []
var _active_labels: Array = []
var _stagger_timer: float = 0.0

const CATEGORY_COLORS := {
	"kill": Color(1.0, 0.92, 0.55),
	"reward": Color(0.55, 0.95, 0.75),
	"wave": Color(0.75, 0.85, 1.0),
	"run": Color(0.95, 0.75, 1.0),
}


func _ready() -> void:
	layer = 3
	var event_bus := get_node_or_null("/root/EventBus")
	if event_bus and event_bus.has_signal("CombatBroadcast"):
		event_bus.connect("CombatBroadcast", _on_combat_broadcast)


func _process(delta: float) -> void:
	if _pending.is_empty():
		return
	_stagger_timer -= delta
	if _stagger_timer > 0.0:
		return
	var next: Dictionary = _pending.pop_front()
	_show_line(str(next.get("message", "")), str(next.get("category", "reward")))
	_stagger_timer = STAGGER_DELAY if not _pending.is_empty() else 0.0


func _on_combat_broadcast(message: String, category: String) -> void:
	if message.is_empty():
		return
	_pending.append({"message": message, "category": category})
	if _stagger_timer <= 0.0 and _active_labels.size() < MAX_LINES:
		_stagger_timer = 0.01


func _show_line(message: String, category: String) -> void:
	if _feed == null:
		return
	while _active_labels.size() >= MAX_LINES:
		var old: Label = _active_labels.pop_front()
		if is_instance_valid(old):
			old.queue_free()

	var label := Label.new()
	label.text = message
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 11)
	label.modulate = CATEGORY_COLORS.get(category, Color.WHITE)
	label.modulate.a = 0.0
	_feed.add_child(label)
	_active_labels.append(label)

	var tw := create_tween()
	tw.tween_property(label, "modulate:a", 1.0, 0.08)
	tw.tween_interval(DISPLAY_DURATION)
	tw.tween_property(label, "modulate:a", 0.0, FADE_DURATION)
	tw.tween_callback(func() -> void:
		_active_labels.erase(label)
		if is_instance_valid(label):
			label.queue_free()
	)
