extends Control

# NEW SIGNAL: Lets parent know a change happened
signal setting_changed

@onready var vc_volume_slider = $MarginContainer/ScrollContainer/VBoxContainer/VoicechatVolumeSlider
@onready var main_volume_slider = $MarginContainer/ScrollContainer/VBoxContainer/MainVolumeSlider
@onready var audio_input_device_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/MicInputDropdown
@onready var mic_option_button: OptionButton = $MarginContainer/ScrollContainer/VBoxContainer/MicInputDropdown/MarginContainer/HBoxContainer/OptionButton
@onready var environment_volume_slider = $MarginContainer/ScrollContainer/VBoxContainer/EnvironmentVolumeSlider
@onready var music_volume_slider = $MarginContainer/ScrollContainer/VBoxContainer/MusicVolumeSlider
@onready var player_movement_volume_slider = $MarginContainer/ScrollContainer/VBoxContainer/PlayeMovementVolumeSlider

# the settings object
var settings_prefrences: UserSettingPrefrences
var is_loading_ui: bool = false # Prevents signals from firing when UI boots
var input_devices: Array[String] = []
var last_detected_devices: PackedStringArray = PackedStringArray()
var device_refresh_interval: float = 2.0
var elapsed_refresh_time: float = 0.0

func _ready() -> void:
  # Lock the signals while we load the UI
  is_loading_ui = true
  
  # load the settings prefrences
  settings_prefrences = UserSettingPrefrences.load_or_create()
  # set the slider tag immedately 
  var normalized_voicechat_volume: String = str(int(settings_prefrences.voicechat_volume * 100))
  var normalized_master_volume: String = str(int(settings_prefrences.master_volume * 100))
  var normalized_player_movement_volume: String = str(int(settings_prefrences.player_movement_volume * 100))
  var normalized_music_volume: String = str(int(settings_prefrences.music_volume * 100))
  var normalized_environment_volume: String = str(int(settings_prefrences.environment_volume * 100))
  # set the text tag and the slider default values
  vc_volume_slider.number_tag.text = normalized_voicechat_volume
  main_volume_slider.number_tag.text = normalized_master_volume
  player_movement_volume_slider.number_tag.text = normalized_player_movement_volume
  music_volume_slider.number_tag.text = normalized_music_volume
  environment_volume_slider.number_tag.text = normalized_environment_volume
  vc_volume_slider.StartingValue = normalized_voicechat_volume
  main_volume_slider.StartingValue = normalized_master_volume
  player_movement_volume_slider.StartingValue = normalized_player_movement_volume
  music_volume_slider.StartingValue = normalized_music_volume
  environment_volume_slider.StartingValue = normalized_environment_volume

  # load all available mic devices into the dropdown
  _load_input_devices_into_dropdown()
  last_detected_devices = AudioServer.get_input_device_list()
  
  # Unlock the signals now that the UI is done setting up
  is_loading_ui = false
  set_process(true)

func _process(delta: float) -> void:
  if !visible:
    return

  elapsed_refresh_time += delta
  if elapsed_refresh_time < device_refresh_interval:
    return

  elapsed_refresh_time = 0.0
  _refresh_input_devices_if_changed()

func _refresh_input_devices_if_changed() -> void:
  var latest_devices: PackedStringArray = AudioServer.get_input_device_list()
  if _packed_string_arrays_equal(latest_devices, last_detected_devices):
    return

  # Keep this update silent; this is a device hot-plug sync, not a user action.
  is_loading_ui = true
  _load_input_devices_into_dropdown()
  is_loading_ui = false
  last_detected_devices = latest_devices

func _packed_string_arrays_equal(a: PackedStringArray, b: PackedStringArray) -> bool:
  if a.size() != b.size():
    return false

  for i in range(a.size()):
    if a[i] != b[i]:
      return false

  return true

