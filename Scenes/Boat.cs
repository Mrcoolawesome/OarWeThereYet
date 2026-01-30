using Godot;
using System;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{

	// Floating forces 
	[Export]
	public float FloatForce { get; set; } = 1.0f;
	[Export]
	public float WaterDrag { get; set; } = 0.05f;
	[Export]
	public float WaterAngularDrag { get; set; } = 0.05f;
	[Export]
	public RiverFloatSystem River { get; set; }

	// speed multiplier for river angle
	[Export]
	public float Marketplier {get; set;} = 10.0f;

	// private variables
	private float _gravity; // get the gravity of the project
	private float _waterHeight = 0.0f; // height of the water
	private bool _submerged = false;
	private Marker3D[] probes = new Marker3D[6];

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// this might break if it can't actually type cast to a float
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

		// getting the probes
		var probeContainer = GetNode<Node3D>("ProbeContainer");
		int count = 0;
		foreach(Marker3D child in probeContainer.GetChildren().Cast<Marker3D>())
		{
			probes[count] = child;
			count++;
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

  public override void _PhysicsProcess(double delta)
  {
		// not submerged by default
		_submerged = false;

		foreach(Marker3D probe in probes)
		{
			// get the current nodes velocity
			// surface water height
			float surfaceWaterY = River.GetWaterHeight(probe.GlobalPosition);
			// get the depth based on the global posiiton
			float depth = surfaceWaterY - probe.GlobalPosition.Y;

			// Get the direction vector
			Vector3 flowDirection = River.GetWaterFlowDirection(probe.GlobalPosition);

			// Angle from the y vector
			var angleToY = Mathf.RadToDeg(flowDirection.AngleTo(Vector3.Up));
			GD.Print($"{Mathf.RadToDeg(angleToY)}");
			// make the angle 0 by default - above 0 is going down below is going up
			float zeroedAngle = ((angleToY - 90) / Marketplier) + 1;

			// check if they're under the water
			if (depth > 0.0)
			{
				_submerged = true;
				Vector3 bouyancy = new Vector3(0, FloatForce * _gravity * depth, 0);
				Vector3 riverVelocity = flowDirection* zeroedAngle;
				ApplyForce(bouyancy + riverVelocity, probe.GlobalPosition - GlobalPosition);
			}
		}
  }

	// this is for accessing the specific physics state of rigid bodies
	// Specifically in the tutorial he says that physics may run in another thread at different granularity (i think this means tick speeds?) 
	// So we wanna use IntegrateForces for precise control of the bodies state, and thus we should set our velocities using it
  public override void _IntegrateForces(PhysicsDirectBodyState3D state)
  {
    if (_submerged) {
			state.LinearVelocity *= 1 - WaterDrag;
			state.AngularVelocity *= 1 - WaterAngularDrag;
		}
  }
}
