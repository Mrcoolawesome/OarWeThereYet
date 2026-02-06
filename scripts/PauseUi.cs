using Godot;
using System;

public partial class PauseUi : Control
{
	[Signal] public delegate void ResumeEventHandler();
	[Signal] public delegate void ExitEventHandler();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnResumeButtonPressed()
	{
		EmitSignal(SignalName.Resume);
	}

	private void OnExitButtonPressed()
	{
		EmitSignal(SignalName.Exit);
	}
}
