extends Control

func _on_voice_chat_volume_slider_value_changed(value: float) -> void:
	# set the volume via linearly 
	AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), value);
