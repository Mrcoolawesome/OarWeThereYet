using Godot;
using System;

public partial class Cube : RigidBody3D
{

	// Floating forces 
	[Export]
	public float FloatForce { get; set; } = 1.0f;
	[Export]
	public float WaterDrag { get; set; } = 0.05f;
	[Export]
	public float WaterAngularDrag { get; set; } = 0.05f;
	[Export]
	public WaterPlane WaterPlane;

	// private variables
	private float _gravity; // get the gravity of the project
	private float _waterHeight = 0.0f; // height of the water
	private bool _submerged = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// this might break if it can't actually type cast to a float
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

  public override void _PhysicsProcess(double delta)
  {
		// not submerged by default
		_submerged = false;

		// get the depth based on the global posiiton
    float depth = WaterPlane.GetHeight(GlobalPosition) - GlobalPosition.Y;

		// check if they're under the water
		if (depth > 0.0)
		{
			_submerged = true;
			ApplyCentralForce(Vector3.Up * FloatForce * _gravity * depth);
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
