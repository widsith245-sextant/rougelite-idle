extends Node

## Simple mutex for HUD FX overlays (skill cast vs chest reveal).

var _busy := false


func try_acquire() -> bool:
	if _busy:
		return false
	_busy = true
	return true


func release() -> void:
	_busy = false


func is_busy() -> bool:
	return _busy
