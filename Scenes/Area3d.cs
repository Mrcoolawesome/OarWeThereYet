using Godot;
using System;

public partial class Area3d : Area3D
{

	public void OnArea3DBodyEntered(Node3D body)
	{
		GD.Print($"gamin? {body.Name}");
	} 
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// subscribe to the body entered signal. the first thing is the name of the overall signal and the function is the thing that we're using that gets called
		BodyEntered += OnArea3DBodyEntered;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
