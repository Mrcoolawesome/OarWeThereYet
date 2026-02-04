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
	[Export]
	public float CrouchLerpSpeed = 10.0f;

	// private variables
	private float _currSpeed = 5.0f;

	private float _gravity = 9.8f;

	private Vector3 _direction = Vector3.Zero;

	private Node3D _head;

	private CollisionShape3D _crouchingCollision;

	private CollisionShape3D _standingCollision;

	private float _crouchingDepth = -0.5f; // this is relative to the regular head 

	// different states for being in the menu and just playing the game
	private enum GameState {
		Playing, 
		Menu
	}
	private GameState _currState = GameState.Menu; // default state is being in the menu

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head"); // get the head node
		_crouchingCollision = GetNode<CollisionShape3D>("CrouchingCollision");
		_standingCollision = GetNode<CollisionShape3D>("StandingCollision");
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

  public override void _Process(double delta)
  {
    // logic for escaping into the menu and then either quitting the game or going back into the game
		switch(_currState)
		{
			case GameState.Playing:
				_HandleGamingState();
				break;
			case GameState.Menu:
				_HandleMenuState();
				break;
		}
  }

	// handling ui state if they're just playing the game
	private void _HandleGamingState()
	{
		if (Input.IsActionJustPressed("ui_cancel")) // we use IsActionJustPressed because it's a trigger and not a continuous input event like IsActionPressed is
		{
			_currState = GameState.Menu;
			// release the mouse
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	// handling ui state if they're in the menu
	private void _HandleMenuState()
	{
		// if they're in the menu and press escape then close the game
		if (Input.IsActionJustPressed("ui_cancel"))
		{
			GetTree().Quit();
		}

		// if they press their mouse button then capture the mouse again
		if (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			_currState = GameState.Playing;
			Input.MouseMode = Input.MouseModeEnum.Captured; // capture the mouse again
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
			Vector3 targetHeadPosition = new Vector3(_head.Position.X, _crouchingDepth, _head.Position.Z);
			_head.Position = _head.Position.MoveToward(targetHeadPosition, (float)delta * CrouchLerpSpeed);

			// disable the staning collision shape
			_standingCollision.Disabled = true;
			_crouchingCollision.Disabled = false;
		} 
		else
		{
			// set head position to be default in all other scenarios other than crouching
			Vector3 targetHeadPosition = new Vector3(_head.Position.X, 0.0f, _head.Position.Z);
			_head.Position = _head.Position.MoveToward(targetHeadPosition, (float)delta * CrouchLerpSpeed);

			// enable the standing collision shape
			_standingCollision.Disabled = false;
			_crouchingCollision.Disabled = true;

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
