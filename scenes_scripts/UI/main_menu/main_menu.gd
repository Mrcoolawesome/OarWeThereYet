extends Control

@onready var host_button: Button = $MainMenuContainer/VBoxContainer/HostButton
@onready var join_button: Button = $MainMenuContainer/VBoxContainer/JoinButton
@onready var steam = $Steam
@onready var main_menu_container: MarginContainer = $MainMenuContainer
@onready var popup_menu: Control = $HostGamePopUpMenu
@onready var join_menu_container: Control = $JoinGamePopUpMenu

func _on_id_prompt_text_changed(new_text: String) -> void:
	join_button.disabled = new_text.length() == 0

func _on_host_button_pressed() -> void:
	# show the pop up menu and hide the main menu
	popup_menu.visible = true
	main_menu_container.visible = false

func _on_join_button_pressed() -> void:
	if GlobalVariables.active_network_type == GlobalVariables.MULTIPLAYER_NETWORK_TYPE.STEAM:
		# hide the main ui and put the pop up ui
		join_menu_container.visible = true
		main_menu_container.visible = false
		join_menu_container._look_for_lobbies(0) # looks for the lobbies, 0 is the default which is friends lobbies
	else:
		GlobalSignalServer.emit_signal("JoinGame", 0) # lobby id doesn't matter for ENet network

func _on_host_pop_up_menu_go_back_button_pressed() -> void:
	# hide the pop up menu and show the main menu
	popup_menu.visible = false
	main_menu_container.visible = true

func _on_join_game_pop_up_menu_go_back_button_pressed() -> void:
	# hide the main menu and show the join menu
	join_menu_container.visible = false
	main_menu_container.visible = true
