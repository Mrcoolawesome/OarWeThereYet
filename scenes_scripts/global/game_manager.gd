extends Node

@onready var main_menu_ui = $MainMenu
@onready var menu_3d_scene = $Menu3DScene

# i want to make them the server host if they press the host button

func _ready() -> void:
  GlobalSignalServer.HostGameSteam.connect(become_host_steam)
  GlobalSignalServer.HostGameEnet.connect(become_host_enet)
  GlobalSignalServer.JoinGame.connect(join_lobby)

func become_host_steam(is_public: bool, lobbyName: String):
  # remove the main menu ui
  main_menu_ui.queue_free()
  menu_3d_scene.queue_free()
  $MultiplayerManager.become_host_steam(is_public, lobbyName) # call the generic become host function

func become_host_enet():
  # remove the main menu ui
  main_menu_ui.queue_free()
  menu_3d_scene.queue_free()
  $MultiplayerManager.become_host() # call the generic become host function

func join_lobby(lobby_id):
  # remove the main menu ui
  main_menu_ui.queue_free()
  menu_3d_scene.queue_free()
  $MultiplayerManager.join_as_client(lobby_id)
