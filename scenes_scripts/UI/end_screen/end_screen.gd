extends Control

@onready var plug_socials = $PlugSocials
@onready var credits = $Credits
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
	
	# (If you have a scrolling script attached to a RichTextLabel here, 
	# this is where you would tell it to start rolling!)
	
	# 5. Wait 10 seconds before allowing the player to skip
	await get_tree().create_timer(10.0).timeout
	can_skip = true

func _input(event: InputEvent) -> void:
	# If 10 seconds of credits have passed, ANY button press quits the game
	if can_skip and event.is_pressed():
		# Prevent them from spamming the button and firing this 5 times
		can_skip = false 
		# Call your C# GlobalSignalServer from GDScript!
		GlobalSignalServer.emit_signal("GoToMainMenu")

# --- HYPERLINKS ---
# OS.shell_open() tells the player's computer to open their default web browser

func _on_instagram_icon_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		OS.shell_open("https://google.com")

func _on_tic_tok_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		OS.shell_open("https://google.com")

func _on_discord_icon_gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		OS.shell_open("https://google.com")