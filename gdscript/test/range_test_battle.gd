extends Node

## Loads melee+ ranged range test encounter on ready.

const TEST_ENCOUNTER := "res://data/tables/combat/encounter_range_test.json"


func _ready() -> void:
	var combat := get_node_or_null("/root/CombatManager")
	if combat and combat.has_method("ReloadEncounter"):
		combat.call("ReloadEncounter", TEST_ENCOUNTER)
