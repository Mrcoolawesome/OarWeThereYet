extends Control

# get the sliders so we can edit their display properly
@onready var msaa_slider = $MarginContainer/ScrollContainer/VBoxContainer/MSAASlider

# the settings object
var graphics_settings_prefrences: UserSettingPrefrences

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# load their settings to see what they already have saved
	graphics_settings_prefrences = UserSettingPrefrences.load_or_create()

	# set the number display to what they already have by default
	# this is type casting to an int because the .msaa_mode value is an enum
	var loaded_msaa_mode: float = int(graphics_settings_prefrences.msaa_mode) as float
	_on_msaa_slider_slider_changed(loaded_msaa_mode) # this needs to be a float because that's what the function takes in due to that being what the slider emits
	# then set the sliders default value as well so the grabber is in the right spot
	msaa_slider.StartingValue = loaded_msaa_mode

func _on_msaa_slider_slider_changed(new_value: float) -> void:
	var converted_value: Viewport.MSAA = int(new_value) as Viewport.MSAA # get the value in terms of the MSAA enum
	graphics_settings_prefrences.msaa_mode = converted_value # store the value in the temp settings object

	# set the display depending on the mode
	var display_string: String = ""
	match converted_value:
		Viewport.MSAA_DISABLED:
			display_string = "DISABLED"
		Viewport.MSAA_2X:
			display_string = "2x"
		Viewport.MSAA_4X:
			display_string = "4x"
		Viewport.MSAA_8X:
			display_string = "8x"

	# change the number display to the display string
	msaa_slider.change_number_display_tag(display_string)

# sets the dropdown value in the settings object
func _on_screen_mode_dropdown_item_selected(item: int) -> void:
	var converted_value: DisplayServer.WindowMode = item as DisplayServer.WindowMode
	var new_display_mode: DisplayServer.WindowMode
	var new_display_flag: DisplayServer.WindowFlags = DisplayServer.WINDOW_FLAG_BORDERLESS
	var borderless_enable: bool = false
	# set the display mode based on what they select
	match converted_value:
		0:
			new_display_mode = DisplayServer.WINDOW_MODE_FULLSCREEN
		1: 
			new_display_mode = DisplayServer.WINDOW_MODE_MAXIMIZED
			borderless_enable = true
		2: 
			new_display_mode = DisplayServer.WINDOW_MODE_WINDOWED
	graphics_settings_prefrences.display_mode = new_display_mode
	graphics_settings_prefrences.display_flag = new_display_flag
	graphics_settings_prefrences.borderless_enable = borderless_enable

# settings are only applied when this button is pressed
# TODO: have a confirm page that resets the settings to the previous values either if they choose to revert them, or if 15 sec has gone by without any input
func _on_apply_settings_button_pressed() -> void:
	# just save and apply all the settings
	graphics_settings_prefrences.save()
	PrefrencesLoader.apply_graphics_settings(graphics_settings_prefrences)
