using Godot;
using System;

public partial class InvSlot
{
	public InvItem Data { get; set; }
	public int Amount { get; set; }

	public InvSlot(InvItem item, int amount)
	{
		Data = item;
		Amount = amount;
	}

	public bool IsEmpty()
	{
		return Data == null || Amount <= 0;
	}
}
