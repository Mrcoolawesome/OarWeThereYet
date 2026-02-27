extends MarginContainer

@onready var lobby_name = $LobbyName
var lobby_id = 0

func _instantiate(given_lobby_id: int, given_lobby_name: String) -> void:
	lobby_name = given_lobby_name
	lobby_id = given_lobby_id

func _on_join_lobby_button_pressed() -> void:
	# join the lobby with a given id
	pass # Replace with function body.