using Godot;
using System.Linq;
using Waterways;

public partial class WaterPhysics : Node3D
{
	[Export] private Node3D ProbeContainer; // The parent node of all your Marker3D probes

	// Signal to send the calculated force and the relative position to apply it
	[Signal] public delegate void ApplyWaterForceEventHandler(Vector3 force, Vector3 relativePosition);

	private float _gravity;
	private Node3D _parentBody; // Changed to Node3D for modularity
	private RiverFloatSystem _river;
	private float _floatForce = 1.0f;
	private float _riverSpeed = 1.0f;
	private float _waterDrag = 2.0f;

	public void SetParameters(RiverFloatSystem river, float floatForce = 1.0f, float riverSpeed = 1.0f, float waterDrag = 2.0f)
	{
		_river = river;
		_floatForce = floatForce;
		_riverSpeed = riverSpeed;
		_waterDrag = waterDrag;
	}

	public override void _Ready()
	{
		_gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

		// Grab the parent as a generic Node3D
		_parentBody = GetParent<Node3D>();

		// Warn the developer if the parent isn't a physics body that we know how to read velocity from
		if (!(_parentBody is RigidBody3D || _parentBody is CharacterBody3D))
		{
			GD.PushWarning("WaterPhysics parent is neither a RigidBody3D nor a CharacterBody3D. Water drag calculations will default to zero velocity.");
		}
		
		if (ProbeContainer == null)
		{
			GD.PushWarning("ProbeContainer is not assigned in WaterPhysics.");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_parentBody == null || _river == null || ProbeContainer == null) return;

		ProbeBuoyancyPhysicsProcess();
	}

	private void ProbeBuoyancyPhysicsProcess()
  {
    // Fetch the velocities once per frame rather than inside the loop
    Vector3 parentLinearVelocity = GetParentLinearVelocity();
    Vector3 parentAngularVelocity = GetParentAngularVelocity();

    foreach (Marker3D probe in ProbeContainer.GetChildren().OfType<Marker3D>())
    {
      Vector3 globalPos = probe.GlobalPosition;
      Vector3 relativePos = globalPos - _parentBody.GlobalPosition; 
      Vector3 flowDirection = _river.GetWaterFlowDirection(globalPos);

      // --- THE FIX: FLATTEN THE CURRENT PUSH ---
      // We strip out the Y axis and normalize it so the current ONLY pushes horizontally
      Vector3 flatFlowDirection = new Vector3(flowDirection.X, 0, flowDirection.Z);
      
      // Prevent division by zero if the flow was perfectly vertical
      if (flatFlowDirection.LengthSquared() > 0.001f) {
				flatFlowDirection = flatFlowDirection.Normalized();
      }

      float waterHeight = _river.GetWaterHeight(globalPos);
      float depth = waterHeight - globalPos.Y;
      float buoyancyMultiplier = 2 - Mathf.Exp(-depth + 0.6f);
      
      // Note: Replaced WaterNormal() with Vector3.Up. 
      // Real buoyancy always fights gravity straight up! Using the surface normal 
      // on a steep waterfall pushes the player horizontally out of the water.
      Vector3 buoyancyForce = Vector3.Up * _gravity * _floatForce * buoyancyMultiplier;

      // Calculate current velocity at the exact probe position using our safely extracted velocities
      Vector3 currentVelocity = parentLinearVelocity + parentAngularVelocity.Cross(relativePos);
      Vector3 frictionForce = -currentVelocity * depth * _waterDrag;

      if (depth > 0)
      {
        // Apply the flatFlowDirection here so the river speed never touches the Y axis
        Vector3 finalForce = buoyancyForce + frictionForce + (flatFlowDirection * _riverSpeed);
        
        EmitSignal(SignalName.ApplyWaterForce, finalForce, relativePos);
      }
    }
  }

	// --- Helper Methods for Safe Velocity Extraction ---

	private Vector3 GetParentLinearVelocity()
	{
		if (_parentBody is RigidBody3D rb) return rb.LinearVelocity;
		if (_parentBody is CharacterBody3D cb) return cb.Velocity;
		
		return Vector3.Zero; // Fallback if it's just a generic Node3D
	}

	private Vector3 GetParentAngularVelocity()
	{
		if (_parentBody is RigidBody3D rb) return rb.AngularVelocity;
		
		// CharacterBody3D does not have a native angular velocity property.
		// We return Zero here. If you manually track character rotation speed later, you can plug it in here.
		return Vector3.Zero; 
	}

	private Vector3 WaterNormal(Vector3 globalPos)
	{
		Vector3 flowDirection = _river.GetWaterFlowDirection(globalPos);
		Vector3 waterRight = flowDirection.Cross(Vector3.Up);
		return waterRight.Cross(flowDirection);
	}
}