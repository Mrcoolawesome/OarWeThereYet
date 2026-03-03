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
			_change_number_display_tag(value)

@export var WholeNumbers: bool = true:
	set(value):
		WholeNumbers = value
		if is_node_ready():
			# Update the text formatting immediately if toggled
			_change_number_display_tag(StartingValue) 

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

@export var SliderLabelAppendedText: String = "":
	set(value):
		SliderLabelAppendedText = value
		if is_node_ready() and value != "":
			number_tag.text += value
		elif value == "":
			_change_number_display_tag(StartingValue) # make it use the starting value if the appended string is blank

@onready var slider: Slider = $MarginContainer/HBoxContainer/Slider
@onready var number_tag: Label = $MarginContainer/HBoxContainer/SliderNumberLabel
@onready var slider_label: Label = $MarginContainer/HBoxContainer/SliderNameLabel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# Apply initial values once the children are guaranteed to exist
	slider.min_value = MinValue
	slider.max_value = MaxValue
	slider.step = Step
	slider.value = StartingValue
	slider_label.text = SliderLabelText
	slider.tick_count = TickCount
	_change_number_display_tag(StartingValue)

func _on_slider_value_changed(value: float) -> void:
	slider_changed.emit(value)
	_change_number_display_tag(value) # update the display tag

# function to update the amount displayed on the number tag
func _change_number_display_tag(value: float) -> void:
	if number_tag: # Good practice to check if it exists first
		if WholeNumbers:
			number_tag.text = "%.0f" % value + SliderLabelAppendedText
		else:
			number_tag.text = "%.1f" % value + SliderLabelAppendedText