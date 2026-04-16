extends Control

# NEW SIGNAL: Lets parent know a change happened
signal setting_changed

# get the sliders so we can edit their display properly
@onready var msaa_slider = $MarginContainer/ScrollContainer/VBoxContainer/MSAASlider
# get the screen mode dropdown menu
@onready var screen_mode_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/ScreenModeDropdown
# resolution drop down menu
@onready var resolution_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/ResolutionDropdown

# scroll bar item
@onready var scroll_container = $MarginContainer/ScrollContainer

# new dropdowns and sliders for the added settings
@onready var vsync_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/VSyncDropdown
@onready var max_fps_slider = $MarginContainer/ScrollContainer/VBoxContainer/MaxFPSSlider
@onready var fov_slider = $MarginContainer/ScrollContainer/VBoxContainer/FOVSlider
@onready var render_scale_slider = $MarginContainer/ScrollContainer/VBoxContainer/RenderScaleSlider
@onready var shadow_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/ShadowDropdown

# lighting and advanced graphics nodes
@onready var taa_toggle = $MarginContainer/ScrollContainer/VBoxContainer/TAAToggle # CheckBox/CheckButton
@onready var upscaler_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/UpscalerDropdown # 0: Disabled, 1: FSR 1.0, 2: FSR 2.2
@onready var ssao_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/SSAODropdown # 0: Very Low, 1: Low, 2: Medium, 3: High
@onready var sdfgi_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/SDFGIDropdown # 0: Low, 1: High

# the settings object
var settings_prefrences: UserSettingPrefrences
# backup settings object to safely hold the old settings before confirming
var cached_settings: UserSettingPrefrences

var is_loading_ui: bool = false # Prevents signals from firing when UI boots

# all the resolutions they're able to select
var resolutions_array: Array[Vector2i] = [
  # 16:9 resolutions (Standard Widescreen)
  Vector2i(3840, 2160), # 4K / UHD
  Vector2i(2560, 1440), # 1440p / QHD (2K)
  Vector2i(1920, 1080), # 1080p / FHD
  Vector2i(1600, 900),  # 900p / HD+
  Vector2i(1280, 720),  # 720p / HD

  # 16:10 resolutions (Common on modern laptops and MacBooks)
  Vector2i(3840, 2400), # WQUXGA (4K equivalent)
  Vector2i(2560, 1600), # WQXGA (1600p)
  Vector2i(1920, 1200), # WUXGA (1200p)
  Vector2i(1440, 900),  # WXGA+
  Vector2i(1280, 800),  # WXGA

  # 21:9 resolutions (Ultrawide Monitors)
  Vector2i(5120, 2160), # 5K2K / WUHD (Ultrawide 4K equivalent)
  Vector2i(3440, 1440), # UWQHD (Ultrawide 1440p)
  Vector2i(2560, 1080), # UWFHD (Ultrawide 1080p)

  # 32:9 resolutions (Super Ultrawide Monitors)
  Vector2i(7680, 2160), # Dual 4K width / DQHD+
  Vector2i(5120, 1440), # DQHD (Dual 1440p)
  Vector2i(3840, 1080), # DFHD (Dual 1080p)

  # 3:2 resolutions (Common on Surface / Productivity laptops)
  Vector2i(2160, 1440), # 1440p 3:2
  Vector2i(1920, 1280), # 1280p 3:2

  # 4:3 resolutions (Retro/CRT monitors)
  Vector2i(1600, 1200), # UXGA
  Vector2i(1024, 768),  # XGA
  Vector2i(800, 600)    # SVGA
]

func _ready() -> void:
  # load their settings to see what they already have saved
  settings_prefrences = UserSettingPrefrences.load_or_create()
  
  # make a safe deep copy of the settings so we have something to revert back to
  cached_settings = settings_prefrences.duplicate(true)

  # update all the ui upon loading in
  _load_ui_stuff()
  
  # NEW: Connect the visibility signal
  visibility_changed.connect(_on_visibility_changed)

# NEW: Snap the scrollbar back to the top when opened
func _on_visibility_changed() -> void:
  if visible and scroll_container:
    scroll_container.scroll_vertical = 0

func _load_ui_stuff() -> void:
  # Lock the signals while we load the UI
  is_loading_ui = true

  # MSAA SLIDER
  # set the number display to what they already have by default
  # this is type casting to an int because the .msaa_mode value is an enum
  var loaded_msaa_mode: float = int(settings_prefrences.msaa_mode) as float
  _on_msaa_slider_slider_changed(loaded_msaa_mode) # this needs to be a float because that's what the function takes in due to that being what the slider emits
  # then set the sliders default value as well so the grabber is in the right spot
  msaa_slider.StartingValue = loaded_msaa_mode

  # WINDOW MODE DROPDOWN
  # set the screen mode dropdown default value
  var default_item: int = 0
  match settings_prefrences.display_mode:
    DisplayServer.WINDOW_MODE_FULLSCREEN:
      default_item = 0
    DisplayServer.WINDOW_MODE_MAXIMIZED:
      default_item = 1
    DisplayServer.WINDOW_MODE_WINDOWED:
      default_item = 2
  screen_mode_dropdown.DefaultItem = default_item

  # RESOLUTION DROPDOWN
  # only display this if they've selected either windowed or fullscreen
  if settings_prefrences.display_mode == DisplayServer.WINDOW_MODE_WINDOWED:
    resolution_dropdown.visible = true # make it visible
    # load all the resolutions into the resolution dropdown
    _load_all_resolutions()
    # load the current resolution into the dropdown menu
    # get the index of the resolution in the table, which will correlate to the index in the dropdown
    resolution_dropdown.DefaultItem = resolutions_array.find(settings_prefrences.resolution)
  else:
    # make it invisible if they're not in a window mode that makes sense (aka if they're in fullscreen or borderless mode)
    resolution_dropdown.visible = false
    
  # VSYNC DROPDOWN
  # this assumes the items in your dropdown match the order of the DisplayServer.VSyncMode enum
  vsync_dropdown.DefaultItem = int(settings_prefrences.vsync_mode)

  # MAX FPS SLIDER
  # call the function first to set the display string properly, then set the starting value
  _on_max_fps_slider_slider_changed(settings_prefrences.max_fps)
  max_fps_slider.StartingValue = settings_prefrences.max_fps

  # FOV SLIDER
  var loaded_fov: float = clamp(settings_prefrences.player_fov, 1.0, 179.0)
  if loaded_fov != settings_prefrences.player_fov:
    settings_prefrences.player_fov = loaded_fov
  _on_fov_slider_slider_changed(loaded_fov)
  fov_slider.StartingValue = loaded_fov

  # RENDER SCALE SLIDER
  _on_render_scale_slider_slider_changed(settings_prefrences.render_scale)
  render_scale_slider.StartingValue = settings_prefrences.render_scale

  # SHADOW QUALITY DROPDOWN
  # 0 = Low, 1 = Medium, 2 = High, 3 = Ultra
  shadow_dropdown.DefaultItem = settings_prefrences.shadow_quality

  # ADVANCED GRAPHICS
  taa_toggle.DefaultState = settings_prefrences.taa_enable
  upscaler_dropdown.DefaultItem = int(settings_prefrences.upscaler_mode)
  ssao_dropdown.DefaultItem = int(settings_prefrences.ssao_quality)
  sdfgi_dropdown.DefaultItem = int(settings_prefrences.sdfgi_quality)
  
  # Set initial visibility of the upscaler dropdown upon loading in
  upscaler_dropdown.visible = settings_prefrences.render_scale < 1.0

  # Unlock the signals now that the UI is done setting up
  is_loading_ui = false

func _on_msaa_slider_slider_changed(new_value: float) -> void:
  var converted_value: Viewport.MSAA = int(new_value) as Viewport.MSAA # get the value in terms of the MSAA enum
  settings_prefrences.msaa_mode = converted_value # store the value in the temp settings object

  # set the display depending on the mode
  var display_string: String = ""
  match converted_value:
    Viewport.MSAA_DISABLED:
      display_string = "DISABLED"
    Viewport.MSAA_2X:
      display_string = "2x"
    Viewport.MSAA_4X:
      display_string = "4x"
    Viewport.MSAA_8X:
      display_string = "8x"

  # change the number display to the display string
  msaa_slider.change_number_display_tag(display_string)
  
  if !is_loading_ui:
    setting_changed.emit()

# sets the dropdown value in the settings object
func _on_screen_mode_dropdown_item_selected(item: int) -> void:
  var converted_value: DisplayServer.WindowMode = item as DisplayServer.WindowMode
  var new_display_mode: DisplayServer.WindowMode
  var new_display_flag: DisplayServer.WindowFlags = DisplayServer.WINDOW_FLAG_BORDERLESS
  var borderless_enable: bool = false
  # set the display mode based on what they select
  match converted_value:
    0:
      new_display_mode = DisplayServer.WINDOW_MODE_FULLSCREEN
      # make the resoution dropdown visible
      resolution_dropdown.visible = true
    1:  
      # this is fullscreen borderless
      new_display_mode = DisplayServer.WINDOW_MODE_MAXIMIZED
      borderless_enable = true
      resolution_dropdown.visible = false
      # set the display resolution to be the full resolution
      settings_prefrences.resolution = DisplayServer.screen_get_size(DisplayServer.window_get_current_screen())
    2: 
      new_display_mode = DisplayServer.WINDOW_MODE_WINDOWED
      resolution_dropdown.visible = true

  # apply everything to the prefrences object
  settings_prefrences.display_mode = new_display_mode
  settings_prefrences.display_flag = new_display_flag
  settings_prefrences.borderless_enable = borderless_enable

  if !is_loading_ui:
    setting_changed.emit()

func _on_resolution_dropdown_item_selected(item: int) -> void:
  # get the resoluiton and put it into the prefrences object
  var new_resolution: Vector2i = resolutions_array[item]
  settings_prefrences.resolution = new_resolution
  
  if !is_loading_ui:
    setting_changed.emit()

# function to pass in all the resolutions into the resolution selection dropdown
func _load_all_resolutions() -> void:
  # get the max screen size
  var max_screen_size: Vector2i = DisplayServer.screen_get_size(DisplayServer.window_get_current_screen())
  # load in each resolution into the dropdown that are smaller than their displays
  var index: int = 0
  for resolution in resolutions_array:
    # gemini made this boolean i don't really get why it works but it does so whatever
    var aspect_is_same: bool = (resolution.x * max_screen_size.y) == (max_screen_size.x * resolution.y)
    if (resolution.x <= max_screen_size.x && resolution.y <= max_screen_size.y) && aspect_is_same:
      # turn it into a string
      var string_display_version: String = str(resolution.x) + "x" + str(resolution.y)
      resolution_dropdown.add_item(string_display_version, index) # need to keep track of the actual index so i know what they're selecting
    index += 1

func _on_vsync_dropdown_item_selected(item: int) -> void:
  var converted_value: DisplayServer.VSyncMode = item as DisplayServer.VSyncMode
  settings_prefrences.vsync_mode = converted_value
  
  if !is_loading_ui:
    setting_changed.emit()

func _on_max_fps_slider_slider_changed(new_value: float) -> void:
  settings_prefrences.max_fps = new_value
  
  var display_string: String = ""
  if new_value == 0:
    display_string = "UNLIMITED"
  else:
    display_string = str(int(new_value))
    
  max_fps_slider.change_number_display_tag(display_string)

  if !is_loading_ui:
    setting_changed.emit()

func _on_render_scale_slider_slider_changed(new_value: float) -> void:
  settings_prefrences.render_scale = new_value
  
  # change the float into a percentage string (e.g. 0.75 becomes 75%)
  var display_string: String = str(int(new_value * 100)) + "%"
  render_scale_slider.change_number_display_tag(display_string)

  # UX LOGIC: Hide and reset the upscaler if render scale is 100% or higher
  if new_value >= 1.0:
    upscaler_dropdown.visible = false
    # Force the backend setting to Bilinear (0) to avoid Godot warnings
    settings_prefrences.upscaler_mode = Viewport.SCALING_3D_MODE_BILINEAR
    # Visually update the dropdown to default back to index 0 ("Disabled")
    upscaler_dropdown.DefaultItem = 0 
  else:
    # Reveal the dropdown if they drop below 100%
    upscaler_dropdown.visible = true

  if !is_loading_ui:
    setting_changed.emit()

func _on_shadow_dropdown_item_selected(item: int) -> void:
  settings_prefrences.shadow_quality = item
  
  if !is_loading_ui:
    setting_changed.emit()

func _on_taa_toggle_toggled(toggled_on: bool) -> void:
  if settings_prefrences != null:
    settings_prefrences.taa_enable = toggled_on
    
  if !is_loading_ui:
    setting_changed.emit()

func _on_upscaler_dropdown_item_selected(item: int) -> void:
  settings_prefrences.upscaler_mode = item as Viewport.Scaling3DMode
  
  if !is_loading_ui:
    setting_changed.emit()

func _on_ssao_dropdown_item_selected(item: int) -> void:
  settings_prefrences.ssao_quality = item as RenderingServer.EnvironmentSSAOQuality
  
  if !is_loading_ui:
    setting_changed.emit()

func _on_sdfgi_dropdown_item_selected(item: int) -> void:
  settings_prefrences.sdfgi_quality = item
  
  if !is_loading_ui:
    setting_changed.emit()


# --- DATA MANAGEMENT LOGIC ---

# settings are only applied when this button is pressed
# this is ran by the parent settings script
func apply_settings() -> void:
  # Apply the settings visually to the engine so the player can see the changes
  PrefrencesLoader.apply_graphics_settings(settings_prefrences)

# Called by parent if the player clicks "Confirm"
func confirm_settings() -> void:
  settings_prefrences.save()
  # Update our safe backup to match the newly confirmed settings
  cached_settings = settings_prefrences.duplicate(true)

# Called by parent if the player clicks "Revert" or the timer runs out
func revert_settings() -> void:
  # Overwrite our dirty settings with our safe backup
  settings_prefrences = cached_settings.duplicate(true)
  # Apply the safe backup settings back to the engine
  PrefrencesLoader.apply_graphics_settings(settings_prefrences)
  # Visually update the sliders/dropdowns to reflect the rollback
  _load_ui_stuff()

func _on_fov_slider_slider_changed(new_value: float) -> void:
  var clamped_fov: float = clamp(new_value, 1.0, 120.0)
  settings_prefrences.player_fov = clamped_fov

  var display_string: String = "Quake Pro" if int(clamped_fov) == 120 else str(int(clamped_fov))
  fov_slider.change_number_display_tag(display_string)

  if !is_loading_ui:
    setting_changed.emit()
