extends Control

@onready var audio_menu: Control = $MainContainer/HBoxContainer/AudioMenu
@onready var reset_menu: Control = $MainContainer/HBoxContainer/ResetMenu
@onready var graphics_menu: Control = $MainContainer/HBoxContainer/GraphicsMenu

enum SubMenuVisibility {AUDIO, RESET, GRAPHICS}

signal back_button_pressed

func _on_audio_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.AUDIO)

func _on_reset_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.RESET)

func _on_graphics_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.GRAPHICS)

func _on_back_button_pressed() -> void:
	# emit the back button being pressed signal so the parent node can see it
	back_button_pressed.emit() 

# make only the given menu visible and all others invisible
func _show_menu(menu: SubMenuVisibility) -> void:
	# make all others stuff invisible right here so that they get turned visible right after this
	audio_menu.visible = false
	reset_menu.visible = false
	graphics_menu.visible = false
	match menu:
		SubMenuVisibility.AUDIO:
			audio_menu.visible = true
		SubMenuVisibility.RESET:
			reset_menu.visible = true
		SubMenuVisibility.GRAPHICS:
			graphics_menu.visible = true