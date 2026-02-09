using Godot;
using System;

public partial class InteractRay : RayCast3D
{
	private Label _prompt;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_prompt = GetNode<Label>("Prompt");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (IsColliding())
		{
			GD.Print("Colliding with something");
		}
	}
}
