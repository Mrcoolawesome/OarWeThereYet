extends Control

@onready var host_button: Button = $HostButton
@onready var join_button: Button = $JoinButton
@onready var id_prompt: LineEdit = $IDPrompt
@onready var steam = $Steam

# boolean to keep track of wether the steam toggle was pressed or not
var steam_mode = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass

func _on_id_prompt_text_changed(new_text: String) -> void:
	join_button.disabled = new_text.length() == 0

func _on_host_button_pressed() -> void:
	# send the signal via the global signal server so the network manager can access it
	GlobalSignalServer.emit_signal("HostGame")

func _on_join_button_pressed() -> void:
	# only send the signal if there's an id in the text box and we're in steam mode or just let them bypass the box if we're not in steam mode
	if (id_prompt.text != "" && steam_mode) || !steam_mode: 
		# send the signal via the global signal server
		GlobalSignalServer.emit_signal("JoinGame", int(id_prompt.text))

func _on_toggle_steam_toggled(toggled_on: bool) -> void:
	# send the signal to select the steam network via the signal server
	GlobalSignalServer.emit_signal("SelectSteamNetwork", toggled_on)
	steam.visible = toggled_on
	steam_mode = toggled_on
