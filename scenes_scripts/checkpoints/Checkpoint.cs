using Godot;
using System;
using System.ComponentModel;

public partial class Checkpoint : Area3D
{
	[Export] public int CheckpointNum;

	public void OnBodyEntered(Node3D body)
	{
		if (!Multiplayer.IsServer()) return;
		
		if (body.Name == "Boat")
		{
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.SaveGame), CheckpointNum);
		}
	}
}
