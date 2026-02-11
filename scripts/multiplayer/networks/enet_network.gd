extends Node

const SERVER_PORT = 8080
const SERVER_IP = "127.0.0.1"

var multiplayer_peer: ENetMultiplayerPeer = ENetMultiplayerPeer.new()
var player_scene = preload("res://scenes/player.tscn")
var test_level_scene = preload("res://scenes/test_level.tscn")

# this gets the main scene and then get's the node named 'Level' under that main scene
@onready var main_root_scene = get_tree().current_scene
@onready var level_container = get_tree().current_scene.get_node_or_null("Level")

# become host for ENet server
func become_host():
	# create the server and set the peer of this instance to be the server peer
	multiplayer_peer.create_server(SERVER_PORT)
	multiplayer.multiplayer_peer = multiplayer_peer

	# connect the the callback functions to run when a signal is sent for when a player disconnects or connects
	multiplayer.peer_connected.connect(_add_player_to_game)
	multiplayer.peer_disconnected.connect(_remove_player)

	# add the level first
	_add_level()

	# add the server host's player and set its id to 1
	_add_player_to_game(1)

# this is the function that's called by the multiplayer manager
func join_as_client(lobby_id):
	# set the peer of the current instance to be a peer
	multiplayer_peer.create_client(SERVER_IP, SERVER_PORT)
	multiplayer.multiplayer_peer = multiplayer_peer

func _add_level():
	# only add the level if the current instance is the server
	if multiplayer.is_server():
		# load the level
		var test_level = test_level_scene.instantiate()
		level_container.add_child(test_level)

func _add_player_to_game(id: int):
	# this is where we have to add them properly to the level	
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
