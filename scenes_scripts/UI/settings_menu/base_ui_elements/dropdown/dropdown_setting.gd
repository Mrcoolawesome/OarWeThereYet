@tool
extends Control

signal item_selected(item: int)

@onready var option_button: OptionButton = $MarginContainer/HBoxContainer/OptionButton
@onready var label: Label = $MarginContainer/HBoxContainer/Label

# text to be put on the label
@export var LabelText: String = "Placeholder":
	set(value):
		LabelText = value
		if is_node_ready():
			label.text = LabelText

# exported array to allow for adding Strings
@export var DropdownItems: Array[String] = []:
	set(value):
		DropdownItems = value
		if is_node_ready():
			_put_items_into_dropdown()
			
# for selecting the default item
@export var DefaultItem: int = 0:
	set(value):
		DefaultItem = value
		if is_node_ready():
			option_button.selected = DefaultItem

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# put all the items into the dropdown menu upon loading in
	_put_items_into_dropdown()
	
	# set the text
	label.text = LabelText

	# set the default item via its id
	option_button.selected = DefaultItem

func _put_items_into_dropdown() -> void:
	# put all the items into the dropdown menu
	for item in DropdownItems:
		option_button.add_item(item)

func _on_option_button_item_selected(index: int) -> void:
	item_selected.emit(index)
