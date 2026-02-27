extends BasePopUpMenu

var lobby_entry = preload("res://scenes_scripts/UI/pop_up_menu/join_popup_menu/lobby_listing.tscn")
@onready var lobbies_container: VBoxContainer = $PanelContainer/VBoxContainer/ScrollContainer/MarginContainer/LobbiesContainer

func _look_for_lobbies() -> void:
	Steam.lobby_match_list.connect(_on_lobby_match_list)
	Steam.addRequestLobbyListDistanceFilter(Steam.LOBBY_DISTANCE_FILTER_WORLDWIDE)
	Steam.requestLobbyList() # this actually requests the lobby list that triggers the signal 'lobby_match_list'

func _on_lobby_match_list(lobbies: Array) -> void:
	# Debug print so you know the signal actually fired and how many lobbies it found
	print("Found " + str(lobbies.size()) + " lobbies!")
	for lobby_id in lobbies:
		var lobby_name: String = Steam.getLobbyData(lobby_id, "name")

		# if lobby_name != "":
		var lobby_listing = lobby_entry.instantiate()

		# add the instance to the VBox that displays all the lobbies
		lobbies_container.add_child(lobby_listing)

		# initalize the nodes name and give it the id
		lobby_listing.initalize(lobby_id, lobby_name)
