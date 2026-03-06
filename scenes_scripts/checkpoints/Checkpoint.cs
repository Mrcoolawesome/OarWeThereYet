using Godot;
using System;
using System.ComponentModel;

public partial class Checkpoint : Area3D
{
	[Export] public int CheckpointNum;

	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
	}
}
