@tool
extends Control

signal slider_changed(new_value: float)
var _slider_tween: Tween

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

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Apply initial values once the children are guaranteed to exist
	slider.min_value = MinValue
	slider.max_value = MaxValue
	slider.step = Step
	slider.set_value_no_signal(StartingValue) # don't wanna emit the signal just to set the value
	slider.tick_count = TickCount

func _on_slider_value_changed(value: float) -> void:
	slider_changed.emit(value)

# NEW: Function to smoothly animate the slider
func set_health_smoothly(target_value: float, duration: float = 0.3) -> void:
	# If the slider is already currently animating, stop it so the new animation can take over safely
	if _slider_tween and _slider_tween.is_valid():
		_slider_tween.kill()
	
	# Create a new Tween
	_slider_tween = create_tween()
	
	# Optional: Make the animation feel smooth (eases out at the end)
	_slider_tween.set_trans(Tween.TRANS_SINE)
	_slider_tween.set_ease(Tween.EASE_OUT)
	
	# Tell the tween to animate the "value" property of the slider node to the target_value over X seconds
	_slider_tween.tween_property(slider, "value", target_value, duration)