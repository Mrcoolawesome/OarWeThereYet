extends Node

var current_sample_rate: int = 48000
var use_recommended_sample_rate: bool = true
var voice_buffer: PackedByteArray = PackedByteArray()
var playback: AudioStreamGeneratorPlayback = null
var local_playback: bool = true


func _ready() -> void:
	set_process(false)


func initialize_voice():
	# Start recording when player loads in, could make an option for push to talk later
	Steam.startVoiceRecording()
	get_sample_rate()
	set_process(true)


# Get voice data, compress it, and send it
func _process(_delta: float) -> void:
	var voice_data: Dictionary = Steam.getVoice()

	if voice_data['result'] == Steam.VOICE_RESULT_OK and voice_data['written'] > 0:
		send_voice.rpc(voice_data['buffer'])

		if local_playback:
			process_voice(voice_data['buffer'], multiplayer.get_unique_id())


# Decompress received voice data
func process_voice(voice_data: PackedByteArray, player: int):
	var decompressed_voice: Dictionary = Steam.decompressVoice(voice_data, current_sample_rate)

	if decompressed_voice['result'] == Steam.VOICE_RESULT_OK and decompressed_voice['size'] > 0:
		# Get player's audiostream Node
		var player_stream: AudioStreamPlayer3D = get_node("/root/GameManager/Level/DemoLevel/" + str(player) + "/AudioStreamPlayer3D")
		var playback: AudioStreamGeneratorPlayback = player_stream.get_stream_playback()
		
		# Create array of vector2
		var raw_bytes: PackedByteArray = decompressed_voice['uncompressed']
		var audio_frames: PackedVector2Array = PackedVector2Array()

		# Step through the raw bytes 2 at a time (since 16-bit = 2 bytes per sample)
		for i in range(0, raw_bytes.size(), 2):
			# Grab the 16-bit integer from the byte array
			var sample_int: int = raw_bytes.decode_s16(i)
			
			# A 16-bit integer has a max value of 32768. 
			# We divide by 32768.0 to convert it into a float between -1.0 and 1.0
			var float_sample: float = sample_int / 32768.0
			
			# Create the Vector2 (Left ear, Right ear) and add it to our new array
			audio_frames.append(Vector2(float_sample, float_sample))

			# NOW we check our buffer capacity and push the correctly formatted frames!
			# (Notice I included the () after .size this time so Godot executes the method!)
			if playback.can_push_buffer(audio_frames.size()):
				playback.push_buffer(audio_frames)


# Get steam's recommended sample rate if you want
func get_sample_rate():
	if use_recommended_sample_rate:
		current_sample_rate = Steam.getVoiceOptimalSampleRate()
	else:
		current_sample_rate = 48000


# Send voice data over the internet
@rpc("any_peer", "call_remote", "unreliable", 1)
func send_voice(voice_data: PackedByteArray):
	var sender_id = multiplayer.get_remote_sender_id()
	process_voice(voice_data, sender_id)
