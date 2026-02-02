using Godot;
using System;

public partial class Player : CharacterBody3D
{
	// exported variables
	[Export]
	public float JumpVelocity = 4.5f;
	[Export]
	public float WalkingSpeed = 5.0f;
	[Export]
	public float SprintSpeed = 8.0f;
	[Export]
	public float CrouchingSpeed = 3.0f;
	[Export]
	public float MouseSens = 0.4f;

	// private variables
	private float _currSpeed = 5.0f;

	private float _gravity = 9.8f;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured; // capture the users mouse
	}

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseMotion mouseEvent)
		{
			// the rotation in radians based off of the mouse sensitivity 
			float rotation = -Mathf.DegToRad(mouseEvent.Relative.X * MouseSens);
			RotateY(rotation);
		}
  }

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		if (Input.IsActionPressed("sprint"))
		{
			_currSpeed = SprintSpeed;
		}
		else
		{
			_currSpeed = WalkingSpeed;
		}

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 inputDir = Input.GetVector("left", "right", "move_forward", "move_backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * _currSpeed;
			velocity.Z = direction.Z * _currSpeed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, _currSpeed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, _currSpeed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
