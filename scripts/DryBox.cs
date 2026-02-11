using Godot;
using System;

public partial class DryBox : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Open Drybox";
	public string PromptInput { get; set; } = "action_key";

	public void Interact(Player _player)
	{
		GD.Print("Opened/Closed Drybox");
	}
}
