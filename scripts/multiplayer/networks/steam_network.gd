extends Node

var player_scene = preload("res://scenes/player.tscn")
var multiplayer_peer: SteamMultiplayerPeer = SteamMultiplayerPeer.new()
var _hosted_lobby_id = 0
var _max_lobby_members = 4

# we're just gonna hardcode the lobby name for now
const LOBBY_NAME = "BAD"
const LOBBY_MODE = "CoOP"

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# connect the 'on_lobby_created' function to the lobby created signal
	multiplayer_peer.lobby_created.connect(_on_lobby_created)

func become_host():
	# create a public lobby with a max player count of 4
	Steam.createLobby(Steam.LOBBY_TYPE_PUBLIC, _max_lobby_members)
	multiplayer_peer.create_host()

	# set the current instance's peer to be the new multiplayer peer with 
	multiplayer.multiplayer_peer = multiplayer_peer

	# connect the signals to the callback functions to add a player and remove a player
	multiplayer.peer_connected.connect(_add_player_to_game)
	multiplayer.peer_disconnected.connect(_remove_player)

	# add the server's player to the game and set its id to 1
	_add_player_to_game(1)

func join_as_client(lobby_id):
	# connect the current instance's peer to the lobby given the lobbies id
	multiplayer_peer.connect_to_lobby(lobby_id)

	# set the current instance's peer to be the multiplayer peer that's connected to the lobby
	multiplayer.multiplayer_peer = multiplayer_peer

func _on_lobby_created(connect: int, lobby_id):	
	# connect == 1 means OK | connect == 2 means result failed | connect == 16 means result timeout
	if connect == 1:
		# set the global id
		_hosted_lobby_id = lobby_id

		# make the lobby joinable (this is enabled by default)
		Steam.setLobbyJoinable(_hosted_lobby_id, true)

		# set lobby data parameters
		# setting metadata is just setting your own variables for the lobby, there's no specific parameters
		Steam.setLobbyData(_hosted_lobby_id, "name", LOBBY_NAME)
		Steam.setLobbyData(_hosted_lobby_id, "mode", LOBBY_MODE)

func list_lobbies():
	# filter the lobby by distance
	Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_WORLDWIDE)

	# filter by the name parameter we set so we don't see a flood of servers that are hosted on the test server
	Steam.addRequestLobbyListStringFilter("name", "BAD", Steam.LOBBY_COMPARISON_EQUAL)
	Steam.requestLobbyList()

func _add_player_to_game(id: int):
	# this is where we have to add them properly to the level
	var level_container = get_node_or_null("Level")
	
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