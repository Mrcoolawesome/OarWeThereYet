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
		InventoryUi = FindAncestor<InventoryUi>();
		CountText = GetNode<TextEdit>("TextEdit");
	}

	private T FindAncestor<T>() where T : Node
	{
		Node current = GetParent();
		while (current != null)
		{
			if (current is T match)
				return match;
			current = current.GetParent();
		}
		return null;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (ItemDisplay == null || CountText == null)
			return;

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
