using Godot;
using System;
using System.ComponentModel;

public partial class InventoryUi : CanvasLayer
{
	[Export] public PackedScene InvSlotUI;
	public GridContainer GridContainer;
	public ArmNode ArmNode;
	public Inventory Inventory;
	public InvSlotUi PlayerSlot;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Visible = false;
		GridContainer = GetNode<GridContainer>("GridContainer");
		InvSlotUI = GD.Load<PackedScene>("res://scenes_scripts/UI/inventory_UI/InvSlotUI.tscn");
		PlayerSlot = GetNode<InvSlotUi>("PlayerSlot");
		PlayerSlot.IsPlayerSlot = true;

		Player player = GetParent<Player>();
		ArmNode = player.GetNode<ArmNode>("Head/ArmNode");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Open(Inventory inventory)
	{
		Visible = true;
		Inventory = inventory;
		Inventory.InventoryUpdated += Refresh;

		int counter = 0;
		foreach (InvSlot slot in inventory.Slots)
		{
			InvSlotUi slotInstance = InvSlotUI.Instantiate<InvSlotUi>();

			slotInstance.SlotNum = counter;
			slotInstance.Item = slot;

			GridContainer.AddChild(slotInstance);

			counter++;
		}

		// Update player slot
		PlayerSlot.Item = ArmNode.Item;
	}

	public void Close()
	{
		if (Inventory != null)
			Inventory.InventoryUpdated -= Refresh;
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

	public void Refresh()
	{
		int counter = 0;
		foreach (InvSlotUi slot in GridContainer.GetChildren())
		{
			slot.Item = Inventory.Slots[counter];

			counter++;
		}

		PlayerSlot.Item = ArmNode.Item;
	}
}
