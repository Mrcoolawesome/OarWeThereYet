using Godot;
using System;

public partial class PopUpMenu : Control
{
	// signal for telling the main menu to close out of the pop up menu
	[Signal] public delegate void GoBackButtonPressedEventHandler();

	// we need to get the textbox container so we can hide and unhide it depending on if they made it public or not
	private MarginContainer _textBoxContainer = new MarginContainer(); 

	// variables for storing everything inputted into the pop up menu
	private string _lobbyName = "gaming";
	private bool _isPublic = false;

  public override void _Ready()
  {
    _textBoxContainer = GetNode<MarginContainer>("PanelContainer/VBoxContainer/LobbyNameContainer");
  }

	// this is connected to the checkbox to make it a public lobby or not
	public void OnCheckBoxToggled(bool toggledOn)
	{
		_isPublic = toggledOn;
		_textBoxContainer.Visible = toggledOn; // reveal the text box container
	}

	// this is connected to the textbox that sets the name of the lobby
	public void OnLineEditTextChanged(string newText)
	{
		_lobbyName = newText;
	}

	// triggered when the HOST button is pressed
	public void OnHostButtonPressed()
	{
		// emit via the global signal server
		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.HostGame));
	}

	// triggered when the back button is pressed 
	public void OnBackButtonPressed()
	{
		// emit the signal so the parent knows that the menu should be removed
		EmitSignal(nameof(GoBackButtonPressed));
	}
}
