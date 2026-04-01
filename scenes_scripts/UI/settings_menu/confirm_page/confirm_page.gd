extends Control

signal RevertButtonPressed()
signal ConfirmButtonPressed()

@onready var countdown_label: Label = $GreyBackground/Panel/MarginContainer/VBoxContainer/CountdownLabel

func _on_revert_button_pressed() -> void:
	RevertButtonPressed.emit()


func _on_confirm_button_pressed() -> void:
	ConfirmButtonPressed.emit()

func set_countdown_label_text(text: String) -> void:
	countdown_label.text = text