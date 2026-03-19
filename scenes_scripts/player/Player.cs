using Godot;
using Godot.Collections;
using System;

public partial class Player : CharacterBody3D, ISyncBuffer
{
	// Exported variables
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float WalkingSpeed = 5.0f;
	[Export] public float SprintSpeed = 8.0f;
	[Export] public float CrouchingSpeed = 3.0f;
	[Export] public float AirSpeed = 3.0f;
	[Export] public float MouseSens = 0.4f;
	[Export] public float LerpSpeed = 10.0f;
	[Export] public float CrouchLerpSpeed = 10.0f;
	[Export] public float NetworkLerpSpeed = 10.0f;
	[Export] public Array<Variant> State { get; set; }

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

	// BOAT
	private Boat _boat = new Boat();

	//Pause Menu canvas
	private CanvasLayer _pauseUICanvas;
	// Pause menu ui
	private Control _pauseUI;
	// HUD
	private CanvasLayer _hud;
	// Inventory Menu
	private InventoryUi _invUI;

	// Seat collision objects
	private StaticBody3D _frontLeftSeatCollision;
	private StaticBody3D _frontRightSeatCollision;
	private StaticBody3D _backLeftSeatCollision;
	private StaticBody3D _backRightSeatCollision;

	// Interact ray
	private RayCast3D _interactRay;

	// Different player models
	private MeshInstance3D _localPlayerModel;
	private Node3D _fullPlayerModel;
	private MeshInstance3D _fullPlayerModelBody;
	private MeshInstance3D _fullPlayerModelHead;
	private MeshInstance3D _fullPlayerModelVest;
	private MeshInstance3D _eyeBallLeft;
	private MeshInstance3D _eyeBallRight;
	private MeshInstance3D _pupilLeft;
	private MeshInstance3D _pupilRight;

	// Global variable for seat player is sitting in
	private Boat.SeatIndicies _seat = Boat.SeatIndicies.FrontLeft;
	/*
		front left localShapeIndex: 0
		front right localShapeIndex: 1
		back right localShapeIndex: 2
		back left localShapeIndex: 3
	*/

	// Player state machine. 
	public enum PlayerState
	{
		Rowing,
		Standing
	}

	// Game state machine
	public enum GameState {
		Playing,
		Menu,
	}

	// Game state default is menu
	public GameState CurrGameState = GameState.Playing;

	// Player state default is standing
	public PlayerState CurrPlayerState = PlayerState.Standing;

	// Stuff for player keeping momentum while in the air
	private Vector3 _initialVelocity;
	private bool _isOnGround;

	// booleans for making sure we only apply the new state once for client side stuff
	private bool _applyNewPositionState = false;
	private bool _applyNewRotationState = false;
	private bool _applyNewVelocityState = false;
	// new state that the boat should be set to
	private Transform3D _newPositionState;
	// new rotation state
	private Basis _newRotationState;

	// When true, the server is currently driving this player's transform.
	private bool _isServerCaptured = false;

  public override void _EnterTree()
	{
		// THIS IS VERY IMPORTANT
		// this sets the multiplayer authority of THIS NODE to be the player with the specified id.
		// we made the id of the player we want to be in charge of this node to be the name of the node, so we just use that
		// name to get the id of the client we want to make the authority.
		SetMultiplayerAuthority(int.Parse(Name.ToString()));
	}

	public override void _Ready()
	{
		_head = GetNode<Node3D>("Head");
		_crouchingCollision = GetNode<CollisionShape3D>("CrouchingCollision");
		_standingCollision = GetNode<CollisionShape3D>("StandingCollision");
		_pauseUICanvas = GetNode<CanvasLayer>("PauseCanvas");
		_boat = GetParent().GetNode<Boat>("Boat");
		_hud = GetNode<CanvasLayer>("HUD");
		_invUI = GetNode<InventoryUi>("InventoryUI");
		_fullPlayerModelHead = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/head");
		_fullPlayerModelBody = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/body");
		_fullPlayerModelVest = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/life vest");
		_localPlayerModel = GetNode<MeshInstance3D>("LocalPlayerModel/Armature/Skeleton3D/body");
		_fullPlayerModel = GetNode<Node3D>("FullPlayerModel");
		_eyeBallLeft = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/eyeBallLeft");
		_eyeBallRight = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/eyeBallRight");
		_pupilLeft = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/pupilLeft");
		_pupilRight = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/pupilRight");

		_interactRay = GetNode<RayCast3D>("Head/CameraContainer/Camera3D/InteractRay");

		_frontLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontLeftCollision");
		_frontRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/FrontRightCollision");
		_backLeftSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackLeftCollision");
		_backRightSeatCollision = _boat.GetNode<StaticBody3D>("SeatContainer/BackRightCollision");

		// subscribe to the global signal server call to respawn the player to the boat
		GlobalSignalServer.Instance.RespawnPlayer += OnPauseUIRespawnPlayer;

		// Get the camera reference
		Camera3D camera = _head.GetNodeOrNull<Camera3D>("CameraContainer/Camera3D"); 

		// Add the player to the 'players' group
		AddToGroup("players");

		// set the state array from the server's perspective
		SetStateArray();

		// client code for when setting up their camera and stuff
		// if we are the player, then use the camera for this player
		// IsMultiplayerAuthority checks if the current client is the multiplayer authority of THIS current NODE 
		if (IsMultiplayerAuthority())
		{
			// Spawn sitting in next available seat (only the authority should trigger this)
			RequestSitInSeat(-1);

			// Enable our camera
			if (camera != null)
			{
				camera.Current = true;
			}

			// set the current game state to be the menu state	
			if (CurrGameState == GameState.Menu)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}

			// then set the local player model to visible, the regular one also needs to be visible but the meshes will be set to only cast shadows in the code below
			_localPlayerModel.Visible = true;
			_fullPlayerModel.Visible = true;

			// then set the shadows for all the mesh instances of the real model to be casted and the local model shouldn't cast any shadows
			_fullPlayerModelBody.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_fullPlayerModelHead.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_fullPlayerModelVest.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_eyeBallLeft.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_eyeBallRight.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_pupilLeft.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;
			_pupilRight.CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly;

			// the shadow of the local shoudn't be cast
			_localPlayerModel.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}
		// If we are not the player
		else
		{
			// Delete UI
			_pauseUICanvas.QueueFree();

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
    if ((@event is InputEventMouseMotion mouseEvent) && CurrGameState == GameState.Playing)
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
		switch(CurrGameState)
		{
			case GameState.Playing:
				PlayingStateProcess();
				break;
			case GameState.Menu:
				MenuStateProcess();
				break;
		}
  }

	private void PlayingStateProcess()
	{
		_pauseUICanvas.Visible = false;
		_hud.Visible = true;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_interactRay.Enabled = true;

		// Menu logic
		// We use IsActionJustPressed because it's a trigger and not a continuous input event
		if (Input.IsActionJustPressed("ui_cancel")) 
		{
			CurrGameState = GameState.Menu;
		}

		switch(CurrPlayerState)
		{
			case PlayerState.Standing:
				_interactRay.Enabled = true;
				break;
			case PlayerState.Rowing:
				_interactRay.Enabled = false;
				RowingStateProcess();
				break;
		}
	}

	private void MenuStateProcess()
	{
		if (Input.IsActionJustPressed("ui_cancel")) 
		{
			CurrGameState = GameState.Playing;			

			// Hide Inventory if open
			if (_invUI.isOpen())
			{
				_invUI.Close();
			}
			return; // Skip menu UI updates since we just transitioned to Playing
		}

		if (!_invUI.isOpen()) { _pauseUICanvas.Visible = true; };
		_hud.Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		_interactRay.Enabled = false;
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
			// Broadcast stop rowing. The first boolean is all that matters to make them stop rowing
			RequestRowing((int)_seat, false, false);

			// Reset their global position
			GlobalPosition = GetCurrentSeat().GlobalPosition + new Vector3(0, 1, 0); 

			// Broadcast sitting to false and update their seat (the seat number doesn't matter here)
			Rpc(MethodName.SetSitStandState, false, (int)_seat);

			Rpc(nameof(BroadcastOarAnimation), (int)_seat, 1, false);

			return; // STOP after this we don't wanna take anymore input as if we're sitting
		}

		// (Boat.SeatIndicies seat, bool stopStart, bool backForward)
		if (Input.IsActionPressed("move_forward"))
		{
			// Broadcast move forward
			// have to send the seat as an int because that's a supported variant type: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_variant.html#c-sharp-variant-compatible-types
			RequestRowing((int)_seat, true, true);

			// trigger the oar animation as well
			Rpc(nameof(BroadcastOarAnimation), (int)_seat, 1, true);
		} 
		// If user pressing forward and backward they'll go forward
		else if (Input.IsActionPressed("move_backward"))
		{
			// Broadcast move backward
			RequestRowing((int)_seat, true, false);

			// trigger the oar animation as well
			Rpc(nameof(BroadcastOarAnimation), (int)_seat, -1, true);
		}

