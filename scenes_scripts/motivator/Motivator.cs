using Godot;
using System;

public partial class Motivator : Node3D
{
	[Export] public float Speed = 1.0f;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GlobalSignalServer.Instance.StartMotivator += //Start logic function
		GlobalSignalServer.Instance.StartMotivator += //Stop logic function
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Reset level if collide with something on boat layer (layer 2)
		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.ResetLevel));
	}
}
