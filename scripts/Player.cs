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

	// accumulated movement in the yaw and pitch in radians
	private float _mouseMovementYaw = 0.0f;
	private float _mouseMovementPitch = 0.0f;
	private float _sittingYawDelta = 0.0f; // delta change specifically for sitting down

	// to keep track of if they're choosing to sit or not
	private bool _inSeatHitbox = false;

	// BOAT
	private Boat _boat = new Boat();

	// seat collision objects
	private StaticBody3D _frontLeftSeatCollision;
	private StaticBody3D _frontRightSeatCollision;
	private StaticBody3D _backLeftSeatCollision;
	private StaticBody3D _backRightSeatCollision;

	// need to know which seat they're sitting in
	private Boat.SeatIndicies _seat = Boat.SeatIndicies.FrontLeft;
	/*
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
  }
	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head"); // get the head node
		_crouchingCollision = GetNode<CollisionShape3D>("CrouchingCollision");
		_standingCollision = GetNode<CollisionShape3D>("StandingCollision");
		_pauseUI = GetNode<CanvasLayer>("PauseCanvas");
		// get the boat
		_boat = GetParent().GetNode<Boat>("Boat");
		// get the seat collision objects
		_frontLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontLeftCollision");
		_frontRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontRightCollision");
		_backLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackLeftCollision");
		_backRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackRightCollision");

		// Get the camera reference
    var camera = _head.GetNodeOrNull<Camera3D>("CameraContainer/Camera3D"); 

		// add the player to the 'players' group
		AddToGroup("players");

    // MULTIPLAYER SETUP
    if (IsMultiplayerAuthority())
    {
      // 1. If we ARE the player, enable our camera
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
    else
    {
      // 1. If we are NOT the player, delete the UI.
      _pauseUI.QueueFree();

      // 2. CRITICAL FIX: Delete the Camera for other players!
      // This prevents the "puppet" version of you from hijacking his screen.
      if (camera != null)
      {
				camera.QueueFree(); 
      }
      
      // 3. Disable processing for non-authority
      SetProcess(false);
      SetPhysicsProcess(true); 
    }
	}

	// mouse input logic 
  public override void _Input(InputEvent @event)
  {
		// this is always done so that they can move their head, might wanna change it so that their head is always level
    if ((@event is InputEventMouseMotion mouseEvent) && _currGameState == GameState.Playing)
		{
			// the y rotation of the player in radians based off of the mouse sensitivity 
			_mouseMovementYaw = -Mathf.DegToRad(mouseEvent.Relative.X * MouseSens);

			// the head rotation
			_mouseMovementPitch = -Mathf.DegToRad(mouseEvent.Relative.Y * MouseSens);
			_mouseMovementPitch = Mathf.Clamp(_mouseMovementPitch, Mathf.DegToRad(-89), Mathf.DegToRad(89)); // clamp it to 90 degrees up and down
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
				break;
			case PlayerState.Rowing:
				_HandleRowingState();
				break;
		}
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
		else if (_currGameState == GameState.Playing && _currPlayerState == PlayerState.Rowing) // if they're sitting
		{
			_HandleRowingMovementLogic();
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

		// use mouse input to rotate their head properly
		_HandleStandingPlayerRotation();
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
	// NOTE: chaning their state and initating rowing needs to be rpc calls bc then the variable for this person's player instance will be synced between clients
	// NOTE2: to send an rpc request ONLY to the server use the RpcId function and give it the id of 1
	private void _HandleRowingState()
	{	
		// if they press the spacebar then release them
		if (Input.IsActionJustPressed("ui_accept"))
		{
			// set and broadcast state change
			Rpc(MethodName.Broadcast_SetSitStandState, false, (int)_seat); // set sitting to false (so now we're standing) and update their seat (the seat number doesn't matter here)

			// reset their global rotation
			GlobalRotation = Vector3.Zero;

			// make them stop rowing if they were rowing
			RpcId(1, MethodName.ServerRequestRowing, 0, false, false); // the first boolean is all that matters to make them stop rowing

			return; // STOP after this we don't wanna take anymore input as if we're sitting
		}

		// (Boat.SeatIndicies seat, bool stopStart, bool backForward)
		// if they input w, send out go forward signal
		if (Input.IsActionPressed("move_forward"))
		{
			// tell them to move forward
			// have to send the seat as an int because that's a supported variant type: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_variant.html#c-sharp-variant-compatible-types
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, true, true);
		} 
		else if (Input.IsActionPressed("move_backward")) // don't want them to be able to do both at the same time so if they're pressing both they'll go forward
		{
			// tell them to move backward
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, true, false);
		}

		// emit a signal when they're done rowing
		// setting the direction doesn't matter in this case but i set them to be forward and backward anyways according to which direction we're canceling
		if (Input.IsActionJustReleased("move_forward"))
		{
			// the first boolean is all that matters to make them allowed to row or not, so just setting that to false stops them
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, false, true);
		} 
		else if (Input.IsActionJustReleased("move_backward"))
		{
			RpcId(1, MethodName.ServerRequestRowing, (int)_seat, false, false); 
		}
	}

	// input handling while they're in the seat hitbox
	public void HandleInSeatHitboxState(int seat)
	{
		_currPlayerState = PlayerState.Rowing;

		// set and broadcast state change
		Rpc(nameof(Broadcast_SetSitStandState), true, seat); // set sitting to true and update their seat

		// set their rotation
		GlobalRotation = _boat.Rotation;
		// if they're on the right side they need to be rotated to be facing outwards when they sit down
		if (_seat == Boat.SeatIndicies.BackRight || _seat == Boat.SeatIndicies.FrontRight)
		{
			// change the local rotation (rotation in parent space) on the y-axis to be 180
				RotateY(Mathf.DegToRad(180));
		}
	}

	// Function that has logic to move their head while rowing
	private void _HandleRowingMovementLogic()
	{
		StaticBody3D seatCollision = new StaticBody3D();
		// set their global transform to be that of the boat seat they're sitting on
		if (_seat == Boat.SeatIndicies.FrontLeft)
		{
			seatCollision = _frontLeftSeatCollision;
		} 
		else if (_seat == Boat.SeatIndicies.FrontRight)
		{
			seatCollision = _frontRightSeatCollision;
		}
		else if (_seat == Boat.SeatIndicies.BackLeft)
		{
			seatCollision = _backLeftSeatCollision;
		}
		else if (_seat == Boat.SeatIndicies.BackRight)
		{
			seatCollision = _backRightSeatCollision;
		}

		// set the basis of the player to the basis of the seat
		GlobalPosition = seatCollision.GlobalPosition; // i think this line makes it so that it has to run in the physics process funcion

		// set the rotations according to mouse movement
		_HandleSittingPlayerRotation(seatCollision);
	}

	private void _HandleStandingPlayerRotation()
	{
		// rotation logic based on mouse stuff 
		RotateY(_mouseMovementYaw);

		// set the head rotation
		_head.RotateX(_mouseMovementPitch);
		// clamp their head pitch
		_head.Rotation = new Vector3(Mathf.Clamp(_head.Rotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89)), _head.Rotation.Y, _head.Rotation.Z);

		// YOU ALWAYS HAVE TO RESET YAW AND PITCH MOVEMENT AFTER USING IT 
		_mouseMovementPitch = 0.0f;
		_mouseMovementYaw = 0.0f;
	}

	private void _HandleSittingPlayerRotation(StaticBody3D seatCollision)
	{
		// directly setting the rotation of something is bad so instead we're taking the global basis, then just adding the rotation to that basis and then setting the basis
		_sittingYawDelta += _mouseMovementYaw;
		Basis seatGlobalBasis = seatCollision.GlobalBasis;
		Basis swivelBasis = new Basis(Vector3.Up, _sittingYawDelta);
		GlobalBasis = seatGlobalBasis * swivelBasis; // with matrix multiplication this works ig
		
		// regular head pitch rotation since the head is a child of the player
		_head.RotateX(_mouseMovementPitch);
		// clamp so they cant move their head all the way around
		Vector3 headRot = _head.Rotation;
    headRot.X = Mathf.Clamp(headRot.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
    _head.Rotation = headRot;

		// must reset these variables
		_mouseMovementPitch = 0.0f;
    _mouseMovementYaw = 0.0f;
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

	// this basically needs to exist so that the variable for setting the state is synced between everyone for THIS player
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void Broadcast_SetSitStandState(bool isSitting, int seatIdx)
	{
		// set the rowing state
		_currPlayerState = isSitting ? PlayerState.Rowing : PlayerState.Standing;
    _seat = (Boat.SeatIndicies)seatIdx;
	
		// disable/enable player collision while sitting
		// _standingCollision.Disabled = isSitting;
    // _crouchingCollision.Disabled = isSitting;
	}

	// need to make sending the signal a synced thing between everyone
	// CallLocal CANNOT be true for this because then for clients (non-server players) their local version could get out of sync with the server
	// HOWEVER, we need it to be so that the server-client person can send request to themselves.
	// THIS MEANS: we need to use RpcId given an id of 1 **ANYTIME** we run this function, so that it's only ever sent to the server
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)] // we don't need CallLocal i think bc we're not trying to change the local version of our game we're chainging the server which will sync to our client
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

	// reset function that gets called by the level script
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)] // only update the server so the CallLocal should be false i think
	private void SyncReset()
	{
		// set the player into the standing state and reset their position and velocity
		_currPlayerState = PlayerState.Standing;
		Position = Vector3.Zero;
		Rotation = Vector3.Zero;
		Velocity = Vector3.Zero;
	}
}
