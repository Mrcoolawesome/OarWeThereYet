extends Control

@onready var host_button: Button = $MainMenuContainer/VBoxContainer/HostButton
@onready var join_button: Button = $MainMenuContainer/VBoxContainer/JoinButton
@onready var steam = $Steam
@onready var main_menu_container: MarginContainer = $MainMenuContainer
@onready var host_game_menu: Control = $HostGamePopUpMenu
@onready var join_menu_container: Control = $JoinGamePopUpMenu
@onready var settings_menu_container: Control = $SettingsContainer
@onready var select_save_menu: Control = $SelectSavePopupMenu

enum MenuType {HOST_MENU, JOIN_MENU, SETTINGS_MENU, MAIN_MENU}

func _on_id_prompt_text_changed(new_text: String) -> void:
	join_button.disabled = new_text.length() == 0

func _on_host_button_pressed() -> void:
	# show the pop up menu and hide the main menu
	_show_menu(MenuType.HOST_MENU)

func _on_join_button_pressed() -> void:
	if GlobalVariables.active_network_type == GlobalVariables.MULTIPLAYER_NETWORK_TYPE.STEAM:
		# hide the main ui and put the pop up ui
		_show_menu(MenuType.JOIN_MENU)
		join_menu_container._look_for_lobbies(0) # looks for the lobbies, 0 is the default which is friends lobbies
	else:
		GlobalSignalServer.emit_signal("JoinGame", 0) # lobby id doesn't matter for ENet network

func _on_settings_button_pressed() -> void:
	_show_menu(MenuType.SETTINGS_MENU)

func _on_host_pop_up_menu_go_back_button_pressed() -> void:
	# hide the other menus and show the main menu
	_show_menu(MenuType.MAIN_MENU)

func _on_join_game_pop_up_menu_go_back_button_pressed() -> void:
	# hide the other menus and show the main menu
	_show_menu(MenuType.MAIN_MENU)

func _on_settings_menu_back_button_pressed() -> void:
	_show_menu(MenuType.MAIN_MENU)

func _on_select_save_menu_back_button_pressed() -> void:
	_show_menu(MenuType.MAIN_MENU)

func _show_menu(menu: MenuType) -> void:
	# make them all invisible except for one
	join_menu_container.visible = false
	host_game_menu.visible = false
	main_menu_container.visible = false
	settings_menu_container.visible = false
	select_save_menu.visible = false

	# make the specified menu visible
	match menu:
		MenuType.HOST_MENU:
			host_game_menu.visible = true
			#select_save_menu.visible = true
		MenuType.JOIN_MENU:
			join_menu_container.visible = true
		MenuType.SETTINGS_MENU:
			settings_menu_container.visible = true
		MenuType.MAIN_MENU:
			main_menu_container.visible = true
