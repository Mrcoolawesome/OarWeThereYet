extends Node

var multiplayer_peer: SteamMultiplayerPeer = SteamMultiplayerPeer.new()
var _hosted_lobby_id = 0
var _max_lobby_members = 4

# we're just gonna hardcode the lobby name for now
const LOBBY_NAME = "BAD"
const LOBBY_MODE = "CoOP"

# player scene and test level scene
var player_scene = preload("res://scenes/player.tscn")
var test_level_scene = preload("res://scenes/demo_level.tscn")

# this gets the main scene and then get's the node named 'Level' under that main scene
@onready var main_root_scene = get_tree().current_scene
@onready var level_container = get_tree().current_scene.get_node_or_null("Level")

# we DO NEED THIS to make sure that only clients can join servers
var is_client = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# initalize steam
	Steam.steamInit(480, true)
	Steam.initRelayNetworkAccess() # start steam relay

	# connect the 'on_lobby_created' function to the lobby created signal
	Steam.lobby_created.connect(_on_lobby_created)
	Steam.lobby_joined.connect(_on_lobby_join)

func become_host():
	# create a public lobby with a max player count of 4
	Steam.createLobby(Steam.LOBBY_TYPE_PUBLIC, _max_lobby_members)
	# set SERVER relay to be enabled
	multiplayer_peer.server_relay = true
	multiplayer_peer.create_host()

	# set the current instance's peer to be the new multiplayer peer with 
	multiplayer.multiplayer_peer = multiplayer_peer

	# connect the signals to the callback functions to add a player and remove a player
	multiplayer.peer_connected.connect(_add_player_to_game)
	multiplayer.peer_disconnected.connect(_remove_player)

	# add the level first
	_add_level()

	# add the server's player to the game and set its id to 1
	_add_player_to_game(1)

func join_as_client(lobby_id):
	# connect the current instance's peer to the lobby given the lobbies id
	is_client = true
	Steam.joinLobby(lobby_id)

func _add_level():
	# only add the level if the current instance is the server
	if multiplayer.is_server():
		# load the level
		var test_level = load("res://scenes/test_level.tscn")
		level_container.add_child(test_level.instantiate())

'''
	this just prints their lobby id and then also sets the lobby metadata
'''
func _on_lobby_created(result: int, lobby_id):	
	if result == Steam.Result.RESULT_OK:
		# set the global id
		_hosted_lobby_id = lobby_id
		print(lobby_id)

		# make the lobby joinable (this is enabled by default)
		Steam.setLobbyJoinable(_hosted_lobby_id, true)

		# set lobby data parameters
		# setting metadata is just setting your own variables for the lobby, there's no specific parameters
		Steam.setLobbyData(_hosted_lobby_id, "name", LOBBY_NAME)
		Steam.setLobbyData(_hosted_lobby_id, "mode", LOBBY_MODE)

func _on_lobby_join(lobby_id : int, _permissions : int, _locked : bool, _response : int):
	# if they 
	if !is_client:
		return
	
	# get the lobby id
	var host_id = Steam.getLobbyOwner(lobby_id)

	# set the peer variable to be a new SteamMultiplayerPeer
	multiplayer_peer = SteamMultiplayerPeer.new()
	multiplayer_peer.server_relay = true # enable Steam relay

	# attempt to make a client for the given host/server
	var error = multiplayer_peer.create_client(host_id)
	if error != OK:
		print("Failed to create client: ", error)
		return
	
	# if all goes well connect the peer to godot's internal multiplayer api
	multiplayer.multiplayer_peer = multiplayer_peer

	# reset this 
	is_client = false

func list_lobbies():
	# filter the lobby by distance
	Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_WORLDWIDE)

	# filter by the name parameter we set so we don't see a flood of servers that are hosted on the test server
	Steam.addRequestLobbyListStringFilter("name", "BAD", Steam.LOBBY_COMPARISON_EQUAL)
	Steam.requestLobbyList()

func _add_player_to_game(id: int):	
	# the Level container should always be there, just need to check if it has a level actually loaded (as a child) in it
	if level_container.get_child_count() > 0:
			# Get the actual map node (the first child of the Level container)
			var current_map = level_container.get_child(0)

			# instantiate a new player object
			var player = player_scene.instantiate()
			player.name = str(id) # set the name of the player to be their client id
			
			# Add the player as a child of the LOADED MAP
			current_map.add_child(player, true) # that second boolean is important because it keeps the name of the player to be the one that we set for it
	else:
			print("Error: Cannot spawn player. No map is currently loaded in the Level node.")

'''
	find the player we're looking to remove, and remove their instance.
'''
func _remove_player(id : int):		
	# "true" means recursive (search inside children's children)
	# "false" means it doesn't need to be owned by this node (safer for spawned objects)
	var player_node = self.find_child(str(id), true, false)
	
	if player_node:
		player_node.queue_free()
	else:
		print("Could not find player with ID: ", id)
