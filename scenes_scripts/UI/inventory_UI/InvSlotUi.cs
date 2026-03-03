using Godot;
using System;

public partial class InvSlotUi : Panel
{
	public InvSlot Item = null;
	public Sprite2D ItemDisplay;
	public int SlotNum;
	public InventoryUi InventoryUi;
	
	public override void _Ready()
	{
		ItemDisplay = GetNode<Sprite2D>("CenterContainer/Panel/ItemDisplay");
		InventoryUi = GetNode<InventoryUi>("../..");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Item != null && Item.Data != null)
		{
			ItemDisplay.Visible = true;
			ItemDisplay.Texture = (Texture2D)Item.Data.Icon;
		}
		else
		{
			ItemDisplay.Visible = false;
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton
			&& mouseButton.ButtonIndex == MouseButton.Left
			&& mouseButton.Pressed)
		{
			OnSlotClicked();
			AcceptEvent();
		}
	}

	private void OnSlotClicked()
	{
		InventoryUi.Inventory.RequestSwapItem(SlotNum);
		InventoryUi.Refresh();
	}
}
