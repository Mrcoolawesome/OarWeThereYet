class_name BasePopUpMenu
extends Control

# Signal for telling the main menu to close out of the pop up menu
signal go_back_button_pressed

# Triggered when the back button is pressed 
func on_back_button_pressed():
	# Emit the signal so the parent knows that the menu should be removed
	go_back_button_pressed.emit()