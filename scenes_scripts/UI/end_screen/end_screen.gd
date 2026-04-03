extends Control

# How fast the credits roll (pixels per second)
@export var scroll_speed: float = 50.0 

@onready var plug_socials = $PlugSocials
@onready var credits: RichTextLabel = $Credits
@onready var tic_tok: TextureRect = $PlugSocials/HBoxContainer/MarginContainer/TicTok
@onready var instagram: TextureRect = $PlugSocials/HBoxContainer/MarginContainer3/InstagramIcon
@onready var discord: TextureRect = $PlugSocials/HBoxContainer/MarginContainer2/DiscordIcon

# We use this to prevent them from skipping before the 10 seconds is up
var can_skip: bool = false

func _ready() -> void:
  # Hide everything at the very beginning
  visible = false
  modulate.a = 0.0 
  plug_socials.visible = true
  plug_socials.modulate.a = 1.0
  credits.visible = false
  credits.modulate.a = 0.0

  # Injecting the dummy text directly via code so it's ready to go!
  credits.bbcode_enabled = true
  credits.text = "[center]\n[font_size=40][b]RAFT CANYON[/b][/font_size]\n\n[b]Lead Developer[/b]\nDevin\n\n[b]Art & Animation[/b]\nCool Artist Name\n\n[b]Music & Sound[/b]\nAudio Genius\n\n[b]Special Thanks[/b]\nMy Cat\nCoffee\nGodot Engine\n[/center]"

  # Start the credits off the bottom of the screen
  credits.position.y = get_viewport_rect().size.y

# The Player.cs script will call this function!
func start_end_game_sequence() -> void:
  visible = true
  
  # 1. Fade the entire EndScreen in
  var start_tween = create_tween()
  start_tween.tween_property(self, "modulate:a", 1.0, 2.0)
  
  # 2. Wait 20 seconds while the socials are on screen
  await get_tree().create_timer(20.0).timeout
  
  # 3. Fade out the socials
  var fade_socials = create_tween()
  fade_socials.tween_property(plug_socials, "modulate:a", 0.0, 1.5)
  fade_socials.finished.connect(_start_credits)

func _start_credits() -> void:
  plug_socials.visible = false
  credits.visible = true
  
  # 4. Fade in the credits
  var fade_credits = create_tween()
  fade_credits.tween_property(credits, "modulate:a", 1.0, 1.5)
  
  # 5. Calculate travel distance and start the rolling animation
  var travel_distance: float = get_viewport_rect().size.y + credits.get_content_height()
  var duration: float = travel_distance / scroll_speed
  
  var scroll_tween = create_tween()
  scroll_tween.tween_property(credits, "position:y", -credits.get_content_height(), duration)
  
  # 6. If they watch the whole thing without skipping, auto-quit at the end
  scroll_tween.finished.connect(_go_to_main_menu)

  # 7. Wait 10 seconds before allowing the player to skip early
  await get_tree().create_timer(10.0).timeout
  can_skip = true

func _input(event: InputEvent) -> void:
  # If 10 seconds of credits have passed, ANY button press quits the game
  if can_skip and event.is_pressed():
    can_skip = false 
    _go_to_main_menu()

# Added a quick helper function so both the auto-finish and skip button use the same logic
func _go_to_main_menu() -> void:
  GlobalSignalServer.emit_signal("GoToMainMenu")

# --- HYPERLINKS ---
# OS.shell_open() tells the player's computer to open their default web browser

# Make sure you connect the "pressed" signal on your new buttons to these functions!
func _on_instagram_button_pressed() -> void:
  OS.shell_open("https://google.com")

func _on_tic_tok_button_pressed() -> void:
  OS.shell_open("https://google.com")

func _on_discord_button_pressed() -> void:
  OS.shell_open("https://google.com")