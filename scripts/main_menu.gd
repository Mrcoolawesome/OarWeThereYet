extends Control

signal host_requested
signal join_requested(lobby_id)

@onready var host_button: Button = $HostButton
@onready var join_button: Button = $JoinButton
@onready var id_prompt = $IDPrompt

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass

func _on_id_prompt_text_changed(new_text: String) -> void:
	join_button.disabled = new_text.length() == 0

func _on_host_button_pressed() -> void:
	host_requested.emit()

func _on_join_button_pressed() -> void:
	join_requested.emit(int(id_prompt.text))
