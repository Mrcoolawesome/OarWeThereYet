using Godot;
using System;

public partial class PauseUi : Control
{
	[Signal] public delegate void ResumeEventHandler();
	[Signal] public delegate void ExitEventHandler();
	
	// get the settings menu
	private Control _settingsMenu = new Control();
	private MarginContainer _mainContainer = new MarginContainer();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// get the stuff from the tree
		_settingsMenu = GetNode<Control>("PanelContainer/SettingsMenu");
		_mainContainer = GetNode<MarginContainer>("PanelContainer/PauseButtonMainContainer");
	}

	private void OnResumeButtonPressed()
	{
		EmitSignal(SignalName.Resume);
	}

	private void OnExitButtonPressed()
	{
		EmitSignal(SignalName.Exit);
	}

	private void OnSettingsButtonPressed()
	{
		_settingsMenu.Visible = true;
		_mainContainer.Visible = false;
	}

	private void OnSettingsBackButtonPressed()
	{
		_settingsMenu.Visible = false;
		_mainContainer.Visible = true;
	}
}
