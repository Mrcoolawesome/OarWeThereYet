using Godot;
using System;

public partial class TestLevel : Node
{

	// boat object 
	private Boat _boat = new Boat();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// attach the reset function to the signal from the signal server script
		GlobalSignalServer.Instance.ResetLevel += _InitateReset; // might be a problem to directly call an Rpc function

		// set the boat variable
		_boat = GetNode<Boat>("Boat");
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _InitateReset()
	{
		_ResetSignalRecieved();
	}

	// i think we want CallLocal to be false because we'll just update the server and it'll sync the locations of everything to their starting points
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void _ResetSignalRecieved()
	{
		RpcId(1, MethodName._Reset);
	}

	// still only want the server to execute this stuff, so even though CallLocal is set to true this
	// method should ONLY EVER BE ACCESSED BY THE SERVER - hence you must always use RpcId with an id of 1 
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void _Reset()
	{
		// extra check to make sure only the server can do this
		if (!Multiplayer.IsServer()) return;

		_boat.Reset();

		// reset the players by calling the 'ResetToStart' function on all of them
		GetTree().CallGroup("players", "Reset");
	}
}
