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

		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	private void OnResumeButtonPressed()
  {
    EmitSignal(SignalName.Resume);
  }

  private void OnExitButtonPressed()
  {
    // Whether we are the Host or the Client, the process is now exactly the same!
    // 1. Tell the local game manager to go to the main menu
    GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));

    // 2. Nuke the network connection
    // (If we are the Host, this triggers "OnServerDisconnected" on all connected clients automatically!)
    if (Multiplayer.MultiplayerPeer != null)
    {
      Multiplayer.MultiplayerPeer.Close();
      Multiplayer.MultiplayerPeer = null;
    }
  }

  // This fires automatically on the CLIENT if the HOST closes their game or loses internet
  private void OnServerDisconnected()
  {
    // Force the client back to the main menu
    GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));

    // Clean up their local peer so they don't become a ghost
    if (Multiplayer.MultiplayerPeer != null)
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
