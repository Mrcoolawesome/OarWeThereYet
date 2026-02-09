using Godot;
using System;

public partial class Seat : CollisionShape3D, Interactable
{
	[Export] public string PromptMessage { get; set; } = "Sit";
	public string PromptInput { get; set; } = "action_key";

	public void Interact(Player player)
	{
       GD.Print("Interacted"); 
	}
}
