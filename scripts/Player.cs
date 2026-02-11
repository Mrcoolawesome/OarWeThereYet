using Godot;
using System;
using System.Net.Http;

public partial class Player : CharacterBody3D
{
	// Exported variables
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float WalkingSpeed = 5.0f;
	[Export] public float SprintSpeed = 8.0f;
	[Export] public float CrouchingSpeed = 3.0f;
	[Export] public float MouseSens = 0.4f;
	[Export] public float LerpSpeed = 10.0f;
	[Export] public float CrouchLerpSpeed = 10.0f;

	// Private variables
	private float _currSpeed = 5.0f;
	private float _gravity = 9.8f;
	private Vector3 _direction = Vector3.Zero;
	private Node3D _head;
	private CollisionShape3D _crouchingCollision;
	private CollisionShape3D _standingCollision;
	private float _crouchingDepth = -0.5f; // this is relative to the regular head 

	// Accumulated movement in the yaw and pitch in radians
	private float _mouseMovementYaw = 0.0f;
	private float _mouseMovementPitch = 0.0f;
	private float _sittingYawDelta = 0.0f; // delta change specifically for sitting down

	// BOAT
	private Boat _boat = new Boat();

	//Pause Menu
	private CanvasLayer _pauseUI;

	// Seat collision objects
	private StaticBody3D _frontLeftSeatCollision;
	private StaticBody3D _frontRightSeatCollision;
	private StaticBody3D _backLeftSeatCollision;
	private StaticBody3D _backRightSeatCollision;

	// Global variable for seat player is sitting in
	private Boat.SeatIndicies _seat = Boat.SeatIndicies.FrontLeft;
	/*
		front left localShapeIndex: 0
		front right localShapeIndex: 1
		back right localShapeIndex: 2
		back left localShapeIndex: 3
	*/

	// Player state machine. 
	// TODO: I made this a state machine so that we could add swimming in the future
	private enum PlayerState
	{
		Rowing,
		Standing
	}

	// Game state machine
	private enum GameState {
		Playing,
		Menu,
	}

	// Game state default is menu
	private GameState _currGameState = GameState.Menu;

	// Player state default is standing
	private PlayerState _currPlayerState = PlayerState.Standing;

	public override void _EnterTree()
	{
		SetMultiplayerAuthority(int.Parse(Name.ToString()));
	}

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_crouchingCollision = GetNode<CollisionShape3D>("CrouchingCollision");
		_standingCollision = GetNode<CollisionShape3D>("StandingCollision");
		_pauseUI = GetNode<CanvasLayer>("PauseCanvas");
		_boat = GetParent().GetNode<Boat>("Boat");

		_frontLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontLeftCollision");
		_frontRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontRightCollision");
		_backLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackLeftCollision");
		_backRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackRightCollision");

		// Get the camera reference
		Camera3D camera = _head.GetNodeOrNull<Camera3D>("CameraContainer/Camera3D"); 

		// Add the player to the 'players' group
		AddToGroup("players");

