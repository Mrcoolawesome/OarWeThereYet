extends Node


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# load their settings or make a new settings save with the defaults defined in UserSettingPrefrences
	var user_settings_prefs: UserSettingPrefrences = UserSettingPrefrences.load_or_create()
	# VERY IMPORTANT: APPLY ALL SETTINGS IMMEDATELY UPON LOADING IN
	apply_all_settings(user_settings_prefs) 

# applies all of their settings
# we need to pass in certain things because this calss extends resource and thus doesn't have access to things that node classes do
func apply_all_settings(user_prefs: UserSettingPrefrences) -> void:
	apply_graphics_settings(user_prefs)
	apply_audio_settings(user_prefs)
	# TODO: apply all other settings here

# i make all of these functions take in the user settings prefrence object so they can be used outside of here
# applies only graphics settings
func apply_graphics_settings(user_prefs: UserSettingPrefrences) -> void:
	get_viewport().msaa_3d = user_prefs.msaa_mode
	DisplayServer.window_set_mode(user_prefs.display_mode)
	DisplayServer.window_set_flag(user_prefs.display_flag, user_prefs.borderless_enable) # this sets the gvien flag to false or true
	get_window().size = user_prefs.resolution # this is the correct way of doing it so that godot knows that the resolution has been changed

# applies only audio settings
func apply_audio_settings(user_prefs: UserSettingPrefrences) -> void:
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), user_prefs.voicechat_volume)
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Master"), user_prefs.master_volume)