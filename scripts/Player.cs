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
	[Export]
	public float LerpSpeed = 10.0f;

	// private variables
	private float _currSpeed = 5.0f;

	private float _gravity = 9.8f;

	private Vector3 _direction = Vector3.Zero;

	private Node3D _head;

	private float _crouchingDepth = -0.5f; // this is relative to the regular head 

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured; // capture the users mouse
		_head = GetNode<Node3D>("Head"); // get the head node
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
			Vector3 newRotation = _head.Rotation;
			newRotation.X += xRotationChange;
			newRotation.X = Mathf.Clamp(newRotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));

			// set the head rotation
			_head.Rotation = newRotation;
		}
  }

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// they can only be crouching or sprinting not both hence the else if
		// if they are pressing both then the speed will be set to crouching speed just bc it's at the beginning of the if else statements
		if (Input.IsActionPressed("crouch"))
		{
			_currSpeed = CrouchingSpeed;

			// set the head height to be offset by the crouching depth
			_head.Position = new Vector3(_head.Position.X, _crouchingDepth, _head.Position.Z);
		} 
		else
		{
			// set head position to be default in all other scenarios other than crouching
			_head.Position = _head.Position = new Vector3(_head.Position.X, 0.0f, _head.Position.Z);

			if (Input.IsActionPressed("sprint"))
			{
				_currSpeed = SprintSpeed;
			}
			else
			{
				_currSpeed = WalkingSpeed;
		}
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
		Vector3 targetDirection = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		// move towards zero vector if we're not trying to move anywhere
		if (inputDir == Vector2.Zero)
		{
			targetDirection = Vector3.Zero;
		}

		// set the direction and move in that direction
		_direction = _direction.MoveToward(targetDirection, (float)delta * LerpSpeed);
		velocity.X = _direction.X * _currSpeed;
		velocity.Z = _direction.Z * _currSpeed;
		Velocity = velocity;
		MoveAndSlide();
	}
}
