@tool
extends Control

# emit this when the checkbox is clicked so parent scripts can listen to it
signal toggled(toggled_on: bool)

@onready var label: Label = $MarginContainer/HBoxContainer/Label
@onready var checkbox: CheckBox = $MarginContainer/HBoxContainer/CheckBox

# text to be put on the label
@export var LabelText: String = "Placeholder":
  set(value):
    LabelText = value
    if is_node_ready():
      label.text = LabelText

# for selecting the default state of the checkbox (checked or unchecked)
@export var DefaultState: bool = false:
  set(value):
    DefaultState = value
    if is_node_ready():
      checkbox.button_pressed = DefaultState

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
  # set the text
  label.text = LabelText

  # set the default state
  checkbox.button_pressed = DefaultState

# Make sure to connect the CheckBox node's built-in 'toggled' signal to this function!
func _on_check_box_toggled(toggled_on: bool) -> void:
  toggled.emit(toggled_on)