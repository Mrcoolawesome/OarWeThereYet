using Godot;
using System;

public partial class Hole : StaticBody3D
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void RequestPatch()
	{
		if (Multiplayer.IsServer())
		{
			QueueFree();
		}
	}
}
