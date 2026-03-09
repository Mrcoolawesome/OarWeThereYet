using Godot;
using System;
using System.ComponentModel;

public partial class Checkpoint : Area3D
{
	[Export] public int CheckpointNum;

	public void OnBodyEntered(Node3D body)
	{
		if (body.Name == "Boat")
		{
			GD.Print("Boat entered checkpoint: " + CheckpointNum);
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.SaveGame), CheckpointNum);
		}
	}
}
