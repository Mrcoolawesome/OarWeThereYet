using Godot;
using System;

public partial class Hole : StaticBody3D
{
	private bool _hasPendingPatchIntent = false;

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPatchConfirmation()
	{
		if (!Multiplayer.IsServer()) return;
		Rpc(nameof(ConfirmPatchRemoval));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ConfirmPatchRemoval()
	{
		if (_hasPendingPatchIntent)
		{
			_hasPendingPatchIntent = false;
		}


		if (Multiplayer.IsServer())
		{
			QueueFree();
		}
	}
}
