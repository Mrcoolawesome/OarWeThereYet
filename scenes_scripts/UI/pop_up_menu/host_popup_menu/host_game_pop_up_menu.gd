extends BasePopUpMenu

# Variables for storing everything inputted into the pop up menu
var lobby_name: String = "gaming"
var is_public: bool = false

signal delete_save

# We need to get the textbox container so we can hide and unhide it depending on if they made it public or not.
# In GDScript, @onready is the cleanest way to grab nodes. It automatically fetches the node right before _ready() is called.
@onready var _text_box_container: MarginContainer = $PanelContainer/VBoxContainer/LobbyNameContainer

# the deletion confirm page
@onready var deletion_confirm_page: Control = $DeletionConfirmPage

@onready var delete_button: Button = $PanelContainer/VBoxContainer/MarginContainer/DeleteButton
@onready var speed_selecter: VBoxContainer = $PanelContainer/VBoxContainer/MarginContainer/SpeedSelecter
@onready var host_button: Button = $PanelContainer/VBoxContainer/MarginContainer3/HostButton

const SAVE_DIR := "user://saves/"

func setup_for_save() -> void:
	var save_path := "%ssave_%d.tres" % [SAVE_DIR, GlobalVariables.save_slot]
	
	if FileAccess.file_exists(save_path):
		delete_button.visible = true
		speed_selecter.visible = false
		host_button.disabled = false
	else:
		delete_button.visible = false
		speed_selecter.visible = true
		GlobalVariables.motivator_speed = -1
		host_button.disabled = true


func _ready() -> void:
	# Make sure the confirm page is hidden by default
	deletion_confirm_page.visible = false
	
	# Connect the signals from the confirm page to functions in this script
	deletion_confirm_page.ConfirmButtonPressed.connect(_on_confirm_deletion)
	deletion_confirm_page.RevertButtonPressed.connect(_on_cancel_deletion)


# This is connected to the checkbox to make it a public lobby or not
func on_check_box_toggled(toggled_on: bool) -> void:
	is_public = toggled_on
	_text_box_container.visible = toggled_on # reveal the text box container


# This is connected to the textbox that sets the name of the lobby
func on_line_edit_text_changed(new_text: String) -> void:
	lobby_name = new_text

# NEW: Connected to the LineEdit's "text_submitted" signal (fires on pressing Enter)
func on_line_edit_text_submitted(new_text: String) -> void:
	lobby_name = new_text # Just to be safe, make sure we have the absolute latest text
	on_host_button_pressed() # Act exactly as if the Host button was clicked


# Triggered when the HOST button is pressed
func on_host_button_pressed() -> void:
	# TODO: Host needs to tell level which save slot they want to use
	if GlobalVariables.active_network_type == GlobalVariables.MULTIPLAYER_NETWORK_TYPE.STEAM:
		# Emit via the global signal server
		# Assuming GlobalSignalServer is set up as an Autoload (Singleton) in your project settings
		GlobalSignalServer.emit_signal("HostGameSteam", is_public, lobby_name)
		GlobalSignalServer.emit_signal("ShowLoadingScreen")
	else:
		# For the ENet network
		GlobalSignalServer.emit_signal("HostGameEnet")
		GlobalSignalServer.emit_signal("ShowLoadingScreen")


# Triggered when the initial "Delete Save" button is pressed
func _delete_save_button_pressed() -> void:
	# Update the text on the confirm page and show it
	deletion_confirm_page.set_countdown_label_text("Are you sure you want to delete this save?")
	deletion_confirm_page.visible = true


# --- CONFIRM PAGE SIGNAL HANDLERS ---

func _on_confirm_deletion() -> void:
	# Hide the confirm page and actually emit the deletion signal
	deletion_confirm_page.visible = false
	delete_save.emit()


func _on_cancel_deletion() -> void:
	# Just hide the confirm page, doing nothing else
	deletion_confirm_page.visible = false


func _on_fast_pressed() -> void:
	GlobalVariables.motivator_speed = 2
	host_button.disabled = false


func _on_medium_pressed() -> void:
	GlobalVariables.motivator_speed = 1
	host_button.disabled = false


func _on_slow_pressed() -> void:
	GlobalVariables.motivator_speed = 0
	host_button.disabled = false
