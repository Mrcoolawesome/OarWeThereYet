extends MarginContainer

@onready var lobby_name = $LobbyName
var lobby_id = 0

func initalize(given_lobby_id: int, given_lobby_name: String) -> void:
	lobby_name.text = given_lobby_name
	lobby_id = given_lobby_id

func _on_join_lobby_button_pressed() -> void:
	# join the lobby with the given id
	GlobalSignalServer.emit_signal("JoinGame", lobby_id)
