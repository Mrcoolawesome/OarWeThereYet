extends Node

'''
This needs to make a generic multiplayer network scene, and then just apply the functions on the generic multiplayer network scene.
We just have two multiplayer scenes - Steam and Enet which is the built-in godot multiplayer network.
	I'm doing this so that we can do local multiplayer testing with Enet since you need to machienes with seperate steam accounts to 
	test Steam multiplayer.
'''

# enum for choosing what multiplayer version we're on
enum MULTIPLAYER_NETWORK_TYPE {ENET, STEAM}

# default network type is the built-in one
var active_network_type: MULTIPLAYER_NETWORK_TYPE = MULTIPLAYER_NETWORK_TYPE.ENET;
# we have to make these scenes not scripts so that we can interact with them properly or something ig
var enet_network_scene := preload("res://scenes/multiplayer/networks/enet_network.tscn")
var steam_network_scene := preload("res://scenes/multiplayer/networks/enet_network.tscn")

# the network we're using
var active_network

# select the multiplayer network that should be started
func _set_active_network():
	if not active_network:

		# set the active network according to what network type we've decided on
		match active_network_type:
				MULTIPLAYER_NETWORK_TYPE.ENET:
					_build_active_network(enet_network_scene)
				MULTIPLAYER_NETWORK_TYPE.STEAM:
					_build_active_network(steam_network_scene)

func _build_active_network(active_network_scene):
	var network_scene_initalized = active_network_scene.instantiate()
	active_network = network_scene_initalized
	# run the player spawn thing here
	# add the network to the level
	add_child(active_network)

func become_host():
	_set_active_network() # set the active network and 'build' it
	active_network.become_host() # run the '_become_host()' function on the given network type

func join_as_client(lobby_id = 0):
	_set_active_network() # set the active network and built it
	active_network.join_as_client(lobby_id)

# this is to run the list lobbies implementation of the specific network type
func list_lobbies():
	_set_active_network() # set and build the network
	active_network.list_lobbies()
