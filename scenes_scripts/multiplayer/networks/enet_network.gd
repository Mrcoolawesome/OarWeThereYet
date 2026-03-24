extends Node

const SERVER_PORT = 8080
const SERVER_IP = "127.0.0.1"

var multiplayer_peer: ENetMultiplayerPeer = ENetMultiplayerPeer.new()
var player_scene = preload("res://scenes_scripts/player/player.tscn")
const LEVEL_SCENE_PATH = "res://scenes_scripts/levels/stylized-map/stylized-map.tscn"
var level_name = "DemoLevel"

# global values to load the level in
var loading: bool = false
var player_id: int = -1

# this gets the main scene and then get's the node named 'Level' under that main scene
@onready var level_container = get_tree().current_scene.get_node_or_null("Level")

# become host for ENet server
func become_host():
  # create the server and set the peer of this instance to be the server peer
  multiplayer_peer.create_server(SERVER_PORT)
  multiplayer.multiplayer_peer = multiplayer_peer

  # connect the the callback functions to run when a signal is sent for when a player disconnects or connects
  multiplayer.peer_connected.connect(_add_player_to_game)
  multiplayer.peer_disconnected.connect(_remove_player)

  # add the level and player in
  _load_game_and_player(1)

# this is the function that's called by the multiplayer manager
func join_as_client(_lobby_id):
  # load the map
  _load_game_and_player(-1)
  # set the peer of the current instance to be a peer
  multiplayer_peer.create_client(SERVER_IP, SERVER_PORT)
  multiplayer.multiplayer_peer = multiplayer_peer

func _load_game_and_player(given_player_id: int) -> void:
  # just need to set global variables so the loading stuff in '_process' can run
  loading = true
  player_id = given_player_id

func _process(_delta: float) -> void:
  # level loading logic
  if (loading):
    # for some reason the progress is returned as a 1 element array with the percentage completed
    var progress = []
    var status = ResourceLoader.load_threaded_get_status(LEVEL_SCENE_PATH, progress)

    # # don't really need this now that we don't have a progress bar, but i wanna keep it here in case we change our mind later and add one back
    # if status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
    #   loading_progress_value = progress[0] * 100.0
    
    if status == ResourceLoader.ThreadLoadStatus.THREAD_LOAD_LOADED:
      # done loading
      loading = false
      # actually load the level in
      _add_level()
      # put the player in the world now that it's done loading
      _add_player_to_game(player_id)
      # we can remove the main menu ui now
      GlobalSignalServer.emit_signal("DoneLoadingMap")

func _add_level():
  # load the level
  var level: Resource = ResourceLoader.load_threaded_get(LEVEL_SCENE_PATH)
  var test_level = level.instantiate()
  test_level.set("SaveSlot", GlobalVariables.save_slot)
  level_container.add_child(test_level)

func _request_level_load() -> void:
  ResourceLoader.load_threaded_request(LEVEL_SCENE_PATH)

func _add_player_to_game(id: int):
  # this is where we have to add them properly to the level	
  # the Level container should always be there, just need to check if it has a level actually loaded (as a child) in it
  if level_container.get_child_count() > 0:
    # Get the actual map node (the first child of the Level container)
    var current_map = level_container.get_child(0)

    # Ensure dynamic map setup (boat spawn, references, etc.) is done before adding players.
    if current_map.has_method("get") and current_map.has_signal("BoatReady"):
      if !current_map.get("IsBoatReady"):
        await current_map.BoatReady

    # instantiate a new player object
    var player = player_scene.instantiate()
    player.name = str(id) # set the name of the player to be their client id
    
    # Add the player as a child of the LOADED MAP
    current_map.add_child(player, true) # that second boolean is important because it keeps the name of the player to be the one that we set for it
    
    # assign the camera to the player for the terrain3d addon
    rpc_id(id, "_assign_camera", id)

  else:
    print("Error: Cannot spawn player. No map is currently loaded in the Level node.")

@rpc("authority", "reliable", "call_local")
func _assign_camera(id: int) -> void:
  var current_map = level_container.get_child(0)
  # Check if the player we just spawned is OUR local player
  if id == multiplayer.get_unique_id():
    # Grab the Terrain3D node from the map
    var terrain = current_map.get_node_or_null("Terrain3D")

    # Grab the Camera3D from the newly spawned player
    var camera_path = str(id) + "/Head/CameraContainer/Camera3D"
    var local_camera = current_map.get_node_or_null(camera_path)

    if terrain and local_camera:
      terrain.set_camera(local_camera)
      print("Successfully linked local camera to Terrain3D!")
    else:
      print("Could not link camera. Terrain or Camera node missing.")

'''
  find the player we're looking to remove, and remove their instance.
'''
func _remove_player(id : int):		
  # recursively looks for the player
  var active_level = level_container.get_node_or_null(level_name)
  var player_node = active_level.get_node_or_null(str(id))
  
  if player_node:
    # Player drops item if they're holding it
    var arm_node = player_node.get_node_or_null("Head/ArmNode")
    if arm_node:
      arm_node.DropItem(Vector3.ZERO)

    # Free player
    player_node.queue_free()
  else:
    print("Could not find player with ID: ", id)
