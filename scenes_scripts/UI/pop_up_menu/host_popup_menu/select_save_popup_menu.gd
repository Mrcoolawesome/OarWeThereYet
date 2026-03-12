extends BasePopUpMenu

@onready var _button_1: Button = $PanelContainer/VBoxContainer/SaveContainer1/Button
@onready var _button_2: Button = $PanelContainer/VBoxContainer/SaveContainer2/Button
@onready var _button_3: Button = $PanelContainer/VBoxContainer/SaveContainer3/Button

const SAVE_DIR := "user://saves/"

signal selected_save
signal deleted_save

func _ready() -> void:
	# Load saves 1-3 and display buttons with save info
	_refresh_save_buttons()


func _refresh_save_buttons() -> void:
	_update_save_button(_button_1, 1)
	_update_save_button(_button_2, 2)
	_update_save_button(_button_3, 3)


func _update_save_button(button: Button, slot: int) -> void:
	var save_path := "%ssave_%d.tres" % [SAVE_DIR, slot]

	if not FileAccess.file_exists(save_path):
		button.text = "Save %d: New Game" % slot
		return

	var save_res := ResourceLoader.load(save_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	if save_res == null:
		button.text = "Save %d: Corrupt" % slot
		return

	# C# exported properties can be read through get() from GDScript.
	var checkpoint_num: int = int(save_res.get("CheckpointNum"))
	button.text = "Save %d: Checkpoint %d" % [slot, checkpoint_num]


func on_save_1_button_pressed():
	GlobalVariables.save_slot = 1
	selected_save.emit()

func on_save_2_button_pressed():
	GlobalVariables.save_slot = 2
	selected_save.emit()

func on_save_3_button_pressed():
	GlobalVariables.save_slot = 3
	selected_save.emit()

func _on_host_game_pop_up_menu_delete_save() -> void:
	var save_path := "%ssave_%d.tres" % [SAVE_DIR, GlobalVariables.save_slot]

	if FileAccess.file_exists(save_path):
		DirAccess.remove_absolute(save_path)
		print("deleted save")
	_refresh_save_buttons()

	deleted_save.emit()

