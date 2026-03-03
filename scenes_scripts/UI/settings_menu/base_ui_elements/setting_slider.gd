@tool
extends Control

signal slider_changed(new_value: float)

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

@export var SliderLabelText: String = "":
	set(value):
		SliderLabelText = value
		if is_node_ready() and slider_label:
			slider_label.text = value

@export var TickCount: int = 0:
	set(value):
		TickCount = value
		if is_node_ready() and slider:
			slider.tick_count = value

@onready var slider: Slider = $MarginContainer/HBoxContainer/Slider
@onready var number_tag: Label = $MarginContainer/HBoxContainer/SliderNumberLabel
@onready var slider_label: Label = $MarginContainer/HBoxContainer/SliderNameLabel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Apply initial values once the children are guaranteed to exist
	slider.min_value = MinValue
	slider.max_value = MaxValue
	slider.step = Step
	slider.set_value_no_signal(StartingValue) # don't wanna emit the signal just to set the value
	slider_label.text = SliderLabelText
	slider.tick_count = TickCount

func _on_slider_value_changed(value: float) -> void:
	slider_changed.emit(value)

# function to update the amount displayed on the number tag
# THE SCENE THAT'S USING AN INSTANTIATED SLIDER NEED TO MANUALLY EDIT THE DISPLAY BY USING THIS
func change_number_display_tag(value: String) -> void:
	number_tag.text = value