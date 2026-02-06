extends Control

@onready var host_button: Button = $HostButton
@onready var join_button: Button = $JoinButton
@onready var id_prompt = $IDPrompt

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	join_button.pressed.connect(GameManager._on_join_button_pressed.bind(id_prompt.text.to_int()))
	host_button.pressed.connect(GameManager._on_host_button_pressed)

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

func _on_id_prompt_text_changed(new_text: String) -> void:
	join_button.disabled = new_text.length() == 0
