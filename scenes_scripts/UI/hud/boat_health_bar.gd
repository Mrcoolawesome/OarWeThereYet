@tool
extends Control

signal slider_changed(new_value: float)
var _slider_tween: Tween

# --- NEW: Colors for the health bar ---
@export var HealthyColor: Color = Color.GREEN
@export var DeadColor: Color = Color.RED

# Exported variables with setters to update the UI live in the editor
@export var MinValue: float = 0.0:
	set(value):
		MinValue = value
		if is_node_ready() and slider:
			slider.min_value = value

@export var MaxValue: float = 100.0:
	set(value):
		MaxValue = value
		if is_node_ready() and slider:
			slider.max_value = value

@export var Step: float = 1.0:
	set(value):
		Step = value
		if is_node_ready() and slider:
			slider.step = value

@export var StartingValue: float = 0.0:
	set(value):
		StartingValue = value
		if is_node_ready() and slider:
			slider.value = value

@export var TickCount: int = 0:
	set(value):
		TickCount = value
		if is_node_ready() and slider:
			slider.tick_count = value

@onready var slider: Slider = $MarginContainer/HBoxContainer/Slider

# --- NEW: Variables to hold our duplicated styleboxes ---
var _grabber_style: StyleBoxFlat
var _grabber_highlight_style: StyleBoxFlat

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Apply initial values once the children are guaranteed to exist
	slider.min_value = MinValue
	slider.max_value = MaxValue
	slider.step = Step
	slider.tick_count = TickCount
	
	# --- NEW: Setup the StyleBoxes ---
	# We must duplicate them so we don't accidentally tint every other slider in the game
	var base_style = slider.get_theme_stylebox("grabber_area")
	if base_style is StyleBoxFlat:
		_grabber_style = base_style.duplicate()
		slider.add_theme_stylebox_override("grabber_area", _grabber_style)
		
	var base_highlight = slider.get_theme_stylebox("grabber_area_highlight")
	if base_highlight is StyleBoxFlat:
		_grabber_highlight_style = base_highlight.duplicate()
		slider.add_theme_stylebox_override("grabber_area_highlight", _grabber_highlight_style)

	# Set the initial value and color
	slider.set_value_no_signal(StartingValue)
	_update_bar_color(StartingValue)


func _on_slider_value_changed(value: float) -> void:
	# Update the color every time the value changes (which happens smoothly during the Tween)
	_update_bar_color(value)
	slider_changed.emit(value)


# --- NEW: Math to calculate the color ---
func _update_bar_color(current_value: float) -> void:
	# Ensure we actually have the styleboxes before trying to color them
	if not _grabber_style or not _grabber_highlight_style:
		return
		
	var total_range = MaxValue - MinValue
	if total_range <= 0:
		return # Prevent division by zero errors
		
	# Calculate how full the bar is from 0.0 (empty) to 1.0 (full)
	var health_percentage = (current_value - MinValue) / total_range
	health_percentage = clamp(health_percentage, 0.0, 1.0)
	
	# lerp mixes the two colors based on the percentage
	var current_color = DeadColor.lerp(HealthyColor, health_percentage)
	
	# Apply the color to both styleboxes
	_grabber_style.bg_color = current_color
	_grabber_highlight_style.bg_color = current_color


# Function to smoothly animate the slider
func set_health_smoothly(target_value: float, duration: float = 0.5) -> void:
	# If the slider is already currently animating, stop it
	if _slider_tween and _slider_tween.is_valid():
		_slider_tween.kill()
	
	# Create a new Tween
	_slider_tween = create_tween()
	
	# TRANS_QUINT makes it start extremely fast and drastically slow down at the end.
	_slider_tween.set_trans(Tween.TRANS_QUINT)
	
	# EASE_OUT tells the engine to put that "slow down" effect at the OUT-put (the end) of the animation
	_slider_tween.set_ease(Tween.EASE_OUT)
	
	# Tell the tween to animate the "value" property
	_slider_tween.tween_property(slider, "value", target_value, duration)