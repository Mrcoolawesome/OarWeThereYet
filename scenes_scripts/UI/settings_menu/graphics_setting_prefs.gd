class_name UserSettingPrefrences extends Resource

# class variables
# these are also the default values for the settings
@export var msaa_mode: Viewport.MSAA = Viewport.MSAA_2X
@export var display_mode: DisplayServer.WindowMode = DisplayServer.WINDOW_MODE_FULLSCREEN
@export var display_flag: DisplayServer.WindowFlags = DisplayServer.WINDOW_FLAG_BORDERLESS
@export var borderless_enable: bool = false
@export var resolution: Vector2i = DisplayServer.screen_get_size(DisplayServer.window_get_current_screen()) # set the resolution to their monitor's resolution by default
@export var apply_resolution: bool = false

# saves the current instance of the class (self) into a file called 'user_settings_prefs.tres'
func save() -> void:
	ResourceSaver.save(self, "user://user_settings_prefs.tres")

# this is a static function so that it doesn't get called on a specific instance, since it return instances of UserSettingPrefrences objects
static func load_or_create() -> UserSettingPrefrences:
	# first try and get their settings
	var res: UserSettingPrefrences = ResourceLoader.load("user://user_settings_prefs.tres") as UserSettingPrefrences
	if !res:
		return UserSettingPrefrences.new() # return a new instance of this class if we don't have a save file for their settings yet
	else:
		return res # otherwise just return their settings from the file