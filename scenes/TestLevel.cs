using Godot;
using System;

public partial class TestLevel : Node
{

	// boat reset position and rotation
	private Vector3 _boatResetPosition = new Vector3(0.0f, 0.0f, -8.0f);
	private Vector3 _boatResetRotation = new Vector3(0.0f, Mathf.DegToRad(90), 0.0f);

	// boat object 
	private RigidBody3D _boat = new RigidBody3D();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// attach the reset function to the signal from the signal server script
		GlobalSignalServer.Instance.ResetLevel += _InitateReset; // might be a problem to directly call an Rpc function

		// set the boat variable
		_boat = GetNode<RigidBody3D>("Boat");
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

		// actual reset logic 
		_boat.GlobalPosition = _boatResetPosition;

		_boat.Rotation = _boatResetRotation;

		// reset the boat velocity
		if (_boat is RigidBody3D rigidBoat)
		{
			// im pretty sure that rigidBoat is the same refrence as _boat
			rigidBoat.LinearVelocity = Vector3.Zero;
			rigidBoat.AngularVelocity = Vector3.Zero;
		}

		// reset the players by calling the 'ResetToStart' function on all of them
		GetTree().CallGroup("players", "Reset");
	}
}