func _load_input_devices_into_dropdown() -> void:
  input_devices.clear()
  mic_option_button.clear()

  var previous_input_device: String = settings_prefrences.input_device
  var devices: PackedStringArray = AudioServer.get_input_device_list()
  var default_input_device: String = AudioServer.input_device
  var selected_index: int = 0
  var current_input_device: String = default_input_device
  if settings_prefrences.input_device != "":
    current_input_device = settings_prefrences.input_device

  for i in range(devices.size()):
    var device_name: String = devices[i]
    input_devices.append(device_name)
    audio_input_device_dropdown.add_item(device_name, i)

    if device_name == current_input_device:
      selected_index = i

  if input_devices.is_empty():
    audio_input_device_dropdown.add_item("No Input Devices Found", 0)
    return

  if !input_devices.has(current_input_device):
    if input_devices.has(default_input_device):
      current_input_device = default_input_device
    else:
      current_input_device = input_devices[0]

  selected_index = input_devices.find(current_input_device)

  settings_prefrences.input_device = current_input_device
  audio_input_device_dropdown.DefaultItem = selected_index

  # Only auto-apply when the logical preference changed (e.g. missing device fallback).
  # Do not compare against AudioServer.input_device here because backend updates can lag.
  if previous_input_device != current_input_device:
    GlobalSignalServer.emit_signal("AssignInputDevice", current_input_device)

func _on_voice_chat_volume_slider_value_changed(value: float) -> void:
  # the audio bus takes values only from 0.0 to 1.0
  var normalized_value: float = value / 100.0
  # set the volume linearly 
  AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), normalized_value)

  # set the slider's number tag value
  # the given value is between 0.0 and 1.0 because that's the value that 
  vc_volume_slider.number_tag.text = str(int(value))

  # apply it to the settings prefrences object for potentially saving it
  settings_prefrences.voicechat_volume = normalized_value
  
  # Emit the signal only if the user actually clicked it
  if !is_loading_ui:
    setting_changed.emit()

func _on_main_volume_slider_slider_changed(new_value: float) -> void:
  # the audio bus takes values only from 0.0 to 1.0
  var normalized_value: float = new_value / 100.0
  # set the volume linearly 
  AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Master"), normalized_value)

  # set the slider's number tag value
  main_volume_slider.number_tag.text = str(int(new_value))

  # apply it to the settings prefrences object for potentially saving it
  settings_prefrences.master_volume = normalized_value
  
  # Emit the signal only if the user actually clicked it
  if !is_loading_ui:
    setting_changed.emit()

# this is ran by the parent settings script
func apply_settings() -> void:
  # save it and then apply them
  settings_prefrences.save()
  PrefrencesLoader.apply_audio_settings(settings_prefrences)

func _on_mic_input_dropdown_item_selected(item: int) -> void:
  if item < 0 or item >= input_devices.size():
    return

  var selected_device: String = input_devices[item]
  settings_prefrences.input_device = selected_device
  GlobalSignalServer.emit_signal("AssignInputDevice", selected_device)

  if !is_loading_ui:
    setting_changed.emit()

func _on_playe_movement_volume_slider_slider_changed(new_value: float) -> void:
  var normalized_value: float = new_value / 100.0
  AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("PlayerMovementSounds"), normalized_value)
  player_movement_volume_slider.number_tag.text = str(int(new_value))
  settings_prefrences.player_movement_volume = normalized_value

  if !is_loading_ui:
    setting_changed.emit()

func _on_music_volume_slider_slider_changed(new_value: float) -> void:
  var normalized_value: float = new_value / 100.0
  AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Music"), normalized_value)
  music_volume_slider.number_tag.text = str(int(new_value))
  settings_prefrences.music_volume = normalized_value

  if !is_loading_ui:
    setting_changed.emit()

func _on_environment_volume_slider_slider_changed(new_value: float) -> void:
  var normalized_value: float = new_value / 100.0
  AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Environment"), normalized_value)
  environment_volume_slider.number_tag.text = str(int(new_value))
  settings_prefrences.environment_volume = normalized_value

  if !is_loading_ui:
    setting_changed.emit()
