extends Node

var current_sample_rate: int = 48000
@export var use_recommended_sample_rate: bool = false
var voice_buffer: PackedByteArray = PackedByteArray()


func _ready() -> void:
	# Start recording when player loads in, could make an option for push to talk later
	Steam.startVoiceRecording()


# Get voice data, compress it, and send it
func _process(delta: float) -> void:
	var voice_data: Dictionary = Steam.getVoice()

	if voice_data['result'] == Steam.VOICE_RESULT_OK and voice_data['written'] > 0:
		send_voice.rpc(voice_data)


# Decompress received voice data
func process_voice(voice_data: Dictionary, player: int):
	get_sample_rate()

	var decompressed_voice: Dictionary = Steam.decompressVoice(voice_data['buffer'], current_sample_rate)

	if decompressed_voice['result'] == Steam.VOICE_RESULT_OK and decompressed_voice['size'] > 0:



# Get steam's recommended sample rate if you want
func get_sample_rate():
	if use_recommended_sample_rate:
		current_sample_rate = Steam.getVoiceOptimalSampleRate()
	else:
		current_sample_rate = 48000


# Send voice data over the internet
@rpc("any_peer", "call_remote", "unreliable", 1)
func send_voice(voice_data: Dictionary):
	var sender_id = multiplayer.get_remote_sender_id()
	process_voice(voice_data, sender_id)
