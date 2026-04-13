extends Node

@onready var main_menu_ui = $MainMenu
@onready var menu_3d_scene = $MainMenuScene
@onready var level_container: Node = $Level

# preload the main menu scene
var main_menu_scene: PackedScene = preload("res://scenes_scripts/main_menu_scene/main_menu_scene.tscn")
var main_menu_ui_scene: PackedScene = preload("res://scenes_scripts/UI/main_menu/main_menu.tscn")

# --- OS WINDOW NOTIFICATIONS (ANTI-LAG FIX) ---
func _notification(what: int) -> void:
  # When the host alt-tabs away from the game
  if what == NOTIFICATION_APPLICATION_FOCUS_OUT:
    DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
    Engine.max_fps = 60 # Keep the game ticking at 60FPS for the network
      
  # When the host tabs back into the game
  elif what == NOTIFICATION_APPLICATION_FOCUS_IN:
    DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
    Engine.max_fps = 0 # 0 means uncapped (or set to your default max FPS)

func _ready() -> void:
  GlobalSignalServer.HostGameSteam.connect(become_host_steam)
  GlobalSignalServer.HostGameEnet.connect(become_host_enet)
  GlobalSignalServer.JoinGame.connect(join_lobby)
  GlobalSignalServer.GoToMainMenu.connect(back_to_main_menu)
  GlobalSignalServer.DoneLoadingMap.connect(remove_ui)

func remove_ui() -> void:
  main_menu_ui.queue_free()

func become_host_steam(is_public: bool, lobbyName: String):
  # get the new instance of the main menu ui and scene
  set_new_main_menu_instances()
  # remove the main menu ui
  menu_3d_scene.queue_free()
  $MultiplayerManager.become_host_steam(is_public, lobbyName) # call the generic become host function

func become_host_enet():
  # get the new instance of the main menu ui and scene
  set_new_main_menu_instances()
  # remove the main menu ui
  menu_3d_scene.queue_free()
  $MultiplayerManager.become_host() # call the generic become host function

func join_lobby(lobby_id):
  # get the new instance of the main menu ui and scene
  set_new_main_menu_instances()
  # remove the main menu ui
  menu_3d_scene.queue_free()
  $MultiplayerManager.join_as_client(lobby_id)

# this is what runs to delete the current level, and then
func back_to_main_menu() -> void:
  # get rid of the level within the level container
  for child in level_container.get_children():
    child.queue_free()

  # put the main menu level into the level container
  var main_menu_scene_instance: Node3D = main_menu_scene.instantiate()
  add_child(main_menu_scene_instance)

  # put the main menu back
  var main_menu_ui_instance: Control = main_menu_ui_scene.instantiate()
  add_child(main_menu_ui_instance) # add it back right under the main node

# sets the global main menu variables to be the new instances that we made
func set_new_main_menu_instances() -> void:
  main_menu_ui = $MainMenu
  menu_3d_scene = $MainMenuScene