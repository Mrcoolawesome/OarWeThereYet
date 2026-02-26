extends BasePopUpMenu

# Variables for storing everything inputted into the pop up menu
var _lobby_name: String = "gaming"
var _is_public: bool = false

# We need to get the textbox container so we can hide and unhide it depending on if they made it public or not.
# In GDScript, @onready is the cleanest way to grab nodes. It automatically fetches the node right before _ready() is called.
@onready var _text_box_container: MarginContainer = $PanelContainer/VBoxContainer/LobbyNameContainer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Because we used @onready above, we don't actually need to fetch the node here.
	# But if you didn't use @onready, it would look like this:
	# _text_box_container = get_node("PanelContainer/VBoxContainer/LobbyNameContainer")
	pass


# This is connected to the checkbox to make it a public lobby or not
func on_check_box_toggled(toggled_on: bool) -> void:
	_is_public = toggled_on
	_text_box_container.visible = toggled_on # reveal the text box container


# This is connected to the textbox that sets the name of the lobby
func on_line_edit_text_changed(new_text: String) -> void:
	_lobby_name = new_text


# Triggered when the HOST button is pressed
func on_host_button_pressed() -> void:
	# Emit via the global signal server
	# Assuming GlobalSignalServer is set up as an Autoload (Singleton) in your project settings
	GlobalSignalServer.emit_signal("HostGame")