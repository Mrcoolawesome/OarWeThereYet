extends Node

class PlayerAudioData:
	var stream: AudioStreamPlayer3D
	var buffer: PackedVector2Array = PackedVector2Array()
	var is_buffering: bool = true
	
	# A custom constructor makes creating this object a one-liner
	func _init(p_stream: AudioStreamPlayer3D):
		stream = p_stream

var current_sample_rate: int = 48000
var use_recommended_sample_rate: bool = true
var local_playback: bool = true

# Jitter Buffer Configuration
var buffer_target_seconds: float = 0.1
var frames_to_buffer: int = 0

# Dictionary to cache references and track states:
# Format: { player_id: { "stream": AudioStreamPlayer3D, "playback": AudioStreamGeneratorPlayback, "is_buffering": bool } }
var active_players: Dictionary[int, PlayerAudioData] = {}


func _ready() -> void:
	set_process(false)


func initialize_voice():
	Steam.startVoiceRecording()
	get_sample_rate()
	
	# Calculate exactly how many frames equal our target delay
	frames_to_buffer = int(current_sample_rate * buffer_target_seconds)
	set_process(true)


func _process(_delta: float) -> void:

	# Get and send voice data
	var voice_data: Dictionary = Steam.getVoice()
	if voice_data['result'] == Steam.VOICE_RESULT_OK and voice_data['written'] > 0:
		send_voice.rpc(voice_data['buffer'])
		if local_playback:
			process_voice(voice_data['buffer'], multiplayer.get_unique_id())

	# Check for empty buffer and turn off playback
	for player_id in active_players.keys():
		var p_data = active_players[player_id]
		
		# If they are already buffering, we don't need to check them
		if p_data.is_buffering:
			continue
			
		var stream: AudioStreamPlayer3D = p_data.stream
		var playback: AudioStreamGeneratorPlayback = stream.get_stream_playback()

		if playback == null:
			continue
		
		# Calculate total capacity of this specific generator bucket
		var total_capacity: int = int(stream.stream.buffer_length * current_sample_rate)
		var empty_space: int = playback.get_frames_available()
		
		# If the empty space is equal to or greater than capacity, the bucket is completely dry
		if empty_space >= total_capacity:
			stream.stop()
			print("stopping stream")
			p_data.is_buffering = true


# Decompress received voice data and push to buffer
func process_voice(voice_data: PackedByteArray, player: int):
	var decompressed_voice: Dictionary = Steam.decompressVoice(voice_data, current_sample_rate)

	if decompressed_voice['result'] != Steam.VOICE_RESULT_OK or decompressed_voice['size'] <= 0:
		return

	# Cache player nodes if we haven't seen them before
	if not active_players.has(player):
		setup_player_audio(player)

	var p_data = active_players[player]
	
	# Decode audio frames
	var voice_buffer: PackedByteArray = decompressed_voice['uncompressed']
	var audio_frames: PackedVector2Array = PackedVector2Array()
	
	for i in range(0, voice_buffer.size(), 2):
		var raw_value: int = voice_buffer.decode_s16(i)
		var amplitude: float = float(raw_value) / 32768.0
		audio_frames.append(Vector2(amplitude, amplitude))

	# Push to Godot's audio generator if not buffering
	if not p_data.is_buffering:
		var playback: AudioStreamGeneratorPlayback = p_data.stream.get_stream_playback()

		if playback.can_push_buffer(audio_frames.size()):
			playback.push_buffer(audio_frames)

	# Else push to our own temp buffer
	else:
		p_data.buffer.append_array(audio_frames)
		print(p_data.buffer.size())

		# If our temp buffer is big enough, start playback
		if p_data.buffer.size() >= frames_to_buffer:
			p_data.is_buffering = false
			p_data.stream.play()
			print("starting and playing stream")
			var playback: AudioStreamGeneratorPlayback = p_data.stream.get_stream_playback()
			playback.push_buffer(p_data.buffer)
			p_data.buffer.clear()

		

# Helper to cleanly cache node references
func setup_player_audio(player_id: int):
	var stream_node: AudioStreamPlayer3D = get_node("/root/GameManager/Level/DemoLevel/" + str(player_id) + "/AudioStreamPlayer3D")
	stream_node.stream.mix_rate = current_sample_rate
	
	var player_data: PlayerAudioData = PlayerAudioData.new(stream_node)

	active_players[player_id] = player_data
	

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
