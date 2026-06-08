extends Control

## Cultivation popup: six meta-growth branches + star chart.

const BRANCH_KEYS := ["squad", "exp", "gold", "bag", "chest", "stats"]

@onready var _main_tab: TabBar = %MainTabBar
@onready var _growth_grid: GridContainer = %GrowthGrid
@onready var _star_grid: GridContainer = %StarGrid
@onready var _footer: Label = %FooterLabel
@onready var _warehouse_btn: Button = %WarehouseButton

var _growth_buttons: Dictionary = {}


func _ready() -> void:
	if _main_tab:
		_main_tab.tab_changed.connect(_on_main_tab_changed)
	if _warehouse_btn:
		_warehouse_btn.pressed.connect(_on_warehouse_pressed)
	call_deferred("refresh")


func refresh() -> void:
	_refresh_growth_tab(_current_branch())
	_refresh_star_tab()
	_update_footer()
	_on_main_tab_changed(_main_tab.current_tab if _main_tab else 0)


func _current_branch() -> String:
	if _main_tab == null or _main_tab.current_tab >= BRANCH_KEYS.size():
		return ""
	return BRANCH_KEYS[_main_tab.current_tab]


func _on_main_tab_changed(tab: int) -> void:
	var is_star := tab >= BRANCH_KEYS.size()
	if _growth_grid:
		_growth_grid.visible = not is_star
	if _star_grid:
		_star_grid.visible = is_star
	if _warehouse_btn:
		_warehouse_btn.visible = not is_star and _current_branch() == "bag"
	if not is_star:
		_refresh_growth_tab(_current_branch())


func _refresh_growth_tab(branch: String) -> void:
	if _growth_grid == null or branch.is_empty():
		return
	for child in _growth_grid.get_children():
		child.queue_free()
	_growth_buttons.clear()
	var db := get_node_or_null("/root/DbManager")
	if db == null:
		return
	var snap: Variant = db.call("GetNodeSnapshot", branch)
	if typeof(snap) != TYPE_ARRAY:
		return
	for entry in snap:
		var data: Dictionary = entry
		var node_id: String = str(data.get("id", ""))
		if node_id.ends_with("_root"):
			continue
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(96, 48)
		var cost: int = int(data.get("cost_gold", 0))
		btn.text = "%s\n%d金" % [str(data.get("display_name", "?")), cost]
		btn.disabled = bool(data.get("purchased", false)) or not bool(data.get("can_purchase", false))
		if bool(data.get("purchased", false)):
			btn.text += "\n✓"
		btn.pressed.connect(_on_growth_node_pressed.bind(node_id))
		_growth_grid.add_child(btn)
		_growth_buttons[node_id] = btn


func _refresh_star_tab() -> void:
	if _star_grid == null:
		return
	for child in _star_grid.get_children():
		child.queue_free()
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


func _on_growth_node_pressed(node_id: String) -> void:
	var db := get_node_or_null("/root/DbManager")
	if db:
		db.call("TryPurchaseNode", node_id)
	refresh()


func _on_star_node_pressed(node_id: String) -> void:
	var meta := get_node_or_null("/root/MetaManager")
	if meta:
		meta.call("TryPurchaseStarNode", node_id)
	refresh()


func _on_warehouse_pressed() -> void:
	var pm := get_node_or_null("/root/PopupManager")
	if pm and pm.has_method("open_popup"):
		pm.call("open_popup", 10)
