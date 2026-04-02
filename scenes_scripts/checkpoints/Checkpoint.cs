using Godot;
using System;
using System.ComponentModel;

public partial class Checkpoint : Area3D
{
	[Export] public int CheckpointNum;
	[Export] public bool UseAnchor = true;

	public void OnBodyEntered(Node3D body)
	{
		if (!Multiplayer.IsServer()) return;
		
		if (body.Name == "Boat")
		{
			GD.Print("Entered checkpoint ", CheckpointNum);
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.SaveGame), CheckpointNum);
		}
	}
}
