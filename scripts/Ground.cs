using Godot;
using System;
using System.ComponentModel;

public partial class Ground : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; }
	public string PromptInput { get; set; } = "interact";

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Interact()
	{
		Visible = !Visible;
	}
}
