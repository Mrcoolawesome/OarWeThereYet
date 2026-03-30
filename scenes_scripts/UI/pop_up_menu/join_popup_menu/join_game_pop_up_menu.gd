extends BasePopUpMenu

var lobby_entry = preload("res://scenes_scripts/UI/pop_up_menu/join_popup_menu/lobby_listing.tscn")
@onready var lobbies_container: VBoxContainer = $PanelContainer/VBoxContainer/ScrollContainer/MarginContainer/LobbiesContainer

# NEW: Keep track of lobbies that are waiting for Steam to return their data
var pending_lobbies: Array = []

func _ready() -> void:
  # connect the lobby fetching logic to the steam api
  Steam.lobby_match_list.connect(_on_lobby_match_list)
  # NEW: Connect the signal that tells us when Steam has finished downloading a lobby's data
  Steam.lobby_data_update.connect(_on_lobby_data_update)

# this is called in the main_menu.gd script where it's triggered when the join button is pressed
# it is also triggered when the filter option has changed
func _look_for_lobbies(index: int) -> void:
  if index == 0:
    # use our custom function to filter out any lobbies our friends might be hosting
    _get_friends_lobbies()
  elif index == 1: 
    # filter the public lobby by distance
    Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_CLOSE)
    Steam.requestLobbyList() # this actually requests the lobby list that triggers the signal 'lobby_match_list'

func _on_lobby_match_list(lobbies: Array) -> void:
  # delete any lobbies already listed
  for child in lobbies_container.get_children():
    child.queue_free()
    
  # Clear our pending list since we are doing a fresh search
  pending_lobbies.clear()
  
  # go through each lobby found by steam
  for lobby_id in lobbies:
    # Request the data from Steam (this is asynchronous!)
    Steam.requestLobbyData(lobby_id)
    var lobby_name: String = Steam.getLobbyData(lobby_id, "name") # oarLobbyName attribute

    if lobby_name != "":
      # Data was already cached locally, add it immediately
      _add_lobby_to_ui(lobby_id, lobby_name)
    else:
      # Data hasn't arrived from Steam yet, add it to our waitlist
      if not pending_lobbies.has(lobby_id):
        pending_lobbies.append(lobby_id)

# NEW: This runs automatically when Steam finishes getting data for a specific lobby
func _on_lobby_data_update(lobby_id, _member_id, _success) -> void:
  # Check if this is a lobby we are currently waiting for
  if pending_lobbies.has(lobby_id):
    var lobby_name: String = Steam.getLobbyData(lobby_id, "name")
    
    if lobby_name != "":
      # We got the name! Remove it from the waitlist and add it to the UI
      pending_lobbies.erase(lobby_id)
      _add_lobby_to_ui(lobby_id, lobby_name)

# NEW: Helper function to keep code clean and avoid repeating the UI instantiation
func _add_lobby_to_ui(lobby_id: int, lobby_name: String) -> void:
  var lobby_listing = lobby_entry.instantiate()
  lobbies_container.add_child(lobby_listing)
  lobby_listing.initalize(lobby_id, lobby_name)

func _get_friends_lobbies() -> void:
  var friends_lobbies: Array = []
  
  # 1. Ask Steam how many friends the player has
  var num_friends: int = Steam.getFriendCount()
  
  # 2. Loop through every single friend
  for i in range(num_friends):
    var friend_steam_id: int = Steam.getFriendByIndex(i, Steam.FRIEND_FLAG_IMMEDIATE)

    # 3. Get the details of whatever game they are currently playing
    var game_info: Dictionary = Steam.getFriendGamePlayed(friend_steam_id)
  
    # 4. Check if they are in a lobby, and if it's for OUR game (using your AppID)
    if game_info.has("lobby") and game_info["lobby"] != 0:
      if game_info.has("id") and game_info["id"] == 480:
        var lobby_id = game_info["lobby"]
        
        # FIX 2: Check if we already found this exact lobby through another friend
        # This deduplicates the list so a server only shows up once!
        if not friends_lobbies.has(lobby_id):
          friends_lobbies.append(lobby_id)
          
      else:
          print("   (Friend is in a lobby, but for a different AppID: ", game_info.get("id", "Unknown"), ")")
    elif game_info.has("id"):
      print("   (Friend is playing our game, but is NOT currently in a lobby)")
    
  _on_lobby_match_list(friends_lobbies)