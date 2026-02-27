using Godot;
using System;

public partial class ArmNode : MeshInstance3D
{
	public InvItem Item { get; set; }

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
			Mesh = Item.ItemMesh;
		}
		else
		{
			Mesh = null;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetItem(string itemPath)
	{
		InvItem item = GD.Load<InvItem>(itemPath);
		Item = item;
	}

}
