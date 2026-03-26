extends Node

var multiplayer_peer: SteamMultiplayerPeer = SteamMultiplayerPeer.new()
var _hosted_lobby_id = 0
var _max_lobby_members = 4

# we're just gonna hardcode the lobby name for now
var LOBBY_NAME = "gaming"

# player scene and level scene path
var player_scene = preload("res://scenes_scripts/player/player.tscn")
const LEVEL_SCENE_PATH = "res://scenes_scripts/levels/stylized-map/stylized-map.tscn"
var level_name = "DemoLevel"

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

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
  # initalize steam
  Steam.steamInit(480, true)
  Steam.initRelayNetworkAccess() # start steam relay

  # initialize voice
  ProxChat.initialize_voice()

  # connect the 'on_lobby_created' function to the lobby created signal
  Steam.lobby_created.connect(_on_lobby_created)
  Steam.lobby_joined.connect(_on_lobby_join)

func _process(_delta: float) -> void:
  Steam.run_callbacks()

  # level loading logic
  if (loading):
    var progress = []
    var status = ResourceLoader.load_threaded_get_status(LEVEL_SCENE_PATH, progress)
    
    if status == ResourceLoader.ThreadLoadStatus.THREAD_LOAD_LOADED:
      loading = false
      _add_level() # The map is officially in the SceneTree now!

      # --- NETWORK INITIALIZATION ---
      if is_hosting:
        # Now that the host has the map loaded, start the server
        multiplayer_peer.server_relay = true
        multiplayer_peer.create_host()
        multiplayer.multiplayer_peer = multiplayer_peer
        
        multiplayer.peer_connected.connect(_add_player_to_game)
        multiplayer.peer_disconnected.connect(_remove_player)
        
        _add_player_to_game(1) # Spawn the host
        is_hosting = false

      elif pending_host_id != 0:
        # Now that the client has the map loaded, connect to the server
        multiplayer_peer = SteamMultiplayerPeer.new()
        multiplayer_peer.server_relay = true 
        var error = multiplayer_peer.create_client(pending_host_id)
        
        if error == OK:
          multiplayer.multiplayer_peer = multiplayer_peer
        else:
          print("Failed to create client: ", error)
          
        pending_host_id = 0
      # ------------------------------

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

func _on_lobby_join(lobby_id : int, _permissions : int, _locked : bool, _response : int):
  if !is_client:
    return
  
  # Save the host ID for later, but DON'T connect Godot multiplayer yet!
  pending_host_id = Steam.getLobbyOwner(lobby_id)
  is_client = false
  
  # Start loading the map
  _request_level_load()

func _request_level_load() -> void:
  await get_tree().process_frame 
  await get_tree().process_frame 
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
  ProxChat.stop_voice()

  var active_level = level_container.get_children()[0];
  var player_node = active_level.get_node_or_null(str(id)) # recursively looks for the player
  
  if player_node:
    # Player drops item if they're holding it
    var arm_node = player_node.get_node_or_null("Head/ArmNode")
    if arm_node:
      arm_node.DropItem(Vector3.ZERO)

    # Free player
    player_node.queue_free()
  else:
    print("Could not find player with ID: ", id)
