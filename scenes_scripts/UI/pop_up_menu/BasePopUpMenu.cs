using Godot;
using System;

public partial class BasePopUpMenu : Control
{
	// signal for telling the main menu to close out of the pop up menu
	[Signal] public delegate void GoBackButtonPressedEventHandler();

	// triggered when the back button is pressed 
	public void OnBackButtonPressed()
	{
		// emit the signal so the parent knows that the menu should be removed
		EmitSignal(nameof(GoBackButtonPressed));
	}
}
