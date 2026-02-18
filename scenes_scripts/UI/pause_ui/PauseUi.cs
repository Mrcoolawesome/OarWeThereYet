using Godot;
using System;

public partial class PauseUi : Control
{
	[Signal] public delegate void ResumeEventHandler();
	[Signal] public delegate void ExitEventHandler();
	[Signal] public delegate void JoinEventHandler();
	[Signal] public delegate void HostEventHandler();
	[Signal] public delegate void RespawnPlayerEventHandler();
	
	// get the button
	private Button _resetButton = new Button();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// get the button from the tree
		_resetButton = GetNode<Button>("ResetGameButton");

		// if the user isn't the server they shouldn't be able to reset the game
		_resetButton.Visible = Multiplayer.IsServer();
	}

	private void OnResumeButtonPressed()
	{
		EmitSignal(SignalName.Resume);
	}

	private void OnExitButtonPressed()
	{
		EmitSignal(SignalName.Exit);
	}

	private void OnJoinButtonPressed()
	{
		EmitSignal(SignalName.Join);
	}

	private void OnHostButtonPressed()
	{
		EmitSignal(SignalName.Host);
	}

	private void OnResetButtonPressed()
	{
		// call the signal on the signal server don't locally emit a signal
		GlobalSignalServer.Instance.EmitSignal(GlobalSignalServer.SignalName.ResetLevel);
	}

	private void OnRespawnPlayerButtonPressed()
	{
		EmitSignal(SignalName.RespawnPlayer);
	}
}
