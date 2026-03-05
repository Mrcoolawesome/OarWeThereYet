extends Control

@onready var vc_volume_slider = $MarginContainer/VBoxContainer/VoicechatVolumeSlider
@onready var main_volume_slider = $MarginContainer/VBoxContainer/MainVolumeSlider

# the settings object
var settings_prefrences: UserSettingPrefrences

func _ready() -> void:
	# load the settings prefrences
	settings_prefrences = UserSettingPrefrences.load_or_create()
	# set the slider tag immedately 
	var normalized_voicechat_volume: String = str(int(settings_prefrences.voicechat_volume * 100))
	var normalized_master_volume: String = str(int(settings_prefrences.master_volume * 100))
	# set the text tag and the slider default values
	vc_volume_slider.number_tag.text = normalized_voicechat_volume
	main_volume_slider.number_tag.text = normalized_master_volume
	vc_volume_slider.StartingValue = normalized_voicechat_volume
	main_volume_slider.StartingValue = normalized_master_volume

func _on_voice_chat_volume_slider_value_changed(value: float) -> void:
	# the audio bus takes values only from 0.0 to 1.0
	var normalized_value: float = value / 100.0
	# set the volume linearly 
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), normalized_value)

	# set the slider's number tag value
	# the given value is between 0.0 and 1.0 because that's the value that 
	vc_volume_slider.number_tag.text = str(int(value))

	# apply it to the settings prefrences object for potentially saving it
	settings_prefrences.voicechat_volume = normalized_value

func _on_main_volume_slider_slider_changed(new_value: float) -> void:
	# the audio bus takes values only from 0.0 to 1.0
	var normalized_value: float = new_value / 100.0
	# set the volume linearly 
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Master"), normalized_value)

	# set the slider's number tag value
	main_volume_slider.number_tag.text = str(int(new_value))

	# apply it to the settings prefrences object for potentially saving it
	settings_prefrences.master_volume = normalized_value

# this is ran by the parent settings script
func apply_settings() -> void:
	# save it and then apply them
	settings_prefrences.save()
	PrefrencesLoader.apply_audio_settings(settings_prefrences)