		// Emit a signal when they're done rowing
		// Setting the direction doesn't matter in this case
		if (Input.IsActionJustReleased("move_forward"))
		{
			RequestRowing((int)_seat, false, true);

			// stop the rowing animation too
			Rpc(nameof(BroadcastOarAnimation), (int)_seat, 1, false);
		} 
		else if (Input.IsActionJustReleased("move_backward"))
		{
			RequestRowing((int)_seat, false, false); 

			// stop the rowing animation too (direction doesn't actually matter here since we're just stopping the animation)
			Rpc(nameof(BroadcastOarAnimation), (int)_seat, -1, false);
		}
	}

  //PHYSICS PROCESS CODE AND ALL ASSOCIATED FUNCTIONS
	// Logic for movement depending on player state
	public override void _PhysicsProcess(double delta)
	{
		// do all the movement and stuff if we're the owner of this instance, we'll sync it to the clients 
		if (IsMultiplayerAuthority())
		{
			if (_isServerCaptured)
			{
				// During capture, the host continuously pushes transform updates via RPC.
				Velocity = Vector3.Zero;
				return;
			}

			// Always apply gravity 
			Gravity(delta);

			// always set the state array as often as possible AS THE CLIENT
			SetStateArray();

			if (CurrGameState == GameState.Playing && CurrPlayerState == PlayerState.Standing)
			{
				StandingStatePhysicsProcess(delta);
				CrouchSprintPhysicsProcess(delta);
			} 
			else if (CurrGameState == GameState.Playing && CurrPlayerState == PlayerState.Rowing)
			{
				RowingStatePhysicsProcess();
			}

			// Always apply MoveAndSlide unless they're rowing
			if (CurrPlayerState != PlayerState.Rowing)
			{
				MoveAndSlide();
			}
		}
		else // if we're not the owner of this instance, then we're just gonna sync their position and stuff (this is for 'network puppets')
		{
			// Do client side processing 
      Gravity(delta);

			// do local movement for the puppet while in the boat
			if (CurrPlayerState == PlayerState.Rowing)
      {
        // Force them to the seat perfectly. The boat is already handling movement.
        RowingStatePhysicsProcess();
      }
      else
      {
        // If they are standing, simulate their gravity and movement locally
        Gravity(delta);
        MoveAndSlide();
      }

      // Sync network data 
      SyncAndLerpClientDataProcess(delta); // this deals with the sitting state
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
			// actually make them jump
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

		// Frame when player leaves ground
		if (!IsOnFloor() && _isOnGround)
		{
			_initialVelocity = GetPlatformVelocity() + velocity;
		}

		if (IsOnFloor())
		{
			// Set direction and move in that direction
			_direction = _direction.MoveToward(targetDirection, (float)delta * LerpSpeed);
			velocity.X = _direction.X * _currSpeed;
			velocity.Z = _direction.Z * _currSpeed;
		} 
		else 
		{
			_direction = _direction.MoveToward(targetDirection, (float)delta * LerpSpeed);
			velocity.X = _direction.X * _currSpeed + _initialVelocity.X;
			velocity.Z = _direction.Z * _currSpeed + _initialVelocity.Z;
		}

		// Handle mouse input while standing
		PlayerRotation();

		_isOnGround = IsOnFloor();

		Velocity = velocity;
	}

	private void CrouchSprintPhysicsProcess(double delta)
	{
		// If player is not on the floor ignore all other logic
		if (!IsOnFloor())
		{
			_currSpeed = AirSpeed;
		}
		else
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
	}

	private void RowingStatePhysicsProcess()
	{
		// Set their global transform to be that of the boat seat they're sitting on
		StaticBody3D seatCollision = GetCurrentSeat();
		GlobalPosition = seatCollision.GlobalPosition;

		// Handle mouse input while sitting
		PlayerRotation();
	}

	private void PlayerRotation()
	{
		RotateY(_mouseMovementYaw);

		// Set and clamp the head rotation
		_head.RotateX(_mouseMovementPitch);
		_head.Rotation = new Vector3(Mathf.Clamp(_head.Rotation.X, Mathf.DegToRad(-89), Mathf.DegToRad(89)), _head.Rotation.Y, _head.Rotation.Z);

		// YOU ALWAYS HAVE TO RESET YAW AND PITCH MOVEMENT AFTER USING IT 
		_mouseMovementPitch = 0.0f;
		_mouseMovementYaw = 0.0f;
	}

	//Signals recieved from Pause Menu UI
	private void OnPauseUIResume()
	{
		// If they press resume button
		if (CurrGameState == GameState.Menu)
		{
			CurrGameState = GameState.Playing;
		// Capture mouse
			Input.MouseMode = Input.MouseModeEnum.Captured;

			// unhide the hud
			_hud.Visible = true;
		}
	}

	private void OnPauseUIRespawnPlayer()
	{
		// set their position to be the position of the boat but just a little higher so they're not just clipping into it
		// this shouldn't need to be an rpc call i think because the multiplayer synchronzier should just handle it
		RequestSitInSeat(-1);

		// put them into the playing state after that so the pause ui goes away
		CurrGameState = GameState.Playing;
	}

	//RPC Functions
	public void RequestSitInSeat(int seat)
	{
		RpcId(1, nameof(SitInSeat), seat);
	}
	
	// If seat == -1, sit in next available seat
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SitInSeat(int seat)
	{
		if (seat == -1) seat = _boat.NextAvailableSeat();
		
		if (_boat.IsSeatAvailable(seat))
		{
			// Broadcast sitting to true and update their seat
			Rpc(nameof(SetSitStandState), true, seat);
		}
	}

	// Makes sure that PlayerState changes is synced for everyone
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetSitStandState(bool isSitting, int seatIdx)
	{
		// Broadcast occupied seat
		_boat.OccupiedSeats[seatIdx] = isSitting;

		// set the rowing state
		CurrPlayerState = isSitting ? PlayerState.Rowing : PlayerState.Standing;
		_seat = (Boat.SeatIndicies)seatIdx;
	}

	// Wrapper for ServerRequestRowing RPC function
	public void RequestRowing(int seatIdx, bool stopStart, bool backForward)
	{
		RpcId(1, MethodName.ServerRequestRowing, seatIdx, stopStart, backForward);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetCapturedByLifepreserver(bool captured)
	{
		_isServerCaptured = captured;
		if (captured)
		{
			// Disable both collision shapes so player doesn't collide with preserver
			if (_standingCollision != null) _standingCollision.Disabled = true;
			if (_crouchingCollision != null) _crouchingCollision.Disabled = true;
		}
		else
		{
			// Re-enable both collision shapes
			if (_standingCollision != null) _standingCollision.Disabled = false;
			if (_crouchingCollision != null) _crouchingCollision.Disabled = true; // Only standing enabled by default
			Velocity = Vector3.Zero;
			Rotation = Vector3.Zero;
		}
	}

	   [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SyncCapturedTransform(Vector3 globalPosition, Vector3 globalRotation)
	{
		GlobalPosition = globalPosition;
		GlobalRotation = globalRotation;
		Velocity = Vector3.Zero;
		SetStateArray(); // Immediately broadcast new state to clients
	}

	// THIS FUNCTION SHOULDN'T BE CALLED DIRECTLY
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ServerRequestRowing(int seatIdx, bool stopStart, bool backForward)
	{
		// Extra safeguard to make sure function only runs on server
		if(!Multiplayer.IsServer()) return;

		// The Server hears this and emits the signal locally to the Boat
		GlobalSignalServer.Instance.EmitSignal(GlobalSignalServer.SignalName.Rowing, seatIdx, stopStart, backForward);
	}

	// this will have everyone else's animations for the oars play when this client triggers or un-triggers it
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void BroadcastOarAnimation(int seat, int direction, bool startStop)
	{
		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.AnimateOar), seat, direction, startStop);
	}

	public void Reset()
	{
		// Only the server should issue this command
		if (Multiplayer.IsServer())
		{
			// Tell EVERYONE (including the server) to run the SyncReset function
			Rpc(nameof(SyncReset));

			// stop the rowing animation too
			Rpc(nameof(BroadcastOarAnimation), (int)_seat, 1, false);

			// get rid of their pause ui after that
			CurrGameState = GameState.Playing;
		}
	}

	// We need to use RpcId = 1 ANYTIME we run this function, so that it's only ever sent to the server
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SyncReset()
	{
		// Set the player into the standing state and reset their position and velocity
		// _currPlayerState = PlayerState.Standing;
		// Position = Vector3.Zero;
		// Rotation = Vector3.Zero;
		// Velocity = Vector3.Zero;

		// Sit in boat which should be reset
		if (IsMultiplayerAuthority())
			RequestSitInSeat(-1);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void OpenInventory(Inventory inventory)
	{
		_invUI.Open(inventory);
		CurrGameState = GameState.Menu;
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

	// LOGIC FOR CLIENT SYNCING POSITION AND ROTATION INFORMATION
	// client updates their position ig
  public void SetStateArray()
  {
		//if (_isServerCaptured) return;

		if (IsMultiplayerAuthority())
		{
			State = [Position, Quaternion, Velocity, (int)CurrPlayerState, (int)_seat];
		}
  }

	// ran when the 'synchronized()' signal is sent out so that it's ALWAYS updating
  public void SyncPosIfNeeded()
  {
    // only ran client side
		if (!IsMultiplayerAuthority()) // this is kinda redundant since we already check this before we call this function but whatever
		{
			// Syncing the rotation:
			// make a new basis to use to transform the boat position
			// set to the current rotation by default because we only wanna change it if we're within a threshold
			// get synced rotation and curr rotation
			Quaternion syncedRotation = (Quaternion)State[1];
			Quaternion currRotation = Quaternion;
			// if the difference is greater than some threshold then lerp
			if (Mathf.Abs(currRotation.AngleTo(syncedRotation)) > Mathf.DegToRad(0.1f)) // if the difference is greater than 0.1 of a degree than sync
			{
				// 'return' the rotation state
				_newRotationState = new Basis(syncedRotation);
				_applyNewRotationState = true;
			}

			// Syncing the position:
			// get the synced position
			Vector3 syncedPosition = (Vector3)State[0];
			// difference between our client side position and the host's position
			float posDiff = (syncedPosition - Position).Length();
			// make a new transform, that just uses our current position by default and the synced position if we've deviated too far
			// if it's greater than 0.5 meters apart then lerp ours to the hosts' position
			if (posDiff > 0.05f) // we want them to be very close
			{
				// this is where the new transformation is 'returned'
				_newPositionState = new Transform3D(Basis, syncedPosition);
				_applyNewPositionState = true;
			}

			// we want this to just always happen when we're updated
			_applyNewVelocityState = true;
		}
  }

	private void SyncAndLerpClientDataProcess(double delta)
	{
		// to account for not having any state at the very beginning
		if (State == null)
		{
			return;
		}
		// Read and apply the state and seat from the authority
    CurrPlayerState = (PlayerState)(int)State[3];
    _seat = (Boat.SeatIndicies)(int)State[4];
		// get the 'speed' at which we lerp at 
		float weight = (float)delta * NetworkLerpSpeed; // state.Step is like the 'delta' parameters given from Process
		// apply the updated state variable if any changes were made
		// ONLY correct position and velocity if they are walking around normally
    if (CurrPlayerState != PlayerState.Rowing)
    {
			if (_applyNewPositionState)
			{
				// interpolate to the new position
				Transform = Transform.InterpolateWith(_newPositionState, weight);
				
				// Only turn off the flag once we are practically touching the target
				// we have to do this because lerping won't instantly snap us to the target postition, so we need to keep going until we're basically right next to it
				if (Transform.Origin.DistanceTo(_newPositionState.Origin) < 0.01f)
				{
					// state.Transform = _newPositionState; // Snaps the final microscopic distance - i don't like this becuase it makes it look like jitter is happening, within 0.05m of the host is close enough
					_applyNewPositionState = false;      // NOW we stop lerping
				}
			}
			// apply the velocity state if needed
			// don't really need to lerp velocity since it doesn't change a significant enough amount i think
			if (_applyNewVelocityState)
			{
				Velocity = (Vector3)State[2];
				_applyNewVelocityState = false;
			}
		}

		// ALWAYS apply rotation sync so that clients always know where someone is looking (since we want that even when they're sitting)
		// apply the updated/corrected rotation state if needed
		if (_applyNewRotationState)
		{
			// interpolate to the new rotation (this was written by gemini but it's prolly fine)
			Quaternion currentRot = Transform.Basis.GetRotationQuaternion();
			Quaternion targetRot = _newRotationState.GetRotationQuaternion(); // Assuming _newRotationState is a Basis
			
			// Slerp (Spherical Linear Interpolation) calculates the smooth rotation
			Quaternion smoothRot = currentRot.Slerp(targetRot, weight);
			
			Vector3 currentPosition = Transform.Origin;
			Transform = new Transform3D(new Basis(smoothRot), currentPosition);
			
			
			// Only turn off the flag once the rotational difference is tiny
			if (Mathf.Abs(currentRot.AngleTo(targetRot)) < 0.01f)
			{
				_applyNewRotationState = false;
			}
		}
	}
}
