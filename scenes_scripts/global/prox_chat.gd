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
  if GlobalSignalServer and !GlobalSignalServer.is_connected("AssignInputDevice", Callable(self, "_on_assign_input_device")):
    GlobalSignalServer.connect("AssignInputDevice", Callable(self, "_on_assign_input_device"))

  print("prox_chat ready on peer ", multiplayer.get_unique_id())
  set_process(false)

func _on_assign_input_device(device_name: String) -> void:
  var available_devices: PackedStringArray = AudioServer.get_input_device_list()
  if !available_devices.has(device_name):
    push_warning("Requested mic input device not found: " + device_name)
    return

  AudioServer.input_device = device_name

  # dump stale frames from the old device so capture starts clean
  if capture_effect:
    capture_effect.clear_buffer()

func initialize_voice():
  print("prox_chat initialize_voice on peer ", multiplayer.get_unique_id())
  _apply_saved_input_device_preference()

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
  print("prox_chat voice initialized: sample_rate=", current_sample_rate, " frames_to_buffer=", frames_to_buffer, " bus_idx=", bus_idx)
  set_process(true)

func _apply_saved_input_device_preference() -> void:
  var settings_prefrences: UserSettingPrefrences = UserSettingPrefrences.load_or_create()
  if settings_prefrences.input_device != "":
    _on_assign_input_device(settings_prefrences.input_device)

func stop_voice():
  print("stopping voice via stop voice")
  if capture_effect:
    capture_effect.clear_buffer()
  active_players.clear()
  print("prox_chat voice stopped and active players cleared on peer ", multiplayer.get_unique_id())
  set_process(false)

func _process(_delta: float) -> void:
  # 1. FETCH VOICE DATA FROM GODOT MIC BUS
  if capture_effect:
    var frames_available = capture_effect.get_frames_available()
    if frames_available > 0:
      print("prox_chat capture frames=", frames_available, " peer=", multiplayer.get_unique_id())
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
      print("prox_chat sending voice bytes=", byte_array.size(), " frames=", audio_buffer.size(), " peer=", multiplayer.get_unique_id())
      send_voice.rpc(byte_array)
      
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
  print("prox_chat process_voice from player=", player, " bytes=", voice_buffer.size(), " local_peer=", multiplayer.get_unique_id())
  if not active_players.has(player):
    # --- WE ADDED THIS ABORT CHECK ---
    var setup_successful = setup_player_audio(player)
    if not setup_successful:
      push_warning("prox_chat could not set up audio playback for player %s" % str(player))
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
  print("prox_chat buffered frames for player=", player, " total_buffered=", p_data.buffer.size())

func setup_player_audio(player_id: int) -> bool:
  # --- WE CHANGED THIS TO get_node_or_null ---
  var stream_path = "/root/GameManager/Level/StylizedMap/" + str(player_id) + "/AudioStuff/ProximityChatOutput"
  var stream_node: AudioStreamPlayer3D = get_node_or_null(stream_path)
  print("prox_chat setup_player_audio path=", stream_path, " found=", stream_node != null, " peer=", multiplayer.get_unique_id())
  
  if stream_node and stream_node.stream is AudioStreamGenerator:
    stream_node.stream.mix_rate = current_sample_rate
    stream_node.stream.buffer_length = 0.1 
    
    var player_data = PlayerAudioData.new(stream_node)
    active_players[player_id] = player_data
    print("prox_chat audio playback ready for player=", player_id)
    return true
  else:
    push_warning("prox_chat missing AudioStreamGenerator or AudioStreamPlayer3D at %s" % stream_path)
    return false # Safely fail without pushing an error

@rpc("any_peer", "call_remote", "unreliable", 1)
func send_voice(voice_buffer: PackedByteArray):
  var sender_id = multiplayer.get_remote_sender_id()
  print("prox_chat send_voice received sender_id=", sender_id, " local_peer=", multiplayer.get_unique_id(), " bytes=", voice_buffer.size())
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
