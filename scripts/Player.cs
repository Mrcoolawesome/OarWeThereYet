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
	[Export]
	public Boat Boat;

	// Signals for telling the boat to apply or stop applying a force given a seat
	// back is false forward is true
	[Signal]
	public delegate void RowingEventHandler(Boat.SeatIndicies seat, bool stopStart, bool backForward);

	// private variables
	private float _currSpeed = 5.0f;

	private float _gravity = 9.8f;

	private Vector3 _direction = Vector3.Zero;

	private Node3D _head;

	private CollisionShape3D _crouchingCollision;

	private CollisionShape3D _standingCollision;

	private float _crouchingDepth = -0.5f; // this is relative to the regular head 

	// to keep track of if they're choosing to sit or not
	private bool _inSeatHitbox = false;

	// need to know which seat they're sitting in
	private Boat.SeatIndicies _seat = Boat.SeatIndicies.FrontLeft;
	/*
		(all of this is assuming you're facing the front)
		front left: 4, 2, -2
		front right: 4, 2, 2
		back left: 0, 2, -2
		back right: 0, 2, 2

		front left localShapeIndex: 0
		front right localShapeIndex: 1
		back right localShapeIndex: 2
		back left localShapeIndex: 3
	*/

	// different playing states. i made this a state machine so that we could add swimming in the future
	private enum PlayerState
	{
		Rowing,
		Standing
	}

	// different states for being in the menu and just playing the game
	private enum GameState {
		Playing,
		Menu,
	}
	private GameState _currGameState = GameState.Menu; // default state is being in the menu
	private PlayerState _currPlayerState = PlayerState.Standing; // default is walking

	private CanvasLayer _pauseUI;

  public override void _EnterTree()
  {
    SetMultiplayerAuthority(int.Parse(Name.ToString()));
		// MULTIPLAYER SETUP
    if (IsMultiplayerAuthority())
    {
			// 1. If we ARE the player, enable our camera
			// (Assumes you have a Camera3D as a child of Head)
			var camera = _head.GetNodeOrNull<Camera3D>("Camera3D"); 
			if (camera != null)
			{
				camera.Current = true;
			}

			// 2. Ensure the UI is properly hidden/shown based on initial state
			_pauseUI.Visible = _currGameState == GameState.Menu;
			
			// 3. Capture mouse immediately if you want them to start playing, 
			// or leave it visible if starting in Menu.
			if (_currGameState == GameState.Menu)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
    }
    else
    {
			// 1. If we are NOT the player (e.g., this is a peer's avatar on the host's screen),
			// DELETE the UI so it doesn't clutter the screen.
			_pauseUI.QueueFree();
			
			// 2. Disable processing for non-authority instances to save performance
			// (Optional, but good practice if _Process only handles inputs)
			SetProcess(false);
			SetPhysicsProcess(true); // Keep physics for smooth interpolation if you add it later
    }
  }

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head"); // get the head node
		_crouchingCollision = GetNode<CollisionShape3D>("CrouchingCollision");
		_standingCollision = GetNode<CollisionShape3D>("StandingCollision");
		_pauseUI = GetNode<CanvasLayer>("PauseCanvas");
	}

	// mouse input logic 
  public override void _Input(InputEvent @event)
  {
		// this is always done so that they can move their head, might wanna change it so that their head is always level
    if ((@event is InputEventMouseMotion mouseEvent) && _currGameState == GameState.Playing)
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
		switch(_currGameState)
		{
			case GameState.Playing:
				_HandleGamingState();
				_pauseUI.Visible = false;
				break;
			case GameState.Menu:
				_HandleMenuState();
				_pauseUI.Visible = true;
				break;
		}
  }

	// handling ui state if they're just playing the game
	private void _HandleGamingState()
	{
		// menu logic
		if (Input.IsActionJustPressed("ui_cancel")) // we use IsActionJustPressed because it's a trigger and not a continuous input event like IsActionPressed is
		{
			_currGameState = GameState.Menu;
			// release the mouse
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		// handle input for sitting and standing states
		switch(_currPlayerState)
		{
			case PlayerState.Standing:
				// handle input for choosing to sit
				_HandleInSeatHitboxState();
				break;
			case PlayerState.Rowing:
				_HandleRowingState();
				break;
		}
	}

	// handling ui state if they're in the menu
	private void _HandleMenuState()
	{
		//Exit and resume logic has been moved to signal function at bottom
	}

	// logic for walking and everything depending on the player state
	public override void _PhysicsProcess(double delta)
	{

		// always apply gravity 
		_Gravity(delta);

		// disable movement if they're not in the walking state, and wait for input to allow them to 'escape'
		if (_currGameState == GameState.Playing && _currPlayerState == PlayerState.Standing)
		{
			// apply crouching and sprinting logic
			_CrouchSprintLogic(delta);
			// apply gravity and movement logic
			_MovementLogic(delta);
		}

		// only apply move and slide if they're not rowing
		if (_currPlayerState != PlayerState.Rowing)
		{
			MoveAndSlide();
		}
	}

	private void _MovementLogic(double delta)
	{
		Vector3 velocity = Velocity;

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
	}

	private void _Gravity(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		Velocity = velocity;
	}

	private void _CrouchSprintLogic(double delta)
	{
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
	}

	// input handling while they're in a rowing state
	private void _HandleRowingState()
	{	
		// if they press the spacebar then release them
		if (Input.IsActionJustPressed("ui_accept"))
		{
			_currPlayerState = PlayerState.Standing;

			// reset their position to the middle of the boat and then make them a sibling of the boat again\
			// Position is in PARENT space so setting it to 0,1,0 will set it to the middle of the boat 
			Position = new Vector3(0, 1.0f, 0);
			Node boatParent = Boat.GetParent();
			Reparent(boatParent, true); // keep the global transform too ig
			GlobalRotation = Vector3.Zero;

			// make them stop rowing if they were rowing
			EmitSignal(SignalName.Rowing, (int)_seat, false, false); // second boolean doesn't matter because im just making them stop rowing

			return; // STOP after this we don't wanna take anymore input as if we're sitting
		}

		// (Boat.SeatIndicies seat, bool stopStart, bool backForward)
		// if they input w, send out go forward signal
		if (Input.IsActionPressed("move_forward"))
		{
			// tell them to move forward
			EmitSignal(SignalName.Rowing, (int)_seat, true, true); // have to send it as an int because that's a supported variant type: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_variant.html#c-sharp-variant-compatible-types
		} 
		else if (Input.IsActionPressed("move_backward")) // don't want them to be able to do both at the same time so if they're pressing both they'll go forward
		{
			// tell them to move backward
			EmitSignal(SignalName.Rowing, (int)_seat, true, false);
		}

		// emit a signal when they're done rowing
		// setting the direction doesn't matter in this case but i set them to be forward and backward anyways according to which direction we're canceling
		if (Input.IsActionJustReleased("move_forward"))
		{
			EmitSignal(SignalName.Rowing, (int)_seat, false, true);
		} 
		else if (Input.IsActionJustReleased("move_backward"))
		{
			EmitSignal(SignalName.Rowing, (int)_seat, false, false);
		}
	}

	// input handling while they're in the seat hitbox
	private void _HandleInSeatHitboxState()
	{
		if (Input.IsActionPressed("action_key") && _inSeatHitbox)
		{
			_currPlayerState = PlayerState.Rowing;

			// do the logic to make the player a child of the boat and move the player to the right position
			// make them a child of the boat and just keep their transform so they don't loose their transformation until this point
			Reparent(Boat, true);

			// reposition them in PARENT space (hence why we're using Position and not GlobalPosition)
			Position = Boat.GetSeatOffset(_seat);
			GlobalRotation = Boat.Rotation;
			// if they're on the right side they need to be rotated to be facing outwards when they sit down
			if (_seat == Boat.SeatIndicies.BackRight || _seat == Boat.SeatIndicies.FrontRight)
			{
				// change the local rotation (rotation in parent space) on the y-axis to be 180
				RotateY(Mathf.DegToRad(180));
			}
		}
	}

	// used to set the isInSeatHitbox value and the seat position
	public void SetRowingState(bool isInSeatHitbox, Boat.SeatIndicies newSeat)
	{
		_inSeatHitbox = isInSeatHitbox;
		_seat = newSeat;
	}

	private void OnPauseUIResume()
	{
		// if they press resume button
		if (_currGameState == GameState.Menu)
		{
			_currGameState = GameState.Playing;
			Input.MouseMode = Input.MouseModeEnum.Captured; // capture the mouse again
		}
	}

	private void OnPauseUIExit()
	{
		// if they press exit button
		if (_currGameState == GameState.Menu)
		{
			GetTree().Quit();
		}
	}
}
