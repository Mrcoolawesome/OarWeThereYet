extends BasePopUpMenu

var lobby_entry = preload("res://scenes_scripts/UI/pop_up_menu/join_popup_menu/lobby_listing.tscn")
@onready var lobbies_container: VBoxContainer = $PanelContainer/VBoxContainer/ScrollContainer/MarginContainer/LobbiesContainer

func _ready() -> void:
  # connect the lobby fetching logic to the steam api
  Steam.lobby_match_list.connect(_on_lobby_match_list)

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
  
  # go through each lobby found by steam
  for lobby_id in lobbies:
    var lobby_name: String = Steam.getLobbyData(lobby_id, "name") # oarLobbyName attribute we set the name to be in the steam_network.gd script

    if lobby_name != "":
      var lobby_listing = lobby_entry.instantiate()

      # add the instance to the VBox that displays all the lobbies
      lobbies_container.add_child(lobby_listing)

      # initalize the nodes name and give it the id
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
      if game_info.has("id") and game_info["id"] == Steam.getAppID():
        friends_lobbies.append(game_info["lobby"])
    
  # 5. Send this custom list directly to your UI function!
  _on_lobby_match_list(friends_lobbies)
