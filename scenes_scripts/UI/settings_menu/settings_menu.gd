extends Control

# controls if the reset menu should be visible 
@export var reset_menu_visible: bool = true

# submenus
@onready var audio_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/AudioMenu
@onready var reset_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/ResetMenu
@onready var graphics_menu: Control = $MainContainer/HBoxContainer/VBoxContainer2/GraphicsMenu

# buttons
@onready var audio_button: Button = $MainContainer/HBoxContainer/VBoxContainer2/ApplySettingsButton
@onready var reset_button: Button = $MainContainer/HBoxContainer/VBoxContainer/ResetMenuButton

# --- NEW CONFIRM PROMPT NODES ---
@onready var confirm_prompt = $ConfirmPage
@onready var revert_timer = $RevertTimer

enum SubMenuVisibility {AUDIO, RESET, GRAPHICS}

# keep track of the current menu
var curr_menu: SubMenuVisibility = SubMenuVisibility.AUDIO

signal back_button_pressed

func _ready() -> void:
  # make the reset button visible depending on if they want it or not
  reset_button.visible = reset_menu_visible

# updates our countdown label every frame
func _process(_delta: float) -> void:
  if confirm_prompt.visible:
    confirm_prompt.set_countdown_label_text("Reverting in " + str(int(revert_timer.time_left)) + " seconds...")

func _on_audio_menu_button_pressed() -> void:
  _show_menu(SubMenuVisibility.AUDIO)

func _on_reset_menu_button_pressed() -> void:
  _show_menu(SubMenuVisibility.RESET)

func _on_graphics_menu_button_pressed() -> void:
  _show_menu(SubMenuVisibility.GRAPHICS)

func _on_back_button_pressed() -> void:
  # emit the back button being pressed signal so the parent node can see it
  back_button_pressed.emit() 

# make only the given menu visible and all others invisible
func _show_menu(menu: SubMenuVisibility) -> void:
  # make all others stuff invisible right here so that they get turned visible right after this
  audio_menu.visible = false
  reset_menu.visible = false
  graphics_menu.visible = false

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

func _on_apply_settings_button_pressed() -> void:
  # run the apply settings function for the specific submenu
  # just make sure that the function name is 'apply_settings()' for every submenu node
  match curr_menu:
    SubMenuVisibility.AUDIO:
      audio_menu.apply_settings()
    SubMenuVisibility.GRAPHICS:
      graphics_menu.apply_settings()
      # Trigger the visual prompt specifically for graphics changes
      confirm_prompt.visible = true
      revert_timer.start(15.0)

# --- CONFIRM / REVERT SIGNALS ---

func _on_confirm_button_pressed() -> void:
  revert_timer.stop()
  confirm_prompt.visible = false
  # Tell the graphics menu to permanently save the tested settings
  graphics_menu.confirm_settings()

func _on_revert_button_pressed() -> void:
  _revert_routine()

func _on_revert_timer_timeout() -> void:
  _revert_routine()

func _revert_routine() -> void:
  revert_timer.stop()
  confirm_prompt.visible = false
  # Tell the graphics menu to roll back to its safe backup
  graphics_menu.revert_settings()