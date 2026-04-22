extends Node

class PlayerAudioData:
	var stream: AudioStreamPlayer3D
	var buffer: PackedVector2Array = PackedVector2Array()
	# NEW: A mathematical tracker for how much audio Godot is currently holding
	var unplayed_frames: float = 0.0 
	
	func _init(p_stream: AudioStreamPlayer3D):
		stream = p_stream

var current_sample_rate: int = 48000
var use_recommended_sample_rate: bool = true
var local_playback: bool = false

var buffer_target_seconds: float = 0.1
var frames_to_buffer: int = 0

var active_players: Dictionary[int, PlayerAudioData] = {}


func _ready() -> void:
	set_process(false)


func initialize_voice():
	Steam.startVoiceRecording()
	get_sample_rate()
	frames_to_buffer = int(current_sample_rate * buffer_target_seconds)
	set_process(true)

func stop_voice():
	Steam.stopVoiceRecording()
	set_process(false)

func get_sample_rate():
	if use_recommended_sample_rate:
		current_sample_rate = Steam.getVoiceOptimalSampleRate()
	else:
		current_sample_rate = 48000


func _process(_delta: float) -> void:
	# 1. FETCH VOICE DATA 
	while true:
		var voice_data: Dictionary = Steam.getVoice()
		if voice_data['result'] == Steam.VOICE_RESULT_OK and voice_data['written'] > 0:
			var raw_buffer = voice_data['buffer']
			send_voice.rpc(raw_buffer)
			
			if local_playback:
				process_voice(raw_buffer, multiplayer.get_unique_id())
				
			# --- NEW: CALCULATE LOCAL LOUDNESS AND EMIT SIGNAL ---
			var decompressed_voice = Steam.decompressVoice(raw_buffer, current_sample_rate)
			if decompressed_voice['result'] == Steam.VOICE_RESULT_OK and decompressed_voice['size'] > 0:
				var loudness = calculate_loudness(decompressed_voice['uncompressed'], decompressed_voice['size'])
				GlobalSignalServer.emit_signal("PlayerLoudness", loudness)
			# -----------------------------------------------------
		else:
			break

	# 2. FEED THE GENERATORS
	for player_id in active_players.keys():
		var p_data = active_players[player_id]
		var stream: AudioStreamPlayer3D = p_data.stream
		
		if not stream.playing:
			if p_data.buffer.size() >= frames_to_buffer:
				stream.play()
				p_data.unplayed_frames = 0.0 # Reset our custom tracker
				
		else:
			var playback: AudioStreamGeneratorPlayback = stream.get_stream_playback()
			if playback == null:
				continue
			
			# MATH MAGIC: Subtract the exact number of frames Godot consumed during this delta tick
			p_data.unplayed_frames -= current_sample_rate * _delta
			if p_data.unplayed_frames < 0:
				p_data.unplayed_frames = 0.0
			
			var frames_needed: int = playback.get_frames_available()
			
			if frames_needed > 0 and p_data.buffer.size() > 0:
				var push_amount = min(frames_needed, p_data.buffer.size())
				playback.push_buffer(p_data.buffer.slice(0, push_amount))
				p_data.buffer = p_data.buffer.slice(push_amount)
				
				# Add the frames we just pushed to our tracker
				p_data.unplayed_frames += push_amount
				
			# BULLETPROOF STARVATION CHECK:
			# We only stop if our custom buffer is empty AND our math says Godot has finished playing everything
			if p_data.buffer.is_empty() and p_data.unplayed_frames <= 0.0:
				stream.stop()


func process_voice(raw_buffer: PackedByteArray, player: int):
	var decompressed_voice: Dictionary = Steam.decompressVoice(raw_buffer, current_sample_rate)

	if decompressed_voice['result'] != Steam.VOICE_RESULT_OK or decompressed_voice['size'] <= 0:
		return

	if not active_players.has(player):
		setup_player_audio(player)

	var p_data = active_players[player]
	var voice_buffer: PackedByteArray = decompressed_voice['uncompressed']
	
	var frame_count = decompressed_voice["size"] / 2
	var audio_frames: PackedVector2Array = PackedVector2Array()
	audio_frames.resize(frame_count) 
	
	var frame_idx = 0
	for i in range(0, decompressed_voice["size"], 2):
		var raw_value: int = voice_buffer.decode_s16(i)
		var amplitude: float = float(raw_value) / 32768.0
		audio_frames[frame_idx] = Vector2(amplitude, amplitude)
		frame_idx += 1

	p_data.buffer.append_array(audio_frames)


func setup_player_audio(player_id: int):
	var stream_node: AudioStreamPlayer3D = get_node("/root/GameManager/Level/StylizedMap/" + str(player_id) + "/AudioStuff/ProximityChatOutput")
	
	if stream_node and stream_node.stream is AudioStreamGenerator:
		stream_node.stream.mix_rate = current_sample_rate
		stream_node.stream.buffer_length = 0.1 # Keep this exactly at 0.1 for perfect tracking
		
		var player_data = PlayerAudioData.new(stream_node)
		active_players[player_id] = player_data
	else:
		push_error("Could not find AudioStreamPlayer3D for player %s" % player_id)


@rpc("any_peer", "call_remote", "unreliable", 1)
func send_voice(voice_buffer: PackedByteArray):
	var sender_id = multiplayer.get_remote_sender_id()
	if sender_id != multiplayer.get_unique_id():
		process_voice(voice_buffer, sender_id)


# --- NEW: MATH HELPER FOR AUDIO VOLUME ---
func calculate_loudness(voice_buffer: PackedByteArray, size: int) -> float:
	var sum: float = 0.0
	var num_samples = size / 2
	for i in range(0, size, 2):
		var raw_value: int = voice_buffer.decode_s16(i)
		# Normalize to a 0.0 - 1.0 range based on the 16-bit audio limit
		var amplitude: float = abs(float(raw_value) / 32768.0)
		sum += amplitude
	
	if num_samples > 0:
		return sum / float(num_samples)
	return 0.0