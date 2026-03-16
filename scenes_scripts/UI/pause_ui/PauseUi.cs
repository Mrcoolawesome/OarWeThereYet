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
		// tell the signal server to tell the game manager to kill the level put the main menu back
		if (Multiplayer.IsServer())
		{
			// if they're the server then tell everyone to go to the main menu
			Rpc(nameof(BroadcastCloseGame));
		} 
		else
		{
			// if they're not the server then just emit the goto menu signal locally
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));
		}
	}

	// this runs on everyone's machine so everybody goes to the main menu, only the server should be able to call this
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void BroadcastCloseGame()
	{
		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));
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
