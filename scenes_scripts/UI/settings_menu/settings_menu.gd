extends Control

@onready var audio_menu_button: Control = $MarginContainer/HBoxContainer/AudioMenu

var audio_menu_button_visible: bool = false

enum SubMenuVisibility {AUDIO, RESET, GRAPHICS}

func _on_audio_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.AUDIO)


# make only the given menu visible and all others invisible
func _show_menu(menu: SubMenuVisibility) -> void:
	# TODO: make all others stuff invisible right here so that they get turned visible right after this
	match menu:
		SubMenuVisibility.AUDIO:
			audio_menu_button.visible = true
