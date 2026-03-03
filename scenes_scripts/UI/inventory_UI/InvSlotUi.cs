using Godot;
using System;

public partial class InvSlotUi : Panel
{
	public InvSlot Item = null;
	public Sprite2D ItemDisplay;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ItemDisplay = GetNode<Sprite2D>("CenterContainer/Panel/ItemDisplay");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Item != null)
		{
			ItemDisplay.Visible = true;
			ItemDisplay.Texture = (Texture2D)Item.Data.Icon;
		}
		else
		{
			ItemDisplay.Visible = false;
		}
	}
}
