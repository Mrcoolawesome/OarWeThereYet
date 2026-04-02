extends Control

# NEW SIGNAL: Lets parent know a change happened
signal setting_changed

@onready var look_speed_slider: Control = $MarginContainer/LookSpeedSlider # ranges from 0.00 - 1.00

# The settings objects
var settings_prefrences: UserSettingPrefrences
var cached_settings: UserSettingPrefrences

var is_loading_ui: bool = false # Prevents signals from firing when UI boots

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Load their settings to see what they already have saved
	settings_prefrences = UserSettingPrefrences.load_or_create()
	
	# Make a safe deep copy of the settings so we have something to revert back to
	cached_settings = settings_prefrences.duplicate(true)

	# Update all the ui upon loading in
	_load_ui_stuff()

func _load_ui_stuff() -> void:
	# Lock the signals while we load the UI
	is_loading_ui = true

	# Set the slider's initial grabber position and number display
	_on_look_speed_slider_slider_changed(settings_prefrences.look_speed)
	look_speed_slider.StartingValue = settings_prefrences.look_speed

	# Unlock the signals now that the UI is done setting up
	is_loading_ui = false

# Connect this function to your LookSpeedSlider's 'slider_changed' signal in the editor
func _on_look_speed_slider_slider_changed(new_value: float) -> void:
	# Update the temporary settings object
	settings_prefrences.look_speed = new_value
	
	# Assuming your custom slider has this method (like in your graphics menu).
	# This formats the float to 2 decimal places (e.g., "0.50"). 
	# Feel free to change the formatting if you want it to display as 1-100 instead!
	var display_string: String = str("%.2f" % new_value)
	look_speed_slider.change_number_display_tag(display_string)
	
	if !is_loading_ui:
		setting_changed.emit()

# --- DATA MANAGEMENT LOGIC ---

# Settings are only applied when this button is pressed
# This is ran by the parent settings script
func apply_settings() -> void:
	# apply the player look speed via the function defined in the prefrences loader
	PrefrencesLoader.apply_controls_settings(settings_prefrences)
	pass

# Called by parent if the player clicks "Confirm"
func confirm_settings() -> void:
	settings_prefrences.save()
	# Update our safe backup to match the newly confirmed settings
	cached_settings = settings_prefrences.duplicate(true)

# Called by parent if the player clicks "Revert" or the timer runs out
func revert_settings() -> void:
	# Overwrite our dirty settings with our safe backup
	settings_prefrences = cached_settings.duplicate(true)
	
	# TODO: Apply the safe backup settings back to the engine/player once you write that logic
	# E.g., PrefrencesLoader.apply_controls_settings(settings_prefrences)
	
	# Visually update the sliders/dropdowns to reflect the rollback
	_load_ui_stuff()