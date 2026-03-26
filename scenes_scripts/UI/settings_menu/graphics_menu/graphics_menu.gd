extends Control

# get the sliders so we can edit their display properly
@onready var msaa_slider = $MarginContainer/ScrollContainer/VBoxContainer/MSAASlider
# get the screen mode dropdown menu
@onready var screen_mode_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/ScreenModeDropdown
# resolution drop down menu
@onready var resolution_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/ResolutionDropdown

# new dropdowns and sliders for the added settings
@onready var vsync_dropdown = $MarginContainer/ScrollContainer/VBoxContainer/VSyncDropdown
@onready var max_fps_slider = $MarginContainer/ScrollContainer/VBoxContainer/MaxFPSSlider
@onready var render_scale_slider = $MarginContainer/ScrollContainer/VBoxContainer/RenderScaleSlider

# the settings object
var settings_prefrences: UserSettingPrefrences

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
  Vector2i(2560, 1080)  # UWFHD (Ultrawide 1080p)
]

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
  # load their settings to see what they already have saved
  settings_prefrences = UserSettingPrefrences.load_or_create()

  # update all the ui upon loading in
  _load_ui_stuff()

func _load_ui_stuff() -> void:
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
  if settings_prefrences.display_mode == DisplayServer.WINDOW_MODE_FULLSCREEN ||\
  settings_prefrences.display_mode == DisplayServer.WINDOW_MODE_WINDOWED:
    resolution_dropdown.visible = true # make it visible
    # load all the resolutions into the resolution dropdown
    _load_all_resolutions()
    # load the current resolution into the dropdown menu
    # get the index of the resolution in the table, which will correlate to the index in the dropdown
    resolution_dropdown.DefaultItem = resolutions_array.find(settings_prefrences.resolution)
  else:
    # make it invisible if they're not in a window mode that makes sense
    resolution_dropdown.visible = false
    
  # VSYNC DROPDOWN
  # this assumes the items in your dropdown match the order of the DisplayServer.VSyncMode enum
  vsync_dropdown.DefaultItem = int(settings_prefrences.vsync_mode)

  # MAX FPS SLIDER
  # call the function first to set the display string properly, then set the starting value
  _on_max_fps_slider_slider_changed(settings_prefrences.max_fps)
  max_fps_slider.StartingValue = settings_prefrences.max_fps

  # RENDER SCALE SLIDER
  _on_render_scale_slider_slider_changed(settings_prefrences.render_scale)
  render_scale_slider.StartingValue = settings_prefrences.render_scale


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

func _on_resolution_dropdown_item_selected(item: int) -> void:
  # get the resoluiton and put it into the prefrences object
  var new_resolution: Vector2i = resolutions_array[item]
  settings_prefrences.resolution = new_resolution

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

func _on_max_fps_slider_slider_changed(new_value: float) -> void:
  settings_prefrences.max_fps = new_value
  
  var display_string: String = ""
  if new_value == 0:
    display_string = "UNLIMITED"
  else:
    display_string = str(int(new_value))
    
  max_fps_slider.change_number_display_tag(display_string)

func _on_render_scale_slider_slider_changed(new_value: float) -> void:
  settings_prefrences.render_scale = new_value
  
  # change the float into a percentage string (e.g. 0.75 becomes 75%)
  var display_string: String = str(int(new_value * 100)) + "%"
  render_scale_slider.change_number_display_tag(display_string)

# settings are only applied when this button is pressed
# TODO: have a confirm page that resets the settings to the previous values either if they choose to revert them, or if 15 sec has gone by without any input
# this is ran by the parent settings script
func apply_settings() -> void:
  # just save and apply all the settings
  settings_prefrences.save()
  PrefrencesLoader.apply_graphics_settings(settings_prefrences)