extends Node

var peer : SteamMultiplayerPeer
var player_scene = preload("res://scenes/player.tscn")
var is_host: bool = false
var is_joining : bool = false
var main_scene = preload("res://scenes/main.tscn")

func _ready():
	print("Steam Initialized: ", Steam.steamInit(480, true))
	Steam.initRelayNetworkAccess()
	Steam.lobby_created.connect(_on_lobby_created)
	Steam.lobby_joined.connect(_on_lobby_join)

func host_lobby():
	Steam.createLobby(Steam.LobbyType.LOBBY_TYPE_PUBLIC, 16)
	is_host = true

func _on_lobby_created(result: int, lobby_id: int):
	if result == Steam.Result.RESULT_OK:
		print("Lobby created successfully")
		
		peer = SteamMultiplayerPeer.new()
		peer.server_relay = true
		peer.create_host() # Port is optional for Steam relay
		
		multiplayer.multiplayer_peer = peer
		
		# Connect signals
		multiplayer.peer_connected.connect(_add_player)
		multiplayer.peer_disconnected.connect(_remove_player)
		
		# Add the local host player first
		_add_player() 
		
		# CHANGE SCENE LAST
		get_tree().change_scene_to_packed(main_scene)

func join_lobby(lobby_id : int):
	is_joining = true
	Steam.joinLobby(lobby_id)

func _on_lobby_join(lobby_id : int, permissions : int, locked : bool, response : int):
	if !is_joining:
		return
	
	# Check if the host ID is valid
	var host_id = Steam.getLobbyOwner(lobby_id)
	if host_id == 0:
		print("Failed to get Lobby Owner. Retrying...")
		# Optional: Add a small timer or loop here to wait for data sync
		return

	peer = SteamMultiplayerPeer.new()
	peer.server_relay = true
	
	# Try to create the client
	var error = peer.create_client(host_id)
	if error != OK:
		print("Failed to create Steam client: ", error)
		return
		
	multiplayer.multiplayer_peer = peer
	
	is_joining = false
	
	# CLIENT ALSO NEEDS TO CHANGE SCENE
	get_tree().change_scene_to_packed(main_scene)

func _add_player(id : int = 1):
	var player = player_scene.instantiate()
	player.name = str(id)
	get_tree().current_scene.add_child(player)

func _remove_player(id : int):
	if !self.has_node(str(id)):
		return
		
	self.get_node(str(id)).queue_free()


func _on_host_button_pressed() -> void:
	host_lobby()


func _on_join_button_pressed(prompt: int) -> void:
	join_lobby(prompt)
