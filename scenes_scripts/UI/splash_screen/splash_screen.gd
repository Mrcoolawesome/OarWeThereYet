extends Control

@onready var blackBackground: ColorRect = $blackBackground
@onready var logoContainer: MarginContainer = $logo

# We don't need _ready or _process for this, so I removed them to keep it clean.

func play_fade_animation() -> void:
	# Make sure the splash screen is visible to start
	visible = true
	
	# Start states: Background fully opaque, logo fully transparent
	blackBackground.modulate.a = 1.0
	logoContainer.modulate.a = 0.0
	
	var tween = create_tween()
	
	# 1. Fade IN the logo over 1.5 seconds
	tween.tween_property(logoContainer, "modulate:a", 1.0, 1.5)
	
	# 2. Wait 2 seconds so the player can see the logo
	tween.tween_interval(2.0)
	
	# 3. Fade OUT the logo over 1.5 seconds
	tween.tween_property(logoContainer, "modulate:a", 0.0, 1.5)
	
	# 4. Fade OUT the black background over 1.5 seconds (revealing the main menu)
	tween.tween_property(blackBackground, "modulate:a", 0.0, 1.5)
	
	# 5. Hide the splash screen completely once done so it doesn't block mouse clicks!
	tween.finished.connect(_on_animation_finished)

func _on_animation_finished() -> void:
	visible = false