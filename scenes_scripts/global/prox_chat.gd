extends Node

var current_sample_rate: int = 48000
var use_recommended_sample_rate: bool = true
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
		var voice_buffer: PackedByteArray = decompressed_voice['uncompressed']
		# Get player's audiostream Node
		var player_stream: AudioStreamPlayer3D = get_node("/root/GameManager/Level/DemoLevel/" + str(player) + "/AudioStreamPlayer3D")
		player_stream.stream.mix_rate = current_sample_rate
		var playback: AudioStreamGeneratorPlayback = player_stream.get_stream_playback()

		var audio_frames: PackedVector2Array = PackedVector2Array()
		
		for i in range(0,voice_buffer.size(), 2):
			var raw_value: int = voice_buffer.decode_s16(i)
			var amplitude: float = float(raw_value) / 32768.0

			# push_frame() takes a Vector2. The x represents the left channel and the y represents the right channel
			audio_frames.append(Vector2(amplitude, amplitude))
			playback.push_frame(Vector2(amplitude, amplitude))

		#if playback.can_push_buffer(audio_frames.size()):
			#playback.push_buffer(audio_frames)


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
