using Godot;
using System;

public partial class InvSlotUi : Panel
{
	public InvSlot Item = null;
	public Sprite2D ItemDisplay;
	public int SlotNum;
	public InventoryUi InventoryUi;
	public TextEdit CountText;
	
	public override void _Ready()
	{
		ItemDisplay = GetNode<Sprite2D>("CenterContainer/Panel/ItemDisplay");
		InventoryUi = GetNode<InventoryUi>("../..");
		CountText = GetNode<TextEdit>("TextEdit");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Item != null && Item.Data != null)
		{
			ItemDisplay.Visible = true;
			CountText.Visible = true;
			ItemDisplay.Texture = (Texture2D)Item.Data.Icon;
			CountText.Text = Item.Amount.ToString();
		}
		else
		{
			ItemDisplay.Visible = false;
			CountText.Visible = false;
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
	}
}
