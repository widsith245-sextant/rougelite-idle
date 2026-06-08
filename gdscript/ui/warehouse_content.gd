extends Control

## Warehouse popup: bidirectional swap with bag.

@onready var _wh_list: ItemList = %WarehouseList
@onready var _bag_list: ItemList = %BagList
@onready var _info: Label = %InfoLabel
@onready var _to_bag_btn: Button = %ToBagButton
@onready var _to_wh_btn: Button = %ToWarehouseButton

var _wh_index: int = -1
var _bag_index: int = -1


func _ready() -> void:
	if _to_bag_btn:
		_to_bag_btn.pressed.connect(_on_to_bag_pressed)
	if _to_wh_btn:
		_to_wh_btn.pressed.connect(_on_to_wh_pressed)
	if _wh_list:
		_wh_list.item_selected.connect(_on_wh_selected)
	if _bag_list:
		_bag_list.item_selected.connect(_on_bag_selected)
	call_deferred("refresh")


func refresh() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot == null:
		return
	if not bool(loot.get("WarehouseUnlocked")):
		if _info:
			_info.text = "仓库未解锁（养成→背包分支）"
		return
	var cap: int = int(loot.get("WarehouseCapacity"))
	var wh_snap: Variant = loot.call("GetWarehouseSnapshot")
	var bag_snap: Variant = loot.call("GetBagSnapshot") if loot.has_method("GetBagSnapshot") else []
	var wh_n := wh_snap.size() if typeof(wh_snap) == TYPE_ARRAY else 0
	if _wh_list:
		_wh_list.clear()
		if typeof(wh_snap) == TYPE_ARRAY:
			for entry in wh_snap:
				var row: Dictionary = entry
				_wh_list.add_item("%s Lv%d" % [str(row.get("display_name", "?")), int(row.get("item_level", 1))])
	if _bag_list:
		_bag_list.clear()
		if typeof(bag_snap) == TYPE_ARRAY:
			for entry in bag_snap:
				var row: Dictionary = entry
				_bag_list.add_item("%s Lv%d" % [str(row.get("display_name", "?")), int(row.get("item_level", 1))])
	if _info:
		_info.text = "仓库 %d/%d · 背包物品可存入仓库" % [wh_n, cap]


func _on_wh_selected(index: int) -> void:
	_wh_index = index
	_bag_index = -1


func _on_bag_selected(index: int) -> void:
	_bag_index = index
	_wh_index = -1


func _on_to_bag_pressed() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot and _wh_index >= 0:
		loot.call("MoveWarehouseItemToBag", _wh_index)
	refresh()


func _on_to_wh_pressed() -> void:
	var loot := get_node_or_null("/root/LootManager")
	if loot and _bag_index >= 0:
		loot.call("MoveBagItemToWarehouse", _bag_index)
	refresh()
