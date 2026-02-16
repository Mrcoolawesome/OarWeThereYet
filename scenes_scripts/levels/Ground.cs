using Godot;
using System;
using System.ComponentModel;

public partial class Ground : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; }
	public string PromptInput { get; set; } = "action_key";

	public void Interact(Player _player)
	{
		Visible = !Visible;
	}
}
