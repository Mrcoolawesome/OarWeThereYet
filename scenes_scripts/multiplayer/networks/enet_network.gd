extends Node

const SERVER_PORT = 8080
const SERVER_IP = "127.0.0.1"

var multiplayer_peer: ENetMultiplayerPeer = ENetMultiplayerPeer.new()
var player_scene = preload("res://scenes_scripts/player/player.tscn")
const LEVEL_SCENE_PATH = "res://scenes_scripts/levels/stylized-map/stylized-map.tscn"

# global values to load the level in
var loading: bool = false
var player_id: int = -1

# State tracking to delay network connection until AFTER loading
var is_hosting: bool = false
var is_joining: bool = false

# this gets the main scene and then get's the node named 'Level' under that main scene
@onready var level_container = get_tree().current_scene.get_node_or_null("Level")

const PLAYER_COLORS = ["#B4B7FD", "#F9D412", "#EAF6FF", "#FCC6E2"]
var assigned_player_colors: Dictionary = {}

func _assign_unique_color(target_player_id: int) -> String:
  if assigned_player_colors.has(target_player_id):
    return assigned_player_colors[target_player_id]

  var used_colors: Array = assigned_player_colors.values()
  for color in PLAYER_COLORS:
    if !used_colors.has(color):
      assigned_player_colors[target_player_id] = color
      return color

  # Fallback (should never hit with max 4 players/colors).
  var fallback_color: String = PLAYER_COLORS[target_player_id % PLAYER_COLORS.size()]
  assigned_player_colors[target_player_id] = fallback_color
  return fallback_color

func _sync_all_player_colors_to_peer(target_peer_id: int) -> void:
  for existing_player_id in assigned_player_colors.keys():
    rpc_id(target_peer_id, "_receive_player_color", existing_player_id, assigned_player_colors[existing_player_id])

# become host for ENet server
func become_host():
  is_hosting = true
  # Start the background loading. We will create the server AFTER it finishes.
  _request_level_load()

# this is the function that's called by the multiplayer manager
func join_as_client(_lobby_id):
  is_joining = true
  # Start the background loading. We will create the client AFTER it finishes.
  _request_level_load()

func _request_level_load() -> void:
  # need to let the loading screen show up, so wait for like two frames
  await get_tree().process_frame 
  await get_tree().process_frame 
  # start the loading process
  ResourceLoader.load_threaded_request(LEVEL_SCENE_PATH)
  loading = true

func _process(_delta: float) -> void:
  # level loading logic
  if (loading):
    # for some reason the progress is returned as a 1 element array with the percentage completed
    var progress = []
    var status = ResourceLoader.load_threaded_get_status(LEVEL_SCENE_PATH, progress)
    
    if status == ResourceLoader.ThreadLoadStatus.THREAD_LOAD_LOADED:
      # done loading
      loading = false
      # initialize voice before players start spawning in
      ProxChat.initialize_voice()
      GlobalSignalServer.GoToMainMenu.connect(cleanup_network_state)
      # actually load the level in
      _add_level()

      # --- NETWORK INITIALIZATION ---
      if is_hosting:
        # Now that the map is fully loaded, start the server
        multiplayer_peer.create_server(SERVER_PORT)
        multiplayer.multiplayer_peer = multiplayer_peer
        
        # connect the callback functions
        multiplayer.peer_connected.connect(_add_player_to_game)
        multiplayer.peer_disconnected.connect(_remove_player)
        
        # add the host player to the game
        _add_player_to_game(1) 
        is_hosting = false
        
      elif is_joining:
        # Now that the map is fully loaded, connect to the server
        multiplayer_peer.create_client(SERVER_IP, SERVER_PORT)
        multiplayer.multiplayer_peer = multiplayer_peer
        is_joining = false
        multiplayer.server_disconnected.connect(_on_server_disconnected)
      # ------------------------------

      # we can remove the main menu ui now
      GlobalSignalServer.emit_signal("DoneLoadingMap")

func _add_level():
  # load the level
  var level: Resource = ResourceLoader.load_threaded_get(LEVEL_SCENE_PATH)
  var test_level = level.instantiate()
  test_level.set("SaveSlot", GlobalVariables.save_slot)
  level_container.add_child(test_level)

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

    # Send an RPC call ONLY to the client who owns this player node
    rpc_id(id, "_receive_gamertag", str(id))

    # Pick a unique color for this player for the current match.
    var color_hex = _assign_unique_color(id)
    # Broadcast this player's color to everyone.
    rpc("_receive_player_color", id, color_hex)

    # Also send a full color snapshot to the joining peer so all existing players are correct.
    _sync_all_player_colors_to_peer(id)
    
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

# "authority" means only the server can call this. 
# "call_local" ensures it also runs for the Host's own player.
@rpc("authority", "reliable", "call_local")
func _receive_gamertag(gamertag: String) -> void:
  # Emit your global signal for the C# script to catch
  GlobalSignalServer.emit_signal("AssignGamertag", gamertag)

@rpc("authority", "reliable", "call_local")
func _receive_player_color(target_player_id: int, color_hex: String) -> void:
  # Emit your global signal for the C# script to catch
  GlobalSignalServer.emit_signal("AssignPlayerColor", target_player_id, color_hex)

'''
  find the player we're looking to remove, and remove their instance.
'''
func _remove_player(id : int):    
  # recursively looks for the player
  var active_level = level_container.get_children()[0];
  var player_node = active_level.get_node_or_null(str(id)) # recursively looks for the player
  
  if player_node:
    # Player drops item if they're holding it
    var arm_node = player_node.get_node_or_null("Head/ArmNode")
    if arm_node:
      arm_node.DropItem(arm_node.global_position, Vector3.ZERO)

    # Free player
    player_node.queue_free()
    assigned_player_colors.erase(id)
  else:
    print("Could not find player with ID: ", id)

'''
  Runs on client when kicked by host, or whenever the peer drops.
'''
func _on_server_disconnected():
  cleanup_network_state()
  GlobalSignalServer.emit_signal("GoToMainMenu")

func cleanup_network_state() -> void:
  print("Cleaning up network state")
  ProxChat.stop_voice()
  print("stopped voice")

  loading = false
  is_hosting = false
  is_joining = false

  if multiplayer.peer_connected.is_connected(_add_player_to_game):
    multiplayer.peer_connected.disconnect(_add_player_to_game)

  if multiplayer.peer_disconnected.is_connected(_remove_player):
    multiplayer.peer_disconnected.disconnect(_remove_player)

  if multiplayer.server_disconnected.is_connected(_on_server_disconnected):
    multiplayer.server_disconnected.disconnect(_on_server_disconnected)

  if GlobalSignalServer.GoToMainMenu.is_connected(cleanup_network_state):
    GlobalSignalServer.GoToMainMenu.disconnect(cleanup_network_state)

  if multiplayer.multiplayer_peer != null:
    multiplayer.multiplayer_peer.close()

  multiplayer.multiplayer_peer = null
  assigned_player_colors.clear()
