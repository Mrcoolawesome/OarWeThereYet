using Godot;
using System;
using System.Net.Http;

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

	private Node3D head;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured; // capture the users mouse
		head = GetNode<Node3D>("Head"); // get the head node
	}

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseMotion mouseEvent)
		{
			// the y rotation of the player in radians based off of the mouse sensitivity 
			float yRotationChange = -Mathf.DegToRad(mouseEvent.Relative.X * MouseSens);
			RotateY(yRotationChange);

			// the head rotation
			float xRotationChange = -Mathf.DegToRad(mouseEvent.Relative.Y * MouseSens);

			// add the rotation change per tick and then clamp the rotation
			Vector3 newRotation = head.Rotation;
			newRotation.X += xRotationChange;
			newRotation.X = Mathf.Clamp(newRotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));

			// set the head rotation
			head.Rotation = newRotation;
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
