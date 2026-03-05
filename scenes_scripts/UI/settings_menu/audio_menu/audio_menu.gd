extends Control

@onready var vc_volume_slider = $MarginContainer/VoicechatVolumeSlider

func _ready() -> void:
	# set the slider tag immedately 
	vc_volume_slider.number_tag.text = str(int(vc_volume_slider.StartingValue * 100))

func _on_voice_chat_volume_slider_value_changed(value: float) -> void:
	# set the volume via linearly 
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), value);

	# set the slider's number tag value
	vc_volume_slider.number_tag.text = str(int(value * 100))
