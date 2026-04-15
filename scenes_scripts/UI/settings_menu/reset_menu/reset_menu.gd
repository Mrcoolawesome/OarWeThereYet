extends Control

@onready var reset_button: Button = $MarginContainer/VBoxContainer2/ResetGameButton

signal back_button_pressed

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# only the host can see the reset game button
	reset_button.visible = multiplayer.is_server()

func _on_back_button_pressed() -> void:
	back_button_pressed.emit()


# respawns player onto the boat
func _on_respawn_player_button_pressed() -> void:
	GlobalSignalServer.emit_signal("RespawnPlayer", multiplayer.get_unique_id());
	back_button_pressed.emit()

# resets the whole game, only the host should be able to do this
func _on_reset_game_button_pressed() -> void:
	GlobalSignalServer.emit_signal("ResetLevel");
	back_button_pressed.emit()
