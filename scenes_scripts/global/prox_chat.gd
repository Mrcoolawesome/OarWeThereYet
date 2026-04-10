extends Node

class PlayerAudioData:
  var stream: AudioStreamPlayer3D
  var buffer: PackedVector2Array = PackedVector2Array()
  # A mathematical tracker for how much audio Godot is currently holding
  var unplayed_frames: float = 0.0 
  
  func _init(p_stream: AudioStreamPlayer3D):
    stream = p_stream

var current_sample_rate: int = 48000
var local_playback: bool = false

var buffer_target_seconds: float = 0.1
var frames_to_buffer: int = 0

var active_players: Dictionary[int, PlayerAudioData] = {}
var capture_effect: AudioEffectCapture

func _ready() -> void:
  set_process(false)

func initialize_voice():
  var bus_idx = AudioServer.get_bus_index("MicInput")
  if bus_idx != -1:
    for i in range(AudioServer.get_bus_effect_count(bus_idx)):
      if AudioServer.get_bus_effect(bus_idx, i) is AudioEffectCapture:
        capture_effect = AudioServer.get_bus_effect(bus_idx, i)
        break
        
  if not capture_effect:
    push_error("AudioEffectCapture not found on 'MicInput' bus! Voice chat will not work.")
    return
    
  current_sample_rate = AudioServer.get_mix_rate()
  frames_to_buffer = int(current_sample_rate * buffer_target_seconds)
  set_process(true)

func stop_voice():
  if capture_effect:
    capture_effect.clear_buffer()
  set_process(false)

func _process(_delta: float) -> void:
  # 1. FETCH VOICE DATA FROM GODOT MIC BUS
  if capture_effect:
    var frames_available = capture_effect.get_frames_available()
    if frames_available > 0:
      var audio_buffer: PackedVector2Array = capture_effect.get_buffer(frames_available)
      
      # Compress to 16-bit mono to save network bandwidth (similar to Steam)
      var byte_array = PackedByteArray()
      byte_array.resize(audio_buffer.size() * 2)
      
      for i in range(audio_buffer.size()):
        # Mix down to mono
        var sample = (audio_buffer[i].x + audio_buffer[i].y) / 2.0 
        var raw_value = int(clamp(sample, -1.0, 1.0) * 32767.0)
        byte_array.encode_s16(i * 2, raw_value)
      
      # SAFETY CHECK: Only send the RPC if we are actually connected to a multiplayer session
      if multiplayer.multiplayer_peer != null and multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED:
        send_voice.rpc(byte_array)
      else:
        # If we lost connection, automatically shut down the mic to prevent errors
        stop_voice()
        return
      
      if local_playback:
        process_voice(byte_array, multiplayer.get_unique_id())
        
      # CALCULATE LOCAL LOUDNESS AND EMIT SIGNAL
      var loudness = calculate_loudness(byte_array, byte_array.size())
      GlobalSignalServer.emit_signal("PlayerLoudness", loudness)

  # 2. FEED THE GENERATORS
  for player_id in active_players.keys():
    var p_data = active_players[player_id]
    
    # Clean up disconnected players
    if not is_instance_valid(p_data.stream):
      active_players.erase(player_id)
      continue
      
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

func process_voice(voice_buffer: PackedByteArray, player: int):
  if not active_players.has(player):
    # --- WE ADDED THIS ABORT CHECK ---
    var setup_successful = setup_player_audio(player)
    if not setup_successful:
      return

  var p_data = active_players[player]
  var frame_count = voice_buffer.size() / 2
  
  var audio_frames: PackedVector2Array = PackedVector2Array()
  audio_frames.resize(frame_count) 
  
  for i in range(frame_count):
    var raw_value: int = voice_buffer.decode_s16(i * 2)
    var amplitude: float = float(raw_value) / 32768.0
    # Duplicate mono back into stereo for the 3D Audio Stream
    audio_frames[i] = Vector2(amplitude, amplitude)

  p_data.buffer.append_array(audio_frames)

func setup_player_audio(player_id: int) -> bool:
  # --- WE CHANGED THIS TO get_node_or_null ---
  var stream_node: AudioStreamPlayer3D = get_node_or_null("/root/GameManager/Level/StylizedMap/" + str(player_id) + "/AudioStreamPlayer3D")
  
  if stream_node and stream_node.stream is AudioStreamGenerator:
    stream_node.stream.mix_rate = current_sample_rate
    stream_node.stream.buffer_length = 0.1 
    
    var player_data = PlayerAudioData.new(stream_node)
    active_players[player_id] = player_data
    return true
  else:
    return false # Safely fail without pushing an error

@rpc("any_peer", "call_remote", "unreliable", 1)
func send_voice(voice_buffer: PackedByteArray):
  var sender_id = multiplayer.get_remote_sender_id()
  if sender_id != multiplayer.get_unique_id():
    process_voice(voice_buffer, sender_id)

# MATH HELPER FOR AUDIO VOLUME
func calculate_loudness(voice_buffer: PackedByteArray, size: int) -> float:
  var sum: float = 0.0
  var num_samples = size / 2
  for i in range(num_samples):
    var raw_value: int = voice_buffer.decode_s16(i * 2)
    # Normalize to a 0.0 - 1.0 range based on the 16-bit audio limit
    var amplitude: float = abs(float(raw_value) / 32768.0)
    sum += amplitude
  
  if num_samples > 0:
    return sum / float(num_samples)
  return 0.0
