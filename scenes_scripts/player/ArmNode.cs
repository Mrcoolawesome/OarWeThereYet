using Godot;
using System;

public partial class ArmNode : MeshInstance3D
{
	public InvSlot Item { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Mesh = null;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Item != null)
		{
			Mesh = Item.Data.ItemMesh;
		}
		else
		{
			Mesh = null;
		}

		if (Input.IsActionPressed("right_click") && GetParent().GetParent<Node>().IsMultiplayerAuthority())
		{
			RequestDropItem();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetItem(string itemPath, int itemCount)
	{
		if (string.IsNullOrEmpty(itemPath))
		{
			Item = null;
		}
		else
		{
			Item = new InvSlot(GD.Load<InvItem>(itemPath), itemCount);
		}
	}

	private void RequestDropItem()
	{
		RpcId(1, MethodName.DropItem);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DropItem()
	{
		if (Item != null)
		{
			string itemPath = Item.Data.ResourcePath;
			int itemCount = Item.Amount;
			Vector3 dropPosition = GlobalPosition;

			// Tell all peers to spawn the item and clear the arm
			Rpc(nameof(SpawnDroppedItem), itemPath, itemCount, dropPosition);
			Rpc(nameof(SetItem), "", 0);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SpawnDroppedItem(string itemPath, int itemCount, Vector3 position)
	{
		PackedScene inWorldScene = GD.Load<PackedScene>("res://scenes_scripts/inventory/items/itemScenes/UniversalInWorld.tscn");
		UniversalInWorld inWorldNode = inWorldScene.Instantiate<UniversalInWorld>();

		inWorldNode.ItemObject = GD.Load<InvItem>(itemPath);
		inWorldNode.ItemCount = itemCount;
		inWorldNode.Position = position;

		GetNode("/root/GameManager/Level/DemoLevel").AddChild(inWorldNode);
	}
}
