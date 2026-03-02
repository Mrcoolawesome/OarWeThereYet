using Godot;
using System;

public partial class InventoryUi : CanvasLayer
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Visible = false;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Open(Inventory inventory)
	{
		Visible = true;

		foreach (InvSlot slot in inventory.Slots)
		{
			
		}
	}

	public void Close()
	{
		Visible = false;
	}

	public bool isOpen()
	{
		return Visible;
	}
}
