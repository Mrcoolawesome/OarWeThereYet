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

	private async void OnExitButtonPressed()
	{
		// tell the signal server to tell the game manager to kill the level put the main menu back
		if (Multiplayer.IsServer())
		{
			// if they're the server then tell everyone to go to the main menu, which will make us go to the main menu too
			Rpc(nameof(BroadcastCloseGame));

			// destroy this server peer
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null; // yes we actually have to manually do this
		} 
		else
		{
			// if they're not the server then just emit the goto menu signal locally
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));

			// remove their multiplayer peer, which will trigger the _remove_player function in the network scripts
      Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	// this runs on everyone's machine so everybody goes to the main menu, only the server should be able to call this
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void BroadcastCloseGame()
	{
		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));

		// NEW: If we are a client and the server just told us to close, 
    // we MUST destroy our local peer so we don't become a ghost!
    if (!Multiplayer.IsServer())
    {
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
    }
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
