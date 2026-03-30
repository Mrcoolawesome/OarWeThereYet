extends Control

# controls if the reset menu should be visible 
@export var reset_menu_visible: bool = true

# submenus
@onready var audio_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/AudioMenu
@onready var reset_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/ResetMenu
@onready var graphics_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/GraphicsMenu
@onready var controls_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/ControlsMenu

# buttons
@onready var audio_button: Button = $MainContainer/HBoxContainer/VBoxContainer2/ApplySettingsButton
@onready var reset_button: Button = $MainContainer/HBoxContainer/VBoxContainer/ResetMenuButton

# --- NEW CONFIRM PROMPT NODES ---
@onready var confirm_prompt = $ConfirmPage
@onready var revert_timer = $RevertTimer

enum SubMenuVisibility {AUDIO, RESET, GRAPHICS, CONTROLS}

# keep track of the current menu
var curr_menu: SubMenuVisibility = SubMenuVisibility.AUDIO

# --- STATE TRACKERS ---
var unsaved_audio: bool = false
var unsaved_graphics: bool = false
var unsaved_controls: bool = false # NEW: Tracks unsaved controls
var waiting_to_exit: bool = false # Tracks if we hit the Back button and are waiting for the revert timer

signal back_button_pressed

func _ready() -> void:
	# make the reset button visible depending on if they want it or not
	reset_button.visible = reset_menu_visible
	
	# Connect the new signals from our child menus
	audio_menu.setting_changed.connect(_on_audio_setting_changed)
	graphics_menu.setting_changed.connect(_on_graphics_setting_changed)
	controls_menu.setting_changed.connect(_on_controls_setting_changed) # NEW: Connect controls signal

# updates our countdown label every frame
func _process(_delta: float) -> void:
	if confirm_prompt.visible:
		confirm_prompt.set_countdown_label_text("Reverting in " + str(int(revert_timer.time_left)) + " seconds...")

# --- SIGNAL RECEIVERS FROM CHILD MENUS ---
func _on_audio_setting_changed() -> void:
	unsaved_audio = true

func _on_graphics_setting_changed() -> void:
	unsaved_graphics = true

func _on_controls_setting_changed() -> void:
	unsaved_controls = true # NEW: Flag controls as dirty

# --- MENU NAVIGATION ---
func _on_audio_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.AUDIO)

func _on_reset_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.RESET)

func _on_graphics_menu_button_pressed() -> void:
	_show_menu(SubMenuVisibility.GRAPHICS)

func _on_controls_menu_button_pressed() -> void: # NEW: Button press for controls menu
	_show_menu(SubMenuVisibility.CONTROLS)

# make only the given menu visible and all others invisible
func _show_menu(menu: SubMenuVisibility) -> void:
	# make all others stuff invisible right here so that they get turned visible right after this
	audio_menu.visible = false
	reset_menu.visible = false
	graphics_menu.visible = false
	controls_menu.visible = false # NEW: Hide controls menu by default

	# want this to be visible by default unless told otherwise
	audio_button.visible = true
	match menu:
		SubMenuVisibility.AUDIO:
			audio_menu.visible = true
			curr_menu = SubMenuVisibility.AUDIO
		SubMenuVisibility.RESET:
			reset_menu.visible = true
			curr_menu = SubMenuVisibility.RESET
			audio_button.visible = false
		SubMenuVisibility.GRAPHICS:
			graphics_menu.visible = true
			curr_menu = SubMenuVisibility.GRAPHICS
		SubMenuVisibility.CONTROLS: # NEW: Show controls menu
			controls_menu.visible = true
			curr_menu = SubMenuVisibility.CONTROLS

# --- BACK BUTTON / EXIT LOGIC ---
func _on_back_button_pressed() -> void:
	if unsaved_audio or unsaved_graphics or unsaved_controls: # NEW: Added unsaved_controls to the check
		waiting_to_exit = true
		
		# Save audio immediately if dirty (no prompt needed for audio)
		if unsaved_audio:
			audio_menu.apply_settings()
			unsaved_audio = false
			
		# Save controls immediately if dirty (no prompt needed for controls)
		if unsaved_controls:
			controls_menu.apply_settings()
			controls_menu.confirm_settings() # Auto-confirm so the cached_settings update
			unsaved_controls = false
		
		# Trigger graphics prompt if dirty
		if unsaved_graphics:
			graphics_menu.apply_settings()
			confirm_prompt.visible = true
			revert_timer.start(15.0)
		else:
			# If only audio/controls were unsaved, they are saved now, so exit safely
			back_button_pressed.emit()
	else:
		# No unsaved changes, just exit normally
		back_button_pressed.emit() 

func _on_apply_settings_button_pressed() -> void:
	# run the apply settings function for the specific submenu
	match curr_menu:
		SubMenuVisibility.AUDIO:
			audio_menu.apply_settings()
			unsaved_audio = false # Changes saved, clear the flag
		SubMenuVisibility.CONTROLS: # NEW: Apply logic for controls
			controls_menu.apply_settings()
			controls_menu.confirm_settings() # Auto-confirm so the cached_settings update
			unsaved_controls = false # Changes saved, clear the flag
		SubMenuVisibility.GRAPHICS:
			graphics_menu.apply_settings()
			# Trigger the visual prompt specifically for graphics changes
			confirm_prompt.visible = true
			revert_timer.start(15.0)

# --- CONFIRM / REVERT SIGNALS ---
func _on_confirm_button_pressed() -> void:
	revert_timer.stop()
	confirm_prompt.visible = false
	graphics_menu.confirm_settings()
	
	unsaved_graphics = false # Changes confirmed, clear the flag
	
	if waiting_to_exit:
		back_button_pressed.emit()

func _on_revert_button_pressed() -> void:
	_revert_routine()

func _on_revert_timer_timeout() -> void:
	_revert_routine()

func _revert_routine() -> void:
	revert_timer.stop()
	confirm_prompt.visible = false
	graphics_menu.revert_settings()
	
	unsaved_graphics = false # Changes reverted, clear the flag
	
	if waiting_to_exit:
		waiting_to_exit = false
		back_button_pressed.emit()