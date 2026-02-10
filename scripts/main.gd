extends Node

var peer : SteamMultiplayerPeer
var player_scene = preload("res://scenes/player.tscn")
var is_host: bool = false
var is_joining : bool = false
var main_scene = preload("res://scenes/main.tscn")

'''
	this connects everything together.
	it connects the function that turns steam relay on, and 
'''
func _ready():
	print("Steam Initialized: ", Steam.steamInit(480, true))
	Steam.initRelayNetworkAccess() # this just makes the lobby and initalized Steam relay
	Steam.lobby_created.connect(_on_lobby_created)
	Steam.lobby_joined.connect(_on_lobby_join)

'''
	these two functions are connected to the signals emitted by the main menu ui
'''
func _on_main_menu_host_requested() -> void:
	host_lobby()

func _on_main_menu_join_requested(lobby_id: Variant) -> void:
	join_lobby(lobby_id)

	# remove the main menu ui after joining the server
	$MainMenu.queue_free()

'''
	this is ran when the host button is ran, and then the '_on_lobby_created' function is ran
	by the signal connected in the '_ready' function seen at the top of this file.
'''
func host_lobby():
	Steam.createLobby(Steam.LobbyType.LOBBY_TYPE_PUBLIC, 16)
	is_host = true

'''
	this does NOT create the lobby, it does all the post-lobby creation stuff.
	this is triggered WHEN we create a lobby
'''
func _on_lobby_created(result: int, lobby_id: int):
	if result == Steam.Result.RESULT_OK:
		print(lobby_id)
		
		# make the global peer variable a new steam multiplayer peer
		peer = SteamMultiplayerPeer.new()
		peer.server_relay = true # make sure that peer variable has Steam relay enabled
		peer.create_host() # this makes the global peer variable a host/server for P2P connections
		
		# this connects the godot internal multiplayer stuff with the new Steam peer
		multiplayer.multiplayer_peer = peer
		
		# signals (from godot's multiplayer api) that get triggered when a player connecs or disconnects from the multiplayer server
		multiplayer.peer_connected.connect(_add_player)
		multiplayer.peer_disconnected.connect(_remove_player)
		
		# instantiate the level scene
		_add_level() # this MUST happen before adding the player

		# add the local player (local meaning whatever machine is running this code)
		_add_player() 

		# remove the main menu
		_remove_main_menu()

func join_lobby(lobby_id : int):
	is_joining = true
	Steam.joinLobby(lobby_id)

	# remove the main menu
	_remove_main_menu()

'''
	this is ran AFTER the client has joined a lobby
'''
func _on_lobby_join(lobby_id : int, _permissions : int, _locked : bool, _response : int):
	# global boolean that acts like a lock to make sure that we're actually joining a lobby or not
	if !is_joining:
		return
	
	# get the lobby id
	var host_id = Steam.getLobbyOwner(lobby_id)

	# set the peer variable to be a new SteamMultiplayerPeer
	peer = SteamMultiplayerPeer.new()
	peer.server_relay = true # enable Steam relay

	# attempt to make a client for the given host/server
	var error = peer.create_client(host_id)
	if error != OK:
		print("Failed to create client: ", error)
		return
	
	# if all goes well connect the peer to godot's internal multiplayer api
	multiplayer.multiplayer_peer = peer
	
	print("joined")
	is_joining = false # we're done joining so record that

'''
	we want to add the player to the map node that's a child of the 'Level' node
'''
func _add_player(id : int = 1):
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
	this needs to be changed in the future probably to be more modular in terms of what map/level should be loaded.
	it loads the level if and ONLY if the current device is the server.
		we do it like this because the server will make a new level, and it will be replicated everywhere else with the MultiplayerSpanwer in the 
		main root node. 
'''
func _add_level():
	if multiplayer.is_server():
		var newLevel = load("res://scenes/test_level.tscn")
		$Level.add_child(newLevel.instantiate())

'''
	just removes the main menu
'''
func _remove_main_menu():
	$MainMenu.queue_free()

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
