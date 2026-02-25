extends Node

var current_sample_rate: int = 48000
var use_recommended_sample_rate: bool = true
var local_playback: bool = true

# Jitter Buffer Configuration
var buffer_target_seconds: float = 0.1 # 100ms of audio
var frames_to_buffer: int = 0

# Dictionary to cache references and track states:
# Format: { player_id: { "stream": AudioStreamPlayer3D, "playback": AudioStreamGeneratorPlayback, "is_buffering": bool } }
var active_players: Dictionary = {}


func _ready() -> void:
    set_process(false)


func initialize_voice():
    Steam.startVoiceRecording()
    get_sample_rate()
    
    # Calculate exactly how many frames equal our target delay
    frames_to_buffer = int(current_sample_rate * buffer_target_seconds)
    set_process(true)


func _process(_delta: float) -> void:
    # 1. OUTGOING: Get local voice data, compress it, and send it
    var voice_data: Dictionary = Steam.getVoice()
    if voice_data['result'] == Steam.VOICE_RESULT_OK and voice_data['written'] > 0:
        send_voice.rpc(voice_data['buffer'])
        if local_playback:
            process_voice(voice_data['buffer'], multiplayer.get_unique_id())

    # 2. THE WATCHDOG: Check for empty buffers / silence
    for player_id in active_players.keys():
        var p_data = active_players[player_id]
        
        # If they are already buffering, we don't need to check them
        if p_data["is_buffering"]:
            continue
            
        var playback: AudioStreamGeneratorPlayback = p_data["playback"]
        var stream: AudioStreamPlayer3D = p_data["stream"]
        
        # Calculate total capacity of this specific generator bucket
        var total_capacity: int = int(stream.stream.buffer_length * current_sample_rate)
        var empty_space: int = playback.get_frames_available()
        
        # If the empty space is equal to or greater than capacity, the bucket is completely dry
        if empty_space >= total_capacity:
            stream.stop()
            playback.clear_buffer() # Purge any microscopic garbage data
            p_data["is_buffering"] = true


# Decompress received voice data and push to buffer
func process_voice(voice_data: PackedByteArray, player: int):
    var decompressed_voice: Dictionary = Steam.decompressVoice(voice_data, current_sample_rate)

    if decompressed_voice['result'] != Steam.VOICE_RESULT_OK or decompressed_voice['size'] <= 0:
        return

    # Cache player nodes if we haven't seen them before
    if not active_players.has(player):
        setup_player_audio(player)

    var p_data = active_players[player]
    var playback: AudioStreamGeneratorPlayback = p_data["playback"]
    var stream: AudioStreamPlayer3D = p_data["stream"]
    
    # Decode audio frames
    var voice_buffer: PackedByteArray = decompressed_voice['uncompressed']
    var audio_frames: PackedVector2Array = PackedVector2Array()
    
    for i in range(0, voice_buffer.size(), 2):
        var raw_value: int = voice_buffer.decode_s16(i)
        var amplitude: float = float(raw_value) / 32768.0
        audio_frames.append(Vector2(amplitude, amplitude))

    # Push to Godot's internal C++ generator
    if playback.can_push_buffer(audio_frames.size()):
        playback.push_buffer(audio_frames)

    # 3. THE TRIGGER: If buffering, check if we have enough to start playing
    if p_data["is_buffering"]:
        var total_capacity: int = int(stream.stream.buffer_length * current_sample_rate)
        var empty_space: int = playback.get_frames_available()
        var loaded_frames: int = total_capacity - empty_space
        
        # Pull the trigger once we hit our target threshold
        if loaded_frames >= frames_to_buffer:
            p_data["is_buffering"] = false
            stream.play()


# Helper to cleanly cache node references
func setup_player_audio(player_id: int):
    var stream_node: AudioStreamPlayer3D = get_node("/root/GameManager/Level/DemoLevel/" + str(player_id) + "/AudioStreamPlayer3D")
    stream_node.stream.mix_rate = current_sample_rate
    
    # We must call play() once to initialize the generator playback object, then immediately stop it
    stream_node.play()
    var playback_ref: AudioStreamGeneratorPlayback = stream_node.get_stream_playback()
    stream_node.stop()
    
    active_players[player_id] = {
        "stream": stream_node,
        "playback": playback_ref,
        "is_buffering": true
    }


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