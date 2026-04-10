extends Node

var multiplayer_peer: SteamMultiplayerPeer = SteamMultiplayerPeer.new()
var _hosted_lobby_id = 0
var _max_lobby_members = 4

# we're just gonna hardcode the lobby name for now
var LOBBY_NAME = "gaming"

# player scene and level scene path
var player_scene = preload("res://scenes_scripts/player/player.tscn")
const LEVEL_SCENE_PATH = "res://scenes_scripts/levels/stylized-map/stylized-map.tscn"

# global values to load the level in
var loading: bool = false
var player_id: int = -1

# this gets the main scene and then get's the node named 'Level' under that main scene
@onready var main_root_scene = get_tree().current_scene
@onready var level_container = get_tree().current_scene.get_node_or_null("Level")

# we DO NEED THIS to make sure that only clients can join servers
var is_client = false
var is_hosting: bool = false
var pending_host_id: int = 0

const PLAYER_COLORS = ["#B4B7FD", "#F9D412", "#EAF6FF", "#FCC6E2"]

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
  # initalize steam
  Steam.steamInit(4563080, true)
  Steam.initRelayNetworkAccess() # start steam relay

  # connect the 'on_lobby_created' function to the lobby created signal
  Steam.lobby_created.connect(_on_lobby_created)
  Steam.lobby_joined.connect(_on_lobby_join)
  Steam.join_requested.connect(_on_lobby_join)

func _process(_delta: float) -> void:
  Steam.run_callbacks()

  # level loading logic
  if (loading):
    var progress = []
    var status = ResourceLoader.load_threaded_get_status(LEVEL_SCENE_PATH, progress)
    
    if status == ResourceLoader.ThreadLoadStatus.THREAD_LOAD_LOADED:
      loading = false
      multiplayer_peer.server_relay = true
      # initialize voice
      ProxChat.initialize_voice()
      _add_level() # The map is officially in the SceneTree now!
      GlobalSignalServer.GoToMainMenu.connect(cleanup_network_state)

      # --- NETWORK INITIALIZATION ---
      if is_hosting:
        # Now that the host has the map loaded, start the server
        multiplayer_peer.create_host()
        multiplayer.multiplayer_peer = multiplayer_peer

        multiplayer.peer_connected.connect(_add_player_to_game)
        multiplayer.peer_disconnected.connect(_remove_player)
        
        _add_player_to_game(1) # Spawn the host
        is_hosting = false

      elif pending_host_id != 0:
        # Check if the host is still there before connecting Godot multiplayer
        var current_owner = Steam.getLobbyOwner(_hosted_lobby_id)
        if current_owner == 0 or current_owner != pending_host_id:
          print("Host is no longer available.")
          pending_host_id = 0
          _on_server_disconnected()
          GlobalSignalServer.emit_signal("DoneLoadingMap")
          return

        # Now that the client has the map loaded, connect to the server
        print("Setting multiplayer peer for client")
        multiplayer_peer.create_client(pending_host_id)
        multiplayer.multiplayer_peer = multiplayer_peer    
        pending_host_id = 0
        multiplayer.server_disconnected.connect(_on_server_disconnected)


      GlobalSignalServer.emit_signal("DoneLoadingMap")

func become_host(is_public: bool, lobby_name: String):
  is_hosting = true
  # create a public or private lobby
  if is_public:
    Steam.createLobby(Steam.LOBBY_TYPE_PUBLIC, _max_lobby_members)
  else:
    Steam.createLobby(Steam.LOBBY_TYPE_FRIENDS_ONLY, _max_lobby_members)

  LOBBY_NAME = lobby_name if lobby_name != null else Steam.getPersonaName()
  
  # Start loading the map immediately. 
  # We will create the Godot host AFTER it loads.
  _request_level_load()

func join_as_client(lobby_id):
  is_client = true
  Steam.joinLobby(lobby_id)
  _hosted_lobby_id = lobby_id

func _on_lobby_join(lobby_id : int, _permissions : int, _locked : bool, _response : int):
  if !is_client:
    return
  
  # Save the host ID for later, but DON'T connect Godot multiplayer yet!
  pending_host_id = Steam.getLobbyOwner(lobby_id)
  is_client = false
  
  # Start loading the map
  _request_level_load()

func _request_level_load() -> void:
  ResourceLoader.load_threaded_request(LEVEL_SCENE_PATH)
  loading = true

func _add_level():
  # load the level
  var level: Resource = ResourceLoader.load_threaded_get(LEVEL_SCENE_PATH)
  var test_level = level.instantiate()
  test_level.set("SaveSlot", GlobalVariables.save_slot)
  level_container.add_child(test_level)

'''
  this just prints their lobby id and then also sets the lobby metadata
'''
func _on_lobby_created(result: int, lobby_id):  
  if result == Steam.Result.RESULT_OK:
    # set the global id
    _hosted_lobby_id = lobby_id

    # make the lobby joinable (this is enabled by default)
    Steam.setLobbyJoinable(_hosted_lobby_id, true)

    # setting metadata is just setting your own variables for the lobby, there's no specific parameters
    Steam.setLobbyData(_hosted_lobby_id, "name", LOBBY_NAME)

func _add_player_to_game(id: int):  
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

    # Pick a color based on their ID to ensure variety
    var color_hex = PLAYER_COLORS[id % PLAYER_COLORS.size()]
    # Send an RPC call ONLY to the client who owns this player node
    rpc_id(id, "_receive_player_color", color_hex)

    # --- THE FIX: Fetch their actual Steam name dynamically ---
    rpc_id(id, "_fetch_and_apply_gamertag")

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
func _fetch_and_apply_gamertag() -> void:
  # Because this is running on the specific client's machine, 
  # getPersonaName() will grab THEIR local Steam profile name!
  var my_steam_name = Steam.getPersonaName()
  
  # Emit the global signal so their local C# Player script catches it
  GlobalSignalServer.emit_signal("AssignGamertag", my_steam_name)

@rpc("authority", "reliable", "call_local")
func _receive_player_color(color_hex: String) -> void:
  # Emit your global signal for the C# script to catch
  GlobalSignalServer.emit_signal("AssignPlayerColor", color_hex)

'''
  Runs on host when a client leaves
'''
func _remove_player(id : int):    
  var active_level = level_container.get_children()[0];
  var player_node = active_level.get_node_or_null(str(id)) # recursively looks for the player
  
  if player_node:
    # Player drops item if they're holding it
    var arm_node = player_node.get_node_or_null("Head/ArmNode")
    if arm_node:
      arm_node.DropItem(arm_node.global_position, Vector3.ZERO)

    # Free player
    player_node.queue_free()
  else:
    print("Could not find player with ID: ", id)

'''
  Runs on client when kicked by host
'''
func _on_server_disconnected():
  cleanup_network_state()
  GlobalSignalServer.emit_signal("GoToMainMenu")

func cleanup_network_state() -> void:
  print("Cleaning up network state")
  ProxChat.stop_voice()
  print("stopped voice")

  # 1. SHUT DOWN NETWORK FIRST (Stop incoming RPCs/Signals)
  multiplayer.peer_connected.disconnect(_add_player_to_game)
  multiplayer.peer_disconnected.disconnect(_remove_player)
  multiplayer.server_disconnected.disconnect(_on_server_disconnected)
  GlobalSignalServer.GoToMainMenu.disconnect(cleanup_network_state)

  if multiplayer.multiplayer_peer != null:
    multiplayer.multiplayer_peer.close()
  multiplayer.multiplayer_peer = null

  Steam.leaveLobby(_hosted_lobby_id)