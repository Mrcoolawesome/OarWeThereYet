extends Node

@onready var main_menu_ui = $MainMenu

func _ready():
	# remove the main menu when the player either joins a game or hosts a game
	GlobalSignalServer.JoinGame.connect(remove_main_menu_ui)
	GlobalSignalServer.HostGame.connect(remove_main_menu_ui)

# we don't use lobby_id so we prefix it with an underscore which gdscript likes i guess
func remove_main_menu_ui(_lobby_id):
	main_menu_ui.queue_free()