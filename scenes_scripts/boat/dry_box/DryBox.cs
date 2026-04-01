using Godot;
using System;

public partial class DryBox : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; } = "Open Drybox";
	public string PromptInput { get; set; } = "action_key";
	public Inventory Inventory;

	public override void _Ready()
	{
	  Inventory = GetNode<Inventory>("Inventory");
	}

	public void Interact(Player player)
	{
		player.OpenInventory(Inventory);
	}
}
