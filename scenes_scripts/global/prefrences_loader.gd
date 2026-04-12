extends Node


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
    # load their settings or make a new settings save with the defaults defined in UserSettingPrefrences
    var user_settings_prefs: UserSettingPrefrences = UserSettingPrefrences.load_or_create()
    # VERY IMPORTANT: APPLY ALL SETTINGS IMMEDATELY UPON LOADING IN
    apply_all_settings(user_settings_prefs) 

# applies all of their settings
# we need to pass in certain things because this calss extends resource and thus doesn't have access to things that node classes do
func apply_all_settings(user_prefs: UserSettingPrefrences) -> void:
    apply_graphics_settings(user_prefs)
    apply_audio_settings(user_prefs)
    # TODO: apply all other settings here

# i make all of these functions take in the user settings prefrence object so they can be used outside of here
# applies only graphics settings
func apply_graphics_settings(user_prefs: UserSettingPrefrences) -> void:
    get_viewport().msaa_3d = user_prefs.msaa_mode
    DisplayServer.window_set_mode(user_prefs.display_mode)
    DisplayServer.window_set_flag(user_prefs.display_flag, user_prefs.borderless_enable) # this sets the gvien flag to false or true
    get_window().size = user_prefs.resolution # this is the correct way of doing it so that godot knows that the resolution has been changed
    
    # apply basic graphics settings
    DisplayServer.window_set_vsync_mode(user_prefs.vsync_mode)
    Engine.max_fps = int(user_prefs.max_fps)
    get_viewport().scaling_3d_scale = user_prefs.render_scale
    
    # apply advanced viewport settings
    get_viewport().use_taa = user_prefs.taa_enable
    get_viewport().scaling_3d_mode = user_prefs.upscaler_mode
    
    # apply shadow quality settings
    # maps 0, 1, 2, 3 to resolution sizes for the shadow atlases
    var shadow_size: int = 0
    match user_prefs.shadow_quality:
        0: shadow_size = 1024 # Low
        1: shadow_size = 2048 # Medium
        2: shadow_size = 4096 # High
        3: shadow_size = 8192 # Ultra
        
    # Set Omni/SpotLight shadow resolution
    get_viewport().positional_shadow_atlas_size = shadow_size
    # Set DirectionalLight (Sun) shadow resolution
    RenderingServer.directional_shadow_atlas_set_size(shadow_size, true)

    # apply lighting quality settings (Requires the level's WorldEnvironment to have these enabled to be seen)
    # SSAO Mapping: 0=Very Low, 1=Low, 2=Medium, 3=High
    RenderingServer.environment_set_ssao_quality(user_prefs.ssao_quality, true, 0.5, 2, 50.0, 300.0)
    
    # SDFGI Mapping: 0=Low(16 rays), 1=High(64 rays) 
    var sdfgi_rays = RenderingServer.ENV_SDFGI_RAY_COUNT_16
    if user_prefs.sdfgi_quality == 1:
        sdfgi_rays = RenderingServer.ENV_SDFGI_RAY_COUNT_64
    RenderingServer.environment_set_sdfgi_ray_count(sdfgi_rays)

# applies only audio settings
func apply_audio_settings(user_prefs: UserSettingPrefrences) -> void:
    AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Voice Chat"), user_prefs.voicechat_volume)
    AudioServer.set_bus_volume_linear(AudioServer.get_bus_index("Master"), user_prefs.master_volume)

    var default_input_device: String = AudioServer.input_device
    if user_prefs.input_device != "":
        # Apply directly first so we don't lose the saved device to early startup timing.
        AudioServer.input_device = user_prefs.input_device

        var available_input_devices: PackedStringArray = AudioServer.get_input_device_list()
        if !available_input_devices.has(user_prefs.input_device) and available_input_devices.has(default_input_device):
            AudioServer.input_device = default_input_device
            user_prefs.input_device = default_input_device

func apply_controls_settings(user_prefs: UserSettingPrefrences) -> void:
    GlobalSignalServer.emit_signal("ApplyPlayerLookSpeed", user_prefs.look_speed)