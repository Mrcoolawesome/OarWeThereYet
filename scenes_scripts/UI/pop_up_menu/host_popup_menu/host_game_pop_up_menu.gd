extends BasePopUpMenu

# Variables for storing everything inputted into the pop up menu
var lobby_name: String = "gaming"
var is_public: bool = false

# We need to get the textbox container so we can hide and unhide it depending on if they made it public or not.
# In GDScript, @onready is the cleanest way to grab nodes. It automatically fetches the node right before _ready() is called.
@onready var _text_box_container: MarginContainer = $PanelContainer/VBoxContainer/LobbyNameContainer

# This is connected to the checkbox to make it a public lobby or not
func on_check_box_toggled(toggled_on: bool) -> void:
	is_public = toggled_on
	_text_box_container.visible = toggled_on # reveal the text box container


# This is connected to the textbox that sets the name of the lobby
func on_line_edit_text_changed(new_text: String) -> void:
	lobby_name = new_text


# Triggered when the HOST button is pressed
func on_host_button_pressed() -> void:
	if GlobalVariables.active_network_type == GlobalVariables.MULTIPLAYER_NETWORK_TYPE.STEAM:
		# Emit via the global signal server
		# Assuming GlobalSignalServer is set up as an Autoload (Singleton) in your project settings
		GlobalSignalServer.emit_signal("HostGameSteam", is_public, lobby_name)
	else:
		# For the ENet network
		GlobalSignalServer.emit_signal("HostGameEnet")