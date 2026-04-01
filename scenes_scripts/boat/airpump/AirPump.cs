using Godot;
using System;

public partial class AirPump : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; } = "Pump Air";
	public string PromptInput { get; set; } = "action_key";

	public void Interact(Player player)
	{
    GD.Print("Pumping");
	}
}
