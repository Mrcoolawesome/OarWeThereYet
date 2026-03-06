extends Control

@onready var reset_button: Button = $MarginContainer/VBoxContainer/ResetGameButton

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# only the host can see the reset game button
	reset_button.visible = multiplayer.is_server()


# respawns player onto the boat
func _on_respawn_player_button_pressed() -> void:
	GlobalSignalServer.emit_signal("RespawnPlayer");

# resets the whole game, only the host should be able to do this
func _on_reset_game_button_pressed() -> void:
	GlobalSignalServer.emit_signal("ResetLevel");



func _on_load_button_pressed() -> void:
	GlobalSignalServer.emit_signal("LoadGame");


func _on_save_button_pressed() -> void:
	GlobalSignalServer.emit_signal("SaveGame");


