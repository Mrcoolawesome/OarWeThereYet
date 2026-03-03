using Godot;
using System;
using System.ComponentModel;

public partial class InventoryUi : CanvasLayer
{
	[Export] public PackedScene InvSlotUI;
	public GridContainer GridContainer;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Visible = false;
		GridContainer = GetNode<GridContainer>("GridContainer");
		InvSlotUI = GD.Load<PackedScene>("res://scenes_scripts/UI/inventory_UI/InvSlotUI.tscn");
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
			Panel slotInstance = InvSlotUI.Instantiate<Panel>();
			GridContainer.AddChild(slotInstance);
		}
	}

	public void Close()
	{
		Visible = false;
		var children = GridContainer.GetChildren();

		foreach (Node child in children)
		{
			child.QueueFree();
		}

		GridContainer.Size = Vector2.Zero;
	}

	public bool isOpen()
	{
		return Visible;
	}
}
