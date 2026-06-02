extends Control

## Cultivation popup: DB tree (gold) | Star chart (points).

@onready var _main_tab: TabBar = %MainTabBar
@onready var _db_grid: GridContainer = %DbGrid
@onready var _star_grid: GridContainer = %StarGrid
@onready var _footer: Label = %FooterLabel

var _db_buttons: Dictionary = {}
var _star_buttons: Dictionary = {}


func _ready() -> void:
	if _main_tab:
		_main_tab.tab_changed.connect(_on_main_tab_changed)
	call_deferred("refresh")


func refresh() -> void:
	_refresh_db_tab()
	_refresh_star_tab()
	_update_footer()
	_on_main_tab_changed(_main_tab.current_tab if _main_tab else 0)


func _on_main_tab_changed(tab: int) -> void:
	if _db_grid:
		_db_grid.visible = tab == 0
	if _star_grid:
		_star_grid.visible = tab == 1


func _refresh_db_tab() -> void:
	if _db_grid == null:
		return
	for child in _db_grid.get_children():
		child.queue_free()
	_db_buttons.clear()
	var db := get_node_or_null("/root/DbManager")
	if db == null:
		return
	var snap: Variant = db.call("GetNodeSnapshot")
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var node_id: String = str(data.get("id", ""))
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(88, 44)
		btn.text = "%s\n%d金" % [str(data.get("display_name", "?")), int(data.get("cost_gold", 0))]
		btn.disabled = bool(data.get("purchased", false)) or not bool(data.get("can_purchase", false))
		if bool(data.get("purchased", false)):
			btn.text += "\n✓"
		btn.pressed.connect(_on_db_node_pressed.bind(node_id))
		_db_grid.add_child(btn)
		_db_buttons[node_id] = btn


func _refresh_star_tab() -> void:
	if _star_grid == null:
		return
	for child in _star_grid.get_children():
		child.queue_free()
	_star_buttons.clear()
	var meta := get_node_or_null("/root/MetaManager")
	if meta == null or not meta.has_method("GetStarChartSnapshot"):
		return
	var snap: Variant = meta.call("GetStarChartSnapshot")
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var node_id: String = str(data.get("id", ""))
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(88, 44)
		btn.text = "%s\n%d点" % [str(data.get("display_name", "?")), int(data.get("cost_star_points", 0))]
		btn.disabled = bool(data.get("purchased", false)) or not bool(data.get("can_purchase", false))
		if bool(data.get("purchased", false)):
			btn.text += "\n✓"
		btn.pressed.connect(_on_star_node_pressed.bind(node_id))
		_star_grid.add_child(btn)
		_star_buttons[node_id] = btn


func _update_footer() -> void:
	if _footer == null:
		return
	var prog := get_node_or_null("/root/ProgressionManager")
	var meta := get_node_or_null("/root/MetaManager")
	var gold := 0
	var points := 0
	if prog:
		var snap: Variant = prog.call("GetHudSnapshot")
		if typeof(snap) == TYPE_DICTIONARY:
			gold = int(snap.get("gold", 0))
	if meta:
		points = int(meta.get("StarChartPoints"))
	_footer.text = "金币 %d | 星图点 %d" % [gold, points]


func _on_db_node_pressed(node_id: String) -> void:
	var db := get_node_or_null("/root/DbManager")
	if db:
		db.call("TryPurchaseNode", node_id)
	refresh()


func _on_star_node_pressed(node_id: String) -> void:
	var meta := get_node_or_null("/root/MetaManager")
	if meta:
		meta.call("TryPurchaseStarNode", node_id)
	refresh()
