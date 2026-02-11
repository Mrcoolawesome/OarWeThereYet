extends Node

@onready var main_menu_ui = $MainMenu

# i want to make them the server host if they press the host button

func _ready() -> void:
  GlobalSignalServer.HostGame.connect(become_host)
  GlobalSignalServer.JoinGame.connect(join_lobby)

func become_host():
  # remove the main menu ui
  main_menu_ui.queue_free()
  $MultiplayerManager.become_host() # call the generic become host function

func join_lobby(lobby_id):
  # remove the main menu ui
  main_menu_ui.queue_free()
  $MultiplayerManager.join_as_client(lobby_id)
