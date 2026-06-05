class_name CombatCoords
extends RefCounted

## Shared combat ↔ canvas coordinate helpers (see docs/UI_COORDINATES.md).

const ENEMY_ANCHOR_X := 340.0
const FEET_OFFSET_Y := -32.0
const ANCHOR_ABOVE_FEET := -20.0
const HUD_DAMAGE_OFFSET := Vector2(-14, -30)

const VIEWPORT_SIZE := Vector2(400, 150)


static func ally_local_x(logic_x: float, ally_slots: Node2D) -> float:
	if ally_slots == null:
		return logic_x - 40.0
	return logic_x - ally_slots.global_position.x


static func enemy_local_x(logic_x: float, enemy_spawn: Node2D) -> float:
	if enemy_spawn == null:
		return logic_x - ENEMY_ANCHOR_X
	return logic_x - enemy_spawn.global_position.x


static func feet_world_y(anchor_node: Node2D) -> float:
	if anchor_node == null:
		return 58.0
	return anchor_node.global_position.y + FEET_OFFSET_Y


static func doll_anchor_pos(doll: Node2D) -> Vector2:
	if doll == null or not is_instance_valid(doll):
		return Vector2.ZERO
	if doll.has_method("get_anchor_position"):
		return doll.get_anchor_position()
	return doll.global_position + Vector2(0, ANCHOR_ABOVE_FEET)


static func doll_to_canvas_pos(doll: Node2D, _camera: Camera2D = null) -> Vector2:
	# Nested CanvasLayer HUD shares viewport pixel space with CombatWorld dolls;
	# get_anchor_position() already matches on-screen placement for this 400x150 layout.
	return doll_anchor_pos(doll)


static func canvas_pos_from_world(world_pos: Vector2, _camera: Camera2D = null) -> Vector2:
	return world_pos


static func hud_canvas_pos(doll: Node2D, camera: Camera2D) -> Vector2:
	return doll_to_canvas_pos(doll, camera) + HUD_DAMAGE_OFFSET


static func edge_dist(x1: float, r1: float, x2: float, r2: float) -> float:
	return maxf(0.0, absf(x2 - x1) - r1 - r2)


static func stop_x_ally_vs_enemy(
	ally_x: float,
	ally_r: float,
	enemy_x: float,
	enemy_r: float,
	atk_range: float,
) -> float:
	if ally_x < enemy_x:
		return enemy_x - (atk_range + ally_r + enemy_r)
	return enemy_x + (atk_range + ally_r + enemy_r)
