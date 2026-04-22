using Godot;
using System;

public partial class Hole : StaticBody3D
{
	private bool _isPatched = false;

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RequestPatch(string armPath)
	{
		if (Multiplayer.IsServer())
		{
			if (_isPatched) return;
			_isPatched = true;

			ArmNode arm = GetNodeOrNull<ArmNode>(armPath);
			if (arm != null && arm.Item != null && arm.Item.Data != null)
			{
				int newAmount = arm.Item.Amount - 1;
				if (newAmount <= 0)
				{
					arm.Rpc(nameof(ArmNode.SetItem), "", 0);
				}
				else
				{
					arm.Rpc(nameof(ArmNode.SetItem), arm.Item.Data.ResourcePath, newAmount);
				}
			}
			
			QueueFree();
		}
	}
}