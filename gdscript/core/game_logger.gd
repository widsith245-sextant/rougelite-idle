class_name GameLoggerGd
extends RefCounted

## Thin GDScript wrapper for C# GameLogger autoload.


static func combat_info(message: String) -> void:
	var logger: Node = Engine.get_main_loop().root.get_node_or_null("/root/GameLogger")
	if logger and logger.has_method("LogCombat"):
		logger.call("LogCombat", message)


static func combat_warn(message: String) -> void:
	var logger: Node = Engine.get_main_loop().root.get_node_or_null("/root/GameLogger")
	if logger and logger.has_method("LogCombatWarn"):
		logger.call("LogCombatWarn", message)


static func loot_info(message: String) -> void:
	var logger: Node = Engine.get_main_loop().root.get_node_or_null("/root/GameLogger")
	if logger and logger.has_method("LogLoot"):
		logger.call("LogLoot", message)
