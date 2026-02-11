using Godot;
using System;

public partial class Seat : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; } = "Sit";
	[Export] public int seatIndex { get; set; }
	public string PromptInput { get; set; } = "action_key";

	public void Interact(Player player)
	{
			player.HandleInSeatHitboxState(seatIndex);
	}
}