		// MULTIPLAYER SETUP
		// If we are the player
		if (IsMultiplayerAuthority())
		{
			// Enable our camera
			if (camera != null)
			{
				camera.Current = true;
			}

			_pauseUI.Visible = _currGameState == GameState.Menu;
			
			if (_currGameState == GameState.Menu)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}
		// If we are not the player
		else
		{
			// Delete UI
			_pauseUI.QueueFree();

			// Delete the Camera for other players
			if (camera != null)
			{
				camera.QueueFree(); 
			}
			
			// Disable processing for non-authority
			SetProcess(false);
			SetPhysicsProcess(true); 
		}
	}

	// Mouse input logic 
  public override void _Input(InputEvent @event)
  {
		// This is always done so that they can move their head
		// TODO: Might wanna change this so that player head is always level
    if ((@event is InputEventMouseMotion mouseEvent) && _currGameState == GameState.Playing)
		{
			// The y rotation of the player in radians based off of the mouse sensitivity 
			_mouseMovementYaw = -Mathf.DegToRad(mouseEvent.Relative.X * MouseSens);

			// The head rotation
			_mouseMovementPitch = -Mathf.DegToRad(mouseEvent.Relative.Y * MouseSens);
			_mouseMovementPitch = Mathf.Clamp(_mouseMovementPitch, Mathf.DegToRad(-89), Mathf.DegToRad(89)); // clamp it to 90 degrees up and down
		}
  }

  //PROCESS CODE AND ALL ASSOCIATED FUNCTIONS
  public override void _Process(double delta)
  {
    // Hide/Unhide PauseUI depending on game state
		switch(_currGameState)
		{
			case GameState.Playing:
				PlayingStateProcess();
				_pauseUI.Visible = false;
				break;
			case GameState.Menu:
				_pauseUI.Visible = true;
				break;
		}
  }

	private void PlayingStateProcess()
	{
		// Menu logic
		// We use IsActionJustPressed because it's a trigger and not a continuous input event
		if (Input.IsActionJustPressed("ui_cancel")) 
		{
			_currGameState = GameState.Menu;
			// Release the mouse
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		switch(_currPlayerState)
		{
			case PlayerState.Standing:
				break;
			case PlayerState.Rowing:
				RowingStateProcess();
				break;
		}
	}

	// Rowing state input handling
	/*
	Chaning state and initating rowing needs to be rpc calls so the variable for 
	this person's player instance will be synced between clients.
	To send an rpc request ONLY to the server use the RpcId function and give it the id of 1
	*/
	private void RowingStateProcess()
	{	
		// Release player if they press space
		if (Input.IsActionJustPressed("ui_accept"))
		{
			//TODO: Make players head rotation consistent between sitting and rowing

			// Broadcast stop rowing. The first boolean is all that matters to make them stop rowing
			RpcId(1, MethodName.ServerRequestRowing, 0, false, false);

			// Reset their global rotation and position
			GlobalRotation = Vector3.Zero;
			StaticBody3D seatCollision = GetCurrentSeat();
			GlobalPosition = seatCollision.GlobalPosition + new Vector3(0, 1, 0); 

			// Broadcast sitting to false and update their seat (the seat number doesn't matter here)
			Rpc(MethodName.Broadcast_SetSitStandState, false, (int)_seat);

			return; // STOP after this we don't wanna take anymore input as if we're sitting
		}

		// (Boat.SeatIndicies seat, bool stopStart, bool backForward)
		if (Input.IsActionPressed("move_forward"))
		{
			// Broadcast move forward
			// have to send the seat as an int because that's a supported variant type: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_variant.html#c-sharp-variant-compatible-types
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, true, true);
		} 
		// If user pressing forward and backward they'll go forward
		else if (Input.IsActionPressed("move_backward"))
		{
			// Broadcast move backward
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, true, false);
		}

		// Emit a signal when they're done rowing
		// Setting the direction doesn't matter in this case
		if (Input.IsActionJustReleased("move_forward"))
		{
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, false, true);
		} 
		else if (Input.IsActionJustReleased("move_backward"))
		{
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, false, false); 
		}
	}

  //PHYSICS PROCESS CODE AND ALL ASSOCIATED FUNCTIONS
	// Logic for movement depending on player state
	public override void _PhysicsProcess(double delta)
	{
		// Always apply gravity 
		Gravity(delta);

		if (_currGameState == GameState.Playing && _currPlayerState == PlayerState.Standing)
		{
			StandingStatePhysicsProcess(delta);
			CrouchSprintPhysicsProcess(delta);
		} 
		else if (_currGameState == GameState.Playing && _currPlayerState == PlayerState.Rowing)
		{
			RowingStatePhysicsProcess();
		}

		// Always apply MoveAndSlide unless they're rowing
		if (_currPlayerState != PlayerState.Rowing)
		{
			MoveAndSlide();
		}
	}

	private void Gravity(double delta)
	{
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		Velocity = velocity;
	}

	private void StandingStatePhysicsProcess(double delta)
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
		// Move towards zero vector if we're not trying to move anywhere
		if (inputDir == Vector2.Zero)
		{
			targetDirection = Vector3.Zero;
		}

		// Set direction and move in that direction
		_direction = _direction.MoveToward(targetDirection, (float)delta * LerpSpeed);
		velocity.X = _direction.X * _currSpeed;
		velocity.Z = _direction.Z * _currSpeed;
		Velocity = velocity;

		// Handle mouse input while standing
		StandingStatePlayerRotation();
	}

	private void CrouchSprintPhysicsProcess(double delta)
	{
		// They can only be crouching or sprinting not both hence the else if
		// If they are pressing both then the speed will be set to crouching speed
		if (Input.IsActionPressed("crouch"))
		{
			_currSpeed = CrouchingSpeed;

			// Set the head height to be offset by the crouching depth
			Vector3 targetHeadPosition = new Vector3(_head.Position.X, _crouchingDepth, _head.Position.Z);
			_head.Position = _head.Position.MoveToward(targetHeadPosition, (float)delta * CrouchLerpSpeed);

			// Disable the staning collision shape
			_standingCollision.Disabled = true;
			_crouchingCollision.Disabled = false;
		} 
		else
		{
			// Set head position to be default when not crouching
			Vector3 targetHeadPosition = new Vector3(_head.Position.X, 0.0f, _head.Position.Z);
			_head.Position = _head.Position.MoveToward(targetHeadPosition, (float)delta * CrouchLerpSpeed);

			// Enable the standing collision shape
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

	private void RowingStatePhysicsProcess()
	{
		// Set their global transform to be that of the boat seat they're sitting on
		StaticBody3D seatCollision = GetCurrentSeat();
		GlobalPosition = seatCollision.GlobalPosition;

		// Handle mouse input while sitting
		RowingStatePlayerRotation(seatCollision);
	}

	private void StandingStatePlayerRotation()
	{
		RotateY(_mouseMovementYaw);

		// Set and clamp the head rotation
		_head.RotateX(_mouseMovementPitch);
		_head.Rotation = new Vector3(Mathf.Clamp(_head.Rotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89)), _head.Rotation.Y, _head.Rotation.Z);

		// YOU ALWAYS HAVE TO RESET YAW AND PITCH MOVEMENT AFTER USING IT 
		_mouseMovementPitch = 0.0f;
		_mouseMovementYaw = 0.0f;
	}

	private void RowingStatePlayerRotation(StaticBody3D seatCollision)
	{
		/* 
		Directly setting the rotation is bad so we take the global basis,
		add the rotation to that basis, and then set the basis
		*/
		_sittingYawDelta += _mouseMovementYaw;
		Basis seatGlobalBasis = seatCollision.GlobalBasis;
		Basis swivelBasis = new Basis(Vector3.Up, _sittingYawDelta);
		GlobalBasis = seatGlobalBasis * swivelBasis;
		
		// Regular head pitch rotation since the head is a child of the player
		_head.RotateX(_mouseMovementPitch);
		// Clamp so they cant move their head all the way around
		Vector3 headRot = _head.Rotation;
    headRot.X = Mathf.Clamp(headRot.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
    _head.Rotation = headRot;

		// YOU ALWAYS HAVE TO RESET YAW AND PITCH MOVEMENT AFTER USING IT 
		_mouseMovementPitch = 0.0f;
    _mouseMovementYaw = 0.0f;
	}

	public void SitInSeat(int seat)
	{
		_currPlayerState = PlayerState.Rowing;

		// Broadcast sitting to true and update their seat
		Rpc(nameof(Broadcast_SetSitStandState), true, seat);
	}

	//Signals recieved from Pause Menu UI
	private void OnPauseUIResume()
	{
		// If they press resume button
		if (_currGameState == GameState.Menu)
		{
			_currGameState = GameState.Playing;
		// Capture mouse
			Input.MouseMode = Input.MouseModeEnum.Captured;
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

	//RPC Functions
	// Makes sure that PlayerState changes is synced for everyone
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void Broadcast_SetSitStandState(bool isSitting, int seatIdx)
	{
		// set the rowing state
		_currPlayerState = isSitting ? PlayerState.Rowing : PlayerState.Standing;
		_seat = (Boat.SeatIndicies)seatIdx;
	}

	//TODO: add safeguards to functions that should only be called on the server
	// We need to use RpcId = 1 ANYTIME we run this function, so that it's only ever sent to the server
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ServerRequestRowing(int seatIdx, bool stopStart, bool backForward)
	{
		// The Server hears this and emits the signal locally to the Boat
		GlobalSignalServer.Instance.EmitSignal(GlobalSignalServer.SignalName.Rowing, seatIdx, stopStart, backForward);
	}

	
	public void Reset()
	{
		// Only the server should issue this command
		if (Multiplayer.IsServer())
		{
			// Tell EVERYONE (including the server) to run the SyncReset function
			RpcId(1, nameof(SyncReset));
		}
	}

	// We need to use RpcId = 1 ANYTIME we run this function, so that it's only ever sent to the server
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SyncReset()
	{
		// Set the player into the standing state and reset their position and velocity
		_currPlayerState = PlayerState.Standing;
		Position = Vector3.Zero;
		Rotation = Vector3.Zero;
		Velocity = Vector3.Zero;
	}

	// Helper functions
	private StaticBody3D GetCurrentSeat()
	{
		if (_seat == Boat.SeatIndicies.FrontLeft)
		{
			return _frontLeftSeatCollision;
		} 
		else if (_seat == Boat.SeatIndicies.FrontRight)
		{
			return _frontRightSeatCollision;
		}
		else if (_seat == Boat.SeatIndicies.BackLeft)
		{
			return _backLeftSeatCollision;
		}
		else if (_seat == Boat.SeatIndicies.BackRight)
		{
			return _backRightSeatCollision;
		}
		else
		{
			GD.Print("Error: Failed to get seat");
			return null;
		}
	}
}
