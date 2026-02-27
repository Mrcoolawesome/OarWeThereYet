extends BasePopUpMenu

var lobby_entry = preload("res://scenes_scripts/UI/pop_up_menu/join_popup_menu/lobby_listing.tscn")
@onready var lobby_tag: Label = $LobbyName

func get_steam_lobbies() -> void:
	Steam.lobby_match_list.connect(lobby_match_list)
	Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_DEFAULT)
	Steam.requestLobbyList() # this actually requests the lobby list that triggers the signal 'lobby_match_list'

func lobby_match_list(lobbies: Array) -> void:
	for lobby_id in lobbies:
		var lobby_name: String = Steam.getLobbyData(lobby_id, "oarLobbyName")

		if lobby_name != "":
			var lobby_listing = lobby_entry.instantiate()
			lobby_listing.initalize(lobby_id, lobby_name)
			lobby_tag.text = lobby_entry

