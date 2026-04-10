extends BasePopUpMenu

var lobby_entry = preload("res://scenes_scripts/UI/pop_up_menu/join_popup_menu/lobby_listing.tscn")
@onready var lobbies_container: VBoxContainer = $PanelContainer/VBoxContainer/ScrollContainer/MarginContainer/LobbiesContainer

var pending_lobbies: Array = []

# NEW: Variables to handle our 2-second refresh loop
var refresh_timer: Timer
var current_tab: int = 0

func _ready() -> void:
	Steam.lobby_match_list.connect(_on_lobby_match_list)
	Steam.lobby_data_update.connect(_on_lobby_data_update)
	
	# NEW: Create a timer in code so you don't have to add it in the editor
	refresh_timer = Timer.new()
	refresh_timer.wait_time = 2.0
	refresh_timer.autostart = false
	refresh_timer.timeout.connect(_on_refresh_timer_timeout)
	add_child(refresh_timer)

# this is called in the main_menu.gd script where it's triggered when the join button is pressed
# it is also triggered when the filter option has changed
func _look_for_lobbies(index: int) -> void:
	current_tab = index # Save the current tab so the timer knows what to refresh
	
	if index == 0:
		_get_friends_lobbies()
		refresh_timer.start() # NEW: Start the 2-second loop for friends
	elif index == 1: 
		refresh_timer.stop() # NEW: Stop the loop for public lobbies to prevent spamming the Steam API
		Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_CLOSE)
		Steam.requestLobbyList()

# NEW: The function that runs every 2 seconds
func _on_refresh_timer_timeout() -> void:
	if current_tab == 0:
		_get_friends_lobbies()

func _on_lobby_match_list(lobbies: Array) -> void:
	var existing_lobby_ids: Array = []
	
	# --- NEW FLICKER-FREE UI LOGIC ---
	# First, look at the lobbies currently on the screen.
	# If a lobby on screen is NO LONGER in the Steam list, delete it.
	for child in lobbies_container.get_children():
		if "lobby_id" in child:
			if not lobbies.has(child.lobby_id):
				child.queue_free() # The lobby closed, remove it
			else:
				existing_lobby_ids.append(child.lobby_id) # Save it so we don't duplicate it below

	pending_lobbies.clear()
	
	# go through each lobby found by steam
	for lobby_id in lobbies:
		# NEW: If it's already on the screen, skip it! No need to recreate it.
		if existing_lobby_ids.has(lobby_id):
			continue
			
		Steam.requestLobbyData(lobby_id)
		var lobby_name: String = Steam.getLobbyData(lobby_id, "name")

		if lobby_name != "":
			_add_lobby_to_ui(lobby_id, lobby_name)
		else:
			if not pending_lobbies.has(lobby_id):
				pending_lobbies.append(lobby_id)

func _on_lobby_data_update(lobby_id, _member_id, _success) -> void:
	if pending_lobbies.has(lobby_id):
		var lobby_name: String = Steam.getLobbyData(lobby_id, "name")
		
		if lobby_name != "":
			pending_lobbies.erase(lobby_id)
			_add_lobby_to_ui(lobby_id, lobby_name)

func _add_lobby_to_ui(lobby_id: int, lobby_name: String) -> void:
	var lobby_listing = lobby_entry.instantiate()
	lobbies_container.add_child(lobby_listing)
	lobby_listing.initalize(lobby_id, lobby_name)

func _get_friends_lobbies() -> void:
	var friends_lobbies: Array = []
	
	var num_friends: int = Steam.getFriendCount()
	
	for i in range(num_friends):
		var friend_steam_id: int = Steam.getFriendByIndex(i, Steam.FRIEND_FLAG_IMMEDIATE)
		var game_info: Dictionary = Steam.getFriendGamePlayed(friend_steam_id)
	
		if game_info.has("lobby") and game_info["lobby"] != 0:
			if game_info.has("id") and game_info["id"] == 4563080:
				var lobby_id = game_info["lobby"]
				
				# Prevents 4 friends in the same server from showing 4 duplicate servers
				if not friends_lobbies.has(lobby_id):
					friends_lobbies.append(lobby_id)
					
	_on_lobby_match_list(friends_lobbies)