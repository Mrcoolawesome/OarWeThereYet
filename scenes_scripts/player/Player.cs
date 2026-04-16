using System;
using Godot;
using Godot.Collections;
using Waterways;

public partial class Player : CharacterBody3D, ISyncBuffer
{
	// Exported variables
	[ExportGroup("Movement Speed Settings")]
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float WalkingSpeed = 5.0f;
	[Export] public float SwimmingSpeed = 1.0f;
	[Export] public float SprintSpeed = 8.0f;
	[Export] public float CrouchingSpeed = 3.0f;
	[Export] public float AirSpeed = 3.0f;
	[Export] public float MouseSens = 0.4f;

	[ExportGroup("Knockback Force Settings")]
	[Export] public float PlayerKnockbackForce = 10.0f; // this is applied as a velocity, not actually a force
	[Export] public float ObjectKnockbackForce = 20.0f; // this is actually applied as a force

	[ExportGroup("Network Lerp Settings")]
	[Export] public float LerpSpeed = 10.0f;
	[Export] public float CrouchLerpSpeed = 10.0f;
	[Export] public float NetworkLerpSpeed = 10.0f;
	[Export] public Array<Variant> State { get; set; }

	[ExportGroup("Water Physics Settings")]
	[Export] public float Mass = 1.0f;
	[Export] public float FloatForce = 1.0f;
	[Export] public float RiverSpeed = 250.0f;
	[Export] public float WaterDrag = 2.0f;

	[ExportGroup("Head talking animation setting")]
  [Export] public float VoiceScaleMultiplier = 15.0f;

	[ExportGroup("Networked Audio Gates")]
	[Export] public bool WalkingOnBoatAudioGate { get; set; } = false;
	[Export] public bool WalkingOnGroundAudioGate { get; set; } = false;
	[Export] public bool TreadingWaterAudioGate { get; set; } = false;
	[Export] public bool PlayerHitSwooshAudioGate { get; set; } = false;
	[Export] public int PlayerHitSomethingAudioTrigger { get; set; } = 0;
	[Export] public int PlayerHitBoatAudioTrigger { get; set; } = 0;
	[Export] public float MovementAudioPitchScale { get; set; } = 1.0f;
	[Export] public bool IsUnderWater { get; set; } = false;
	[Export] public float UnderWaterSubmergedOffset = 0.3f;

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
	private PauseUi _pauseUI;
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
		EndGame
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
	// water physics node
	private WaterPhysics _waterPhysics;
	// probe container
	private Node3D _probeContainer;
	// river node
	private RiverFloatSystem _riverFloatSystem;
	// variables to help apply water physics force
	private bool _applyWaterPhysicsForce = false;
	[Export] public bool IsSwimming { get; private set; } = false;
	private Vector3 _waterPhysicsForce;
	private Vector3 _waterPhysicsForcePosition;

	// variable to check if we want to get knocked back or not
	private bool _applyKnockback = false;
	private Vector3 _knockbackDirection = Vector3.Zero;
  private Vector3 _knockbackVelocity = Vector3.Zero;

	// get the arm node
	public ArmNode ArmNode;

	// Get the terrain
	private Node3D _terrain;
	private GodotObject _terrainData;

	// get the animation player and current animation tracker
	private AnimationPlayer _animationPlayer;
	// Animation tracking to prevent network spam
  private string _currentAnim = "idleStanding";

	// Animation tracking
  private double _crouchStillTimer = 0.0;
  private RayCast3D _groundDetectionRay;
  // Store the Steam Username
	private Label3D _gamerTag;
	public String SteamUsername;

	// character meshes meshes
	private MeshInstance3D _headMesh;
	private MeshInstance3D _bodyMesh;
	private MeshInstance3D _eyesWhitesLeft;
	private MeshInstance3D _eyesWhitesRight;
	private MeshInstance3D _pupilEyeRight;
	private MeshInstance3D _pupilEyeLeft;

	// You MUST add this variable to your MultiplayerSynchronizer!
	[ExportGroup("DO NOT TOUCH")]
  [Export] public string CurrentColorHex = ""; 
  // Used locally by puppets to know when the authority changed the color
  private string _lastAppliedColor = "";

	// loudness caling variables
  public float _targetHeadScale = 1.0f;
  private float _currentHeadScale = 1.0f;

	// END GAME UI
	private Control _endGameUi;

	// audio players
	private AudioStreamPlayer3D _jumpAudio;
	private AudioStreamPlayer3D _walkingOnBoatAudio;
	private AudioStreamPlayer3D _walkingOnGroundAudio;
	private AudioStreamPlayer3D _treadingWaterAudio;
	private AudioStreamPlayer _endGameMusic;
	private AudioStreamPlayer _underwater;
	private AudioStreamPlayer3D _playerHitSwoosh;
	private AudioStreamPlayer3D _playerHitPlayer;
	private AudioStreamPlayer3D _playerHitBoat;

	// under water view
	private Control _underWaterPOV;
	private Camera3D _playerCamera;

	// underwater audio state tracking
	private bool _wasUnderWater = false;
	private AudioEffectReverb _voiceChatReverb;
	private AudioEffectPitchShift _voiceChatPitch;
	private AudioEffectReverb _micInputReverb;
	private AudioEffectPitchShift _micInputPitch;
	private double _playerHitSwooshGateTimer = 0.0;
	private int _lastPlayerHitSomethingAudioTrigger = 0;
	private int _lastPlayerHitBoatAudioTrigger = 0;

	// Oar animation intent handshake state.
	private bool _hasPendingOarAnimationIntent = false;
	private int _requestedOarAnimationSeat = -1;
	private int _requestedOarAnimationDirection = 1;
	private bool _requestedOarAnimationStartStop = false;

	// Seat sit/unsit intent handshake state.
	private bool _hasPendingSeatIntent = false;
	private int _requestedSeatIndex = -1;
	private bool _requestedSeatIsSitting = false;

	// Patch intent handshake state.
	private bool _hasPendingPatchIntent = false;
	private Hole _pendingPatchHole = null;

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
		_pauseUI = GetNode<PauseUi>("PauseCanvas/PauseUI");
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

		// get the water physics node and set its parameters
		// get the probe container and water physics nodes
		_probeContainer = GetNode<Node3D>("ProbeContainer");
    _waterPhysics = GetNode<WaterPhysics>("WaterPhysics");
		_riverFloatSystem = GetParent().GetNode<RiverFloatSystem>("RiverManager/RiverFloatSystem");
		_waterPhysics.SetParameters(_riverFloatSystem, FloatForce, RiverSpeed, WaterDrag);
		
		// Get terrain
		_terrain = GetNode<Node3D>("../Terrain3D");
		_terrainData = _terrain.Get("data").AsGodotObject();

		// get the armnode
		ArmNode = GetNode<ArmNode>("Head/ArmNode");

		// get the meshes
		_bodyMesh = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/body");
		_headMesh = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/head");
		_eyesWhitesLeft = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/eyeBallLeft");
		_eyesWhitesRight = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/eyeBallRight");
		_pupilEyeLeft = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/pupilLeft");
		_pupilEyeRight = GetNode<MeshInstance3D>("FullPlayerModel/Armature/Skeleton3D/pupilRight");

		// this is for seeing how far we are from the ground
		_groundDetectionRay = GetNode<RayCast3D>("GroundDetectionRay");

		_endGameUi = GetNode<Control>("EndScreen");
    _endGameUi.Visible = false;

		// set the audio
		_jumpAudio = GetNode<AudioStreamPlayer3D>("AudioStuff/Jump");
		_walkingOnBoatAudio = GetNode<AudioStreamPlayer3D>("AudioStuff/BoatWalking");
		_walkingOnGroundAudio = GetNode<AudioStreamPlayer3D>("AudioStuff/WorldWalkingSingle");
		_treadingWaterAudio = GetNode<AudioStreamPlayer3D>("AudioStuff/TreadingWater");
		_endGameMusic = GetNode<AudioStreamPlayer>("AudioStuff/Endgame");
		_underwater = GetNode<AudioStreamPlayer>("AudioStuff/Underwater");
		_playerHitSwoosh = GetNode<AudioStreamPlayer3D>("AudioStuff/PlayerHitSwooshShortened");
		_playerHitPlayer = GetNode<AudioStreamPlayer3D>("AudioStuff/BellHitSound");
		_playerHitBoat = GetNode<AudioStreamPlayer3D>("AudioStuff/HitBoatSound");

		// subscribe to the global signal server call to respawn the player to the boat
		GlobalSignalServer.Instance.RespawnPlayer += OnPauseUIRespawnPlayer;
		// subscribe to the signal that changes the mouse sensitivity from the settings menu
		GlobalSignalServer.Instance.ApplyPlayerLookSpeed += ChangePlayerLookSpeed;
		GlobalSignalServer.Instance.ApplyPlayerFov += ChangePlayerFov;
		// subscribe to setting the gamertag
		GlobalSignalServer.Instance.AssignGamertag += SetUsername;
		// subscribe to change colors
		GlobalSignalServer.Instance.AssignPlayerColor += SetPlayerColor;
		// Subscribe to mic loudness
    GlobalSignalServer.Instance.PlayerLoudness += OnPlayerLoudness;
    // NEW: Subscribe to End Game signal
    GlobalSignalServer.Instance.EndGame += OnEndGameTriggered;

		// Get the camera reference
		_playerCamera = _head.GetNodeOrNull<Camera3D>("CameraContainer/Camera3D"); 
		ApplySavedLocalSettings();

		// get the animation player
		_animationPlayer = GetNode<AnimationPlayer>("FullPlayerModel/AnimationPlayer");

		// Add the player to the 'players' group
		AddToGroup("players");

		// set the state array from the server's perspective
		SetStateArray();

		// set their gamertag
		_gamerTag = GetNode<Label3D>("GamerTag");

		// get the underwater pov
		_underWaterPOV = GetNode<Control>("UnderWaterPOV");
		_underWaterPOV.Visible = false; // make the underwater pov invisible by default

		// client code for when setting up their camera and stuff
		// if we are the player, then use the camera for this player
		// IsMultiplayerAuthority checks if the current client is the multiplayer authority of THIS current NODE 
		if (IsMultiplayerAuthority())
		{
			// Spawn sitting in next available seat (only the authority should trigger this)
			RequestSitInSeat(-1);

			// Enable our camera
			if (_playerCamera != null)
			{
				_playerCamera.Current = true;
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
			if (_playerCamera != null)
			{
				_playerCamera.QueueFree(); 
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
		// If we are looking at someone else, and their synced color just arrived over the network:
    if (!IsMultiplayerAuthority() && CurrentColorHex != _lastAppliedColor && CurrentColorHex != "")
    {
      ApplyMaterialColor(CurrentColorHex);
      _lastAppliedColor = CurrentColorHex;
    }

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

		// Always process underwater state regardless of game state
		UnderWaterStateProcess();

		switch(CurrPlayerState)
		{
			case PlayerState.Standing:
				_interactRay.Enabled = CurrGameState == GameState.Playing;
				break;
			case PlayerState.Rowing:
				_interactRay.Enabled = false;
				RowingStateProcess();
				break;
		}

		// If player falls below terrain, teleport them back up
		if (_terrainData != null)
		{
			float terrainHeight = _terrainData.Call("get_height", GlobalPosition).AsSingle();
			if (GlobalPosition.Y - terrainHeight < -2.0f || GlobalPosition.Y <= -110)
			{
				RequestSitInSeat(-1);
			}
		}
  }

	private void PlayingStateProcess()
	{
		_pauseUICanvas.Visible = false;
		_hud.Visible = true;

		// Only keep the cursor captured while the game window is focused.
		if (GetWindow() != null && GetWindow().HasFocus())
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		else
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	private void MenuStateProcess()
	{
		if (!_invUI.isOpen()) { _pauseUICanvas.Visible = true; };
		_hud.Visible = false;
		
		if (_underWaterPOV != null)
		{
			_underWaterPOV.Visible = false;
		}
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void UnderWaterStateProcess()
	{
		if (_riverFloatSystem == null || _underWaterPOV == null)
		{
			return;
		}

		if (_playerCamera == null)
		{
			IsUnderWater = false;
			_underWaterPOV.Visible = false;
			return;
		}

		float waterHeight = _riverFloatSystem.GetWaterHeight(_playerCamera.GlobalPosition);
		IsUnderWater = _playerCamera.GlobalPosition.Y < waterHeight + UnderWaterSubmergedOffset;
		_underWaterPOV.Visible = IsUnderWater;

		// Handle audio effects when transitioning underwater
		if (IsUnderWater && !_wasUnderWater)
		{
			ApplyUnderWaterAudioEffects();
		}
		else if (!IsUnderWater && _wasUnderWater)
		{
			RemoveUnderWaterAudioEffects();
		}

		_wasUnderWater = IsUnderWater;
	}

	private void ApplyUnderWaterAudioEffects()
	{
		// Play the underwater ambient sound
		if (_underwater != null && !_underwater.Playing)
		{
			_underwater.Play();
		}

		// Mute the environment audio bus
		int envBusIdx = AudioServer.GetBusIndex("Environment");
		if (envBusIdx >= 0)
		{
			AudioServer.SetBusMute(envBusIdx, true);
		}

		// Apply reverb and pitch effects to Voice Chat bus
		int voiceChatBusIdx = AudioServer.GetBusIndex("Voice Chat");
		if (voiceChatBusIdx >= 0)
		{
			// Create and apply reverb effect
			if (_voiceChatReverb == null)
			{
        _voiceChatReverb = new AudioEffectReverb
        {
          RoomSize = 0.5f,
          Damping = 0.7f,
          Wet = 0.15f,
          Dry = 1.0f
        };
      }
			AudioServer.AddBusEffect(voiceChatBusIdx, _voiceChatReverb);

			// Create and apply pitch shift effect
			if (_voiceChatPitch == null)
			{
        _voiceChatPitch = new AudioEffectPitchShift
        {
          PitchScale = 0.8f // Lower pitch by 20%
        };
      }
			AudioServer.AddBusEffect(voiceChatBusIdx, _voiceChatPitch);
		}

		// Apply reverb and pitch effects to MicInput bus
		int micInputBusIdx = AudioServer.GetBusIndex("MicInput");
		if (micInputBusIdx >= 0)
		{
			// Create and apply reverb effect
			if (_micInputReverb == null)
			{
        _micInputReverb = new AudioEffectReverb
        {
          RoomSize = 0.5f,
          Damping = 0.7f,
          Wet = 0.15f,
          Dry = 1.0f
        };
      }
			AudioServer.AddBusEffect(micInputBusIdx, _micInputReverb);

			// Create and apply pitch shift effect
			if (_micInputPitch == null)
			{
        _micInputPitch = new AudioEffectPitchShift
        {
          PitchScale = 0.8f // Lower pitch by 20%
        };
      }
			AudioServer.AddBusEffect(micInputBusIdx, _micInputPitch);
		}
	}

	private void RemoveUnderWaterAudioEffects()
	{
		// Stop the underwater ambient sound
		if (_underwater != null)
		{
			_underwater.Stop();
		}

		// Unmute the environment audio bus
		int envBusIdx = AudioServer.GetBusIndex("Environment");
		if (envBusIdx >= 0)
		{
			AudioServer.SetBusMute(envBusIdx, false);
		}

		// Remove reverb and pitch effects from Voice Chat bus
		int voiceChatBusIdx = AudioServer.GetBusIndex("Voice Chat");
		if (voiceChatBusIdx >= 0)
		{
			// Remove reverb effect
			if (_voiceChatReverb != null)
			{
				for (int i = 0; i < AudioServer.GetBusEffectCount(voiceChatBusIdx); i++)
				{
					if (AudioServer.GetBusEffect(voiceChatBusIdx, i) == _voiceChatReverb)
					{
						AudioServer.RemoveBusEffect(voiceChatBusIdx, i);
						break;
					}
				}
			}

			// Remove pitch effect
			if (_voiceChatPitch != null)
			{
				for (int i = 0; i < AudioServer.GetBusEffectCount(voiceChatBusIdx); i++)
				{
					if (AudioServer.GetBusEffect(voiceChatBusIdx, i) == _voiceChatPitch)
					{
						AudioServer.RemoveBusEffect(voiceChatBusIdx, i);
						break;
					}
				}
			}
		}

		// Remove reverb and pitch effects from MicInput bus
		int micInputBusIdx = AudioServer.GetBusIndex("MicInput");
		if (micInputBusIdx >= 0)
		{
			// Remove reverb effect
			if (_micInputReverb != null)
			{
				for (int i = 0; i < AudioServer.GetBusEffectCount(micInputBusIdx); i++)
				{
					if (AudioServer.GetBusEffect(micInputBusIdx, i) == _micInputReverb)
					{
						AudioServer.RemoveBusEffect(micInputBusIdx, i);
						break;
					}
				}
			}

			// Remove pitch effect
			if (_micInputPitch != null)
			{
				for (int i = 0; i < AudioServer.GetBusEffectCount(micInputBusIdx); i++)
				{
					if (AudioServer.GetBusEffect(micInputBusIdx, i) == _micInputPitch)
					{
						AudioServer.RemoveBusEffect(micInputBusIdx, i);
						break;
					}
				}
			}
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
			// Broadcast stop rowing. The first boolean is all that matters to make them stop rowing
			RequestRowing((int)_seat, false, false);

			// Reset their global position
			GlobalPosition = GetCurrentSeat().GlobalPosition + new Vector3(0, 1, 0); 

			UpdateSeatIntent((int)_seat, false);
			UpdateOarAnimationIntent((int)_seat, 1, false);

			return; // STOP after this we don't wanna take anymore input as if we're sitting
		}

		if (ArmNode?.Item?.Data?.UseAction is not Oar)
		{
			UpdateOarAnimationIntent((int)_seat, 1, false);
			return;
		}

		int oarDirection = 1;
		bool shouldAnimateOar = false;

		// (Boat.SeatIndicies seat, bool stopStart, bool backForward)
		if (Input.IsActionPressed("move_forward"))
		{
			// Broadcast move forward
			// have to send the seat as an int because that's a supported variant type: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_variant.html#c-sharp-variant-compatible-types
			RequestRowing((int)_seat, true, true);
			oarDirection = 1;
			shouldAnimateOar = true;
		} 
		// If user pressing forward and backward they'll go forward
		else if (Input.IsActionPressed("move_backward"))
		{
			// Broadcast move backward
			RequestRowing((int)_seat, true, false);
			oarDirection = -1;
			shouldAnimateOar = true;
		}

		UpdateOarAnimationIntent((int)_seat, oarDirection, shouldAnimateOar);

		// Emit a signal when they're done rowing
		// Setting the direction doesn't matter in this case
		if (Input.IsActionJustReleased("move_forward"))
		{
			RequestRowing((int)_seat, false, true);
		} 
		else if (Input.IsActionJustReleased("move_backward"))
		{
			RequestRowing((int)_seat, false, false); 
		}
	}

  //PHYSICS PROCESS CODE AND ALL ASSOCIATED FUNCTIONS
	// Logic for movement depending on player state
	public override void _PhysicsProcess(double delta)
	{
		if (IsMultiplayerAuthority())
		{
			if (_hasPendingSeatIntent)
			{
				RequestSeatStateConfirmation(_requestedSeatIndex, _requestedSeatIsSitting);
			}

			if (_hasPendingOarAnimationIntent)
			{
				RequestOarAnimationConfirmation(_requestedOarAnimationSeat, _requestedOarAnimationDirection, _requestedOarAnimationStartStop);
			}

			if (_hasPendingPatchIntent)
			{
				if (GodotObject.IsInstanceValid(_pendingPatchHole))
				{
					_pendingPatchHole.Rpc(nameof(Hole.RequestPatchConfirmation));
				}
				else
				{
					// Hole was removed/disposed, clear pending state
					_hasPendingPatchIntent = false;
					_pendingPatchHole = null;
				}
			}
		}

		if (IsMultiplayerAuthority() && PlayerHitSwooshAudioGate)
		{
			_playerHitSwooshGateTimer -= delta;
			if (_playerHitSwooshGateTimer <= 0.0)
			{
				PlayerHitSwooshAudioGate = false;
			}
		}

		// Capture the stable state for this whole frame
		IsSwimming = _applyWaterPhysicsForce;

		// this is done on the physics process because this isn't disabled for the puppets unlike the regular process function
		HeadAnimationPhysicsProcess(delta);

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

			if (CurrPlayerState == PlayerState.Standing)
			{
				StandingStatePhysicsProcess(delta);
				CrouchSprintPhysicsProcess(delta);
				FloatingPhysicsProcess(delta);
				ApplyKnockbackPhysicsProcess(delta);
			} 
			else if (CurrPlayerState == PlayerState.Rowing)
			{
				RowingStatePhysicsProcess();
			}

			// Always apply MoveAndSlide unless they're rowing
			if (CurrPlayerState != PlayerState.Rowing)
			{
				MoveAndSlide();
			}

			// do their audio stuff
			HandleAudioPhysicsProcess();
			ApplyAudioFromGates();
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

			// Apply replicated audio state from synchronized gate booleans.
			ApplyAudioFromGates();
		}

		// Reset for the NEXT frame's potential signal
		_applyWaterPhysicsForce = false;
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
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor() && CurrGameState == GameState.Playing)
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

		if (CurrGameState == GameState.Menu)
		{
			targetDirection = Vector3.Zero;
		}

		// Frame when player leaves ground
		if (!IsOnFloor() && _isOnGround)
		{
			_initialVelocity = GetPlatformVelocity() + velocity;
		}

		if (IsOnFloor() || _applyWaterPhysicsForce)
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

		// do the animation stuff
		AnimationManager(velocity, inputDir, delta);

		// Handle mouse input while standing
		PlayerRotation();

		_isOnGround = IsOnFloor();

		Velocity = velocity;
	}

	private void HeadAnimationPhysicsProcess(double delta)
  {
    // Smoothly interpolate the scale towards the target
    _currentHeadScale = Mathf.Lerp(_currentHeadScale, _targetHeadScale, (float)delta * 20.0f);
    
    // Create the XZ scale vector (Y remains 1.0 so they don't get taller)
    Vector3 newScale = new Vector3(_currentHeadScale, 1.0f, _currentHeadScale);
    
    // Apply to the meshes
    if (_headMesh != null) _headMesh.Scale = newScale;
    if (_eyesWhitesLeft != null) _eyesWhitesLeft.Scale = newScale;
    if (_eyesWhitesRight != null) _eyesWhitesRight.Scale = newScale;
    if (_pupilEyeLeft != null) _pupilEyeLeft.Scale = newScale;
    if (_pupilEyeRight != null) _pupilEyeRight.Scale = newScale;

    // ONLY the Authority should automatically decay the target back to 1.0.
    // The puppets (other clients) will just receive the target scale perfectly from the Synchronizer.
		_targetHeadScale = Mathf.Lerp(_targetHeadScale, 1.0f, (float)delta * 10.0f);
  }

	private void HandleAudioPhysicsProcess()
  {
		// Compute synchronized gate booleans for this frame.
    Vector2 inputDir = Input.GetVector("left", "right", "move_forward", "move_backward");
    bool isTryingToMove = inputDir.LengthSquared() > 0.01f;
		TreadingWaterAudioGate = false;
		WalkingOnBoatAudioGate = false;
		WalkingOnGroundAudioGate = false;
		MovementAudioPitchScale = 1.0f;

    // Check swimming
    if (_currentAnim == "swimming")
    {
			TreadingWaterAudioGate = true;
    }
    // Check walking on ground/boat
    else if (IsOnFloor() && isTryingToMove && CurrPlayerState == PlayerState.Standing)
    {
			bool isCrouchWalking = _currentAnim == "crouchWalking" || _currentAnim == "crouchWalkingBackward";
			MovementAudioPitchScale = isCrouchWalking ? 2.0f : 1.0f;

      bool isOnBoat = false;
      
      // Check the floor raycast
      if (_groundDetectionRay != null && _groundDetectionRay.IsColliding())
      {
        GodotObject collider = _groundDetectionRay.GetCollider();
        if (collider is Node colliderNode && (_boat == colliderNode || _boat.IsAncestorOf(colliderNode)))
        {
          isOnBoat = true;
        }
      }

      // Apply the speed and play the correct audio
      if (isOnBoat)
      {
        WalkingOnBoatAudioGate = true;
      }
      else
      {
        WalkingOnGroundAudioGate = true;
      }
    }
  }

	private void ApplyAudioFromGates()
	{
		float audioSpeed = Mathf.Max(0.01f, MovementAudioPitchScale);

		if (PlayerHitSwooshAudioGate)
		{
			if (!_playerHitSwoosh.Playing) _playerHitSwoosh.Play();
		}

		if (PlayerHitSomethingAudioTrigger != _lastPlayerHitSomethingAudioTrigger)
		{
			_playerHitPlayer.Stop();
			_playerHitPlayer.Play();
			_lastPlayerHitSomethingAudioTrigger = PlayerHitSomethingAudioTrigger;
		}

		if (PlayerHitBoatAudioTrigger != _lastPlayerHitBoatAudioTrigger)
		{
			_playerHitBoat.Stop();
			_playerHitBoat.Play();
			_lastPlayerHitBoatAudioTrigger = PlayerHitBoatAudioTrigger;
		}

		if (TreadingWaterAudioGate)
		{
			if (!_treadingWaterAudio.Playing) _treadingWaterAudio.Play();
		}
		else
		{
			_treadingWaterAudio.Stop();
		}

		if (WalkingOnBoatAudioGate)
		{
			_walkingOnBoatAudio.PitchScale = audioSpeed;
			if (!_walkingOnBoatAudio.Playing) _walkingOnBoatAudio.Play();
		}
		else
		{
			_walkingOnBoatAudio.Stop();
		}

		if (WalkingOnGroundAudioGate)
		{
			_walkingOnGroundAudio.PitchScale = audioSpeed;
			if (!_walkingOnGroundAudio.Playing) _walkingOnGroundAudio.Play();
		}
		else
		{
			_walkingOnGroundAudio.Stop();
		}
  }

	private void AnimationManager(Vector3 velocity, Vector2 inputDir, double delta)
  {
    string targetAnim = "idleStanding";

    // 1. SWIMMING LOGIC
    // We check this first so it overrides falling if they are in the water
    if (_applyWaterPhysicsForce)
    {
      targetAnim = "swimming";
      _crouchStillTimer = 0.0;
    }
    // 2. JUMPING AND FALLING LOGIC
    else if (!IsOnFloor())
    {
      _crouchStillTimer = 0.0; // Reset crouch timer if we're in the air

      if (velocity.Y > 0)
      {
        targetAnim = "initalJump";
      }
      else
      {
        // We are falling. Check if we are about to hit the ground.
        if (_groundDetectionRay != null && _groundDetectionRay.IsColliding())
        {
          targetAnim = "landingJump";
        }
        else
        {
          targetAnim = "falling";
        }
      }
    }
    // 3. GROUNDED LOGIC
    else
    {
      bool isCrouching = Input.IsActionPressed("crouch");
      
      // Use LengthSquared to account for controller stick drift/deadzones.
      if (inputDir.LengthSquared() < 0.01f)
      {
        if (isCrouching)
        {
          _crouchStillTimer += delta;
          
          if (_crouchStillTimer >= 5.0) 
          {
            targetAnim = "bouncingOnIt";
          }
          else
          {
            targetAnim = "crouchingStill";
          }
        }
        else
        {
          _crouchStillTimer = 0.0;
          targetAnim = "idleStanding";
        }
      }
      // They are moving on the ground
      else
      {
        _crouchStillTimer = 0.0; // Reset timer because they moved
        
        bool isMovingBackwards = inputDir.Y > 0.1f;

        if (isCrouching)
        {
          targetAnim = isMovingBackwards ? "crouchWalkingBackward" : "crouchWalking";
        }
        else
        {
          // Regular walking now uses 'kneesWalk' for BOTH forward and backward!
          targetAnim = "kneesWalk"; 
        }
      }
    }

    // ONLY fire the network RPC if the state actually changed!
    if (_currentAnim != targetAnim)
    {
      _currentAnim = targetAnim;
      Rpc(nameof(SyncPlayerAnimation), targetAnim);
    }
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
		GlobalPosition = seatCollision.GlobalPosition + new Vector3(0, 0.5f, 0);

    // Handle mouse input while sitting
    PlayerRotation();

    // Trigger the sitting animation!
    string targetAnim = "sittingLegsKicking";
    if (_currentAnim != targetAnim)
    {
      _currentAnim = targetAnim;
      Rpc(nameof(SyncPlayerAnimation), targetAnim);
    }
  }

	private void FloatingPhysicsProcess(double delta)
	{
		if (_applyWaterPhysicsForce)
		{
			// set the movement speed
			_currSpeed = SwimmingSpeed;
			// Calculate acceleration: a = F/m
			Vector3 waterAcceleration = _waterPhysicsForce / Mass;
			// Add the acceleration to the velocity over time
			Velocity += waterAcceleration * (float)delta;
			// set _applyWaterPhysicsForce back to false
			_applyWaterPhysicsForce = false;
		} else
		{
			// otherwise set the speed to the regular walkign speed
			_currSpeed = WalkingSpeed;
		}
	}

	// function that's called from the water physics node's signal
	private void QueueApplyWaterPhysicsForce(Vector3 force, Vector3 relativePosition)
	{
		// set the apply water physics force boolean to be true so that it can be applied in PhysicsProcess
		_applyWaterPhysicsForce = true;

		// then set the global force and forcePosition variables so that they can be seen by PhysicsProcess
		// Filter out the Y-axis force immediately
    _waterPhysicsForce = force;
		_waterPhysicsForcePosition = relativePosition;
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

	private void OnPauseUIRespawnPlayer(int multiplayerID)
	{
		if (Name == multiplayerID.ToString())
		{
			// If they're in a seat, reset the seat
			if (CurrPlayerState == PlayerState.Rowing)
			{
				// Broadcast stop rowing. The first boolean is all that matters to make them stop rowing
				RequestRowing((int)_seat, false, false);
				UpdateSeatIntent((int)_seat, false);
				UpdateOarAnimationIntent((int)_seat, 1, false);
			}

			// set their position to be the position of the boat but just a little higher so they're not just clipping into it
			// this shouldn't need to be an rpc call i think because the multiplayer synchronzier should just handle it
			RequestSitInSeat(-1);

			// put them into the playing state after that so the pause ui goes away
			CurrGameState = GameState.Playing;
		}
	}

	//RPC Functions
	public void RequestSitInSeat(int seat)
	{
		UpdateSeatIntent(seat, true);
	}

	// Makes sure that PlayerState changes is synced for everyone
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetSitStandState(bool isSitting, int seatIdx)
	{
		// Broadcast occupied seat
		_boat.OccupiedSeats[seatIdx] = isSitting;
		if (ArmNode?.Item?.Data?.UseAction == null)
		{
			_boat.HasOarInSeat[seatIdx] = false;
		}
		else if (ArmNode.Item.Data.UseAction is Oar)
    {
			_boat.HasOarInSeat[seatIdx] = isSitting;
    }

		// set the rowing state
		CurrPlayerState = isSitting ? PlayerState.Rowing : PlayerState.Standing;
		_seat = (Boat.SeatIndicies)seatIdx;

		// If we wipe it when standing up, getting knocked out of the boat deletes the hit!
    if (isSitting)
    {
      _knockbackVelocity = Vector3.Zero;
      _applyKnockback = false;
    }
	}

	// Wrapper for ServerRequestRowing RPC function
	public void RequestRowing(int seatIdx, bool stopStart, bool backForward)
	{
		RpcId(1, MethodName.ServerRequestRowing, seatIdx, stopStart, backForward);
	}

	public void RequestSeatStateConfirmation(int seatIdx, bool isSitting)
	{
		if (Multiplayer.IsServer())
		{
			ServerRequestSeatState(seatIdx, isSitting);
			return;
		}

		RpcId(1, nameof(ServerRequestSeatState), seatIdx, isSitting);
	}

	public void RequestOarAnimationConfirmation(int seatIdx, int direction, bool startStop)
	{
		if (Multiplayer.IsServer())
		{
			Rpc(nameof(ConfirmOarAnimationState), seatIdx, direction, startStop);
			return;
		}

		RpcId(1, nameof(ServerRequestOarAnimationState), seatIdx, direction, startStop);
	}

	private void UpdateSeatIntent(int seatIdx, bool isSitting)
	{
		bool requestedStateChanged = _requestedSeatIndex != seatIdx || _requestedSeatIsSitting != isSitting;
		if (requestedStateChanged)
		{
			_requestedSeatIndex = seatIdx;
			_requestedSeatIsSitting = isSitting;
			_hasPendingSeatIntent = true;
		}

		if (_hasPendingSeatIntent)
		{
			RequestSeatStateConfirmation(_requestedSeatIndex, _requestedSeatIsSitting);
		}
	}

	private void UpdateOarAnimationIntent(int seatIdx, int direction, bool startStop)
	{
		bool requestedStateChanged = _requestedOarAnimationSeat != seatIdx
			|| _requestedOarAnimationDirection != direction
			|| _requestedOarAnimationStartStop != startStop;

		if (requestedStateChanged)
		{
			_requestedOarAnimationSeat = seatIdx;
			_requestedOarAnimationDirection = direction;
			_requestedOarAnimationStartStop = startStop;
			_hasPendingOarAnimationIntent = true;
		}

		if (_hasPendingOarAnimationIntent)
		{
			RequestOarAnimationConfirmation(_requestedOarAnimationSeat, _requestedOarAnimationDirection, _requestedOarAnimationStartStop);
		}
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void ServerRequestSeatState(int seat, bool isSitting)
	{
		if (!Multiplayer.IsServer()) return;

		if (isSitting)
		{
			if (CurrPlayerState == PlayerState.Rowing)
			{
				// Idempotent ack: if this player is already seated, reuse that seat.
				Rpc(nameof(ConfirmSeatState), (int)_seat, true);
				return;
			}

			int chosenSeat = seat;
			if (chosenSeat == -1)
			{
				chosenSeat = _boat.NextAvailableSeat();
			}

			if (chosenSeat < 0) return;
			if (!_boat.IsSeatAvailable(chosenSeat)) return;

			Rpc(nameof(ConfirmSeatState), chosenSeat, true);
			return;
		}

		int unsitSeat = seat == -1 ? (int)_seat : seat;
		Rpc(nameof(ConfirmSeatState), unsitSeat, false);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ConfirmSeatState(int seat, bool isSitting)
	{
		if (isSitting && CurrPlayerState == PlayerState.Rowing && (int)_seat != seat)
		{
			// Prevent stale occupancy if seat changed due network timing.
			SetSitStandState(false, (int)_seat);
		}

		if (_hasPendingSeatIntent)
		{
			bool seatMatches = _requestedSeatIndex == seat || (_requestedSeatIsSitting && _requestedSeatIndex == -1);
			if (_requestedSeatIsSitting == isSitting && seatMatches)
			{
				if (_requestedSeatIsSitting && _requestedSeatIndex == -1)
				{
					_requestedSeatIndex = seat;
				}

				_hasPendingSeatIntent = false;
			}
		}

		SetSitStandState(isSitting, seat);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void ServerRequestOarAnimationState(int seat, int direction, bool startStop)
	{
		if (!Multiplayer.IsServer()) return;

		Rpc(nameof(ConfirmOarAnimationState), seat, direction, startStop);
	}

	// The animation only plays after the host confirms and rebroadcasts the state.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ConfirmOarAnimationState(int seat, int direction, bool startStop)
	{
		if (_hasPendingOarAnimationIntent
			&& _requestedOarAnimationSeat == seat
			&& _requestedOarAnimationDirection == direction
			&& _requestedOarAnimationStartStop == startStop)
		{
			_hasPendingOarAnimationIntent = false;
		}

		GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.AnimateOar), seat, direction, startStop);
	}

	/// <summary>
	/// Request to patch a hole. Client will resend until server confirms removal.
	/// </summary>
	public void RequestPatch(Hole hole)
	{
		if (hole == null) return;

		_hasPendingPatchIntent = true;
		_pendingPatchHole = hole;
		if (Multiplayer.IsServer())
		{
			hole.RequestPatchConfirmation();
		}
		else
		{
			hole.Rpc(nameof(Hole.RequestPatchConfirmation));
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void OnPatchConfirmed()
	{
		_hasPendingPatchIntent = false;
		_pendingPatchHole = null;
	}

	public void Reset()
	{
		// Only the server should issue this command
		if (Multiplayer.IsServer())
		{
			// Tell EVERYONE (including the server) to run the SyncReset function
			Rpc(nameof(SyncReset));

			// stop the rowing animation too
			Rpc(nameof(ConfirmOarAnimationState), (int)_seat, 1, false);

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

	public void OpenInventory(Inventory inventory)
	{
		CurrGameState = GameState.Menu;
		_invUI.Open(inventory);
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

	// --- OAR HELPER FUNCTIONS FOR HITTING PEOPLE AND OBJECTS ---

  public GodotObject GetRaycastObject()
  {
    return _interactRay.GetCollider();
  }

	public void TriggerPlayerHitSwoosh()
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		PlayerHitSwooshAudioGate = true;
		_playerHitSwooshGateTimer = 0.15;
	}

	public void TriggerPlayerHitSomething()
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		PlayerHitSomethingAudioTrigger++;
	}

	public void TriggerPlayerHitBoat()
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		PlayerHitBoatAudioTrigger++;
	}

  // 1. Ask the Server to relay the command AND the direction
  public void ApplyKnockbackOnClient(string clientName, Vector3 pushDirection)
  {
    int targetId = clientName.ToInt(); 
    RpcId(1, nameof(ServerRelayKnockback), targetId, pushDirection);
  }

  // 2. The Server forwards the direction to the specific client
  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void ServerRelayKnockback(int targetClientId, Vector3 pushDirection)
  {
    if (Multiplayer.IsServer())
    {
			RpcId(targetClientId, nameof(BroadcastApplyKnockback), pushDirection);
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void BroadcastApplyKnockback(Vector3 pushDirection)
  {
    // If they are sitting in the boat, forcibly eject them!
    if (CurrPlayerState == PlayerState.Rowing)
    {
      // 1. Tell the server to stop the rowing physics for this seat
      RequestRowing((int)_seat, false, false);

			// 2. Request unsit through host-confirmed handshake.
			UpdateSeatIntent((int)_seat, false);
      
			// 3. Teleport them slightly up so they don't clip into the boat hull when launched
      GlobalPosition = GetCurrentSeat().GlobalPosition + new Vector3(0, 0.2f, 0); 
      
			// 4. Request oar stop through host-confirmed handshake.
			UpdateOarAnimationIntent((int)_seat, 1, false);
    }

    // Now that they are officially Standing, apply the new hit
    _applyKnockback = true;
    _knockbackDirection = pushDirection;
  }

  private void ApplyKnockbackPhysicsProcess(double delta)
  {
    if (_applyKnockback)
    {
      // 1. Force the hit direction to be perfectly horizontal so they don't fly to space
      _knockbackDirection.Y = 0; 
      
      // 2. Apply the massive horizontal force
      _knockbackVelocity = _knockbackDirection.Normalized() * PlayerKnockbackForce;
      
      // 3. Add a strict, small vertical pop (2 meters per second) just to clear floor friction
      _knockbackVelocity.Y = 0.2f; 
      
      _applyKnockback = false;
    }

    if (_knockbackVelocity != Vector3.Zero)
    {
      // Decay the momentum over time using Lerp 
      _knockbackVelocity = _knockbackVelocity.Lerp(Vector3.Zero, (float)delta * 5.0f);

      if (_knockbackVelocity.LengthSquared() < 0.01f)
      {
        _knockbackVelocity = Vector3.Zero;
      }

      Velocity += _knockbackVelocity;
    }
  }

  // --- RIGIDBODY FIXES ---

  public void ApplyKnockbackRigidBodies(RigidBody3D rigidBody, Vector3 pushDirection)
  {
    if (Multiplayer.IsServer())
    {
      ApplyImpulseOnHost(rigidBody.GetPath(), pushDirection);
    }
    else
    {
      RpcId(1, nameof(ApplyImpulseOnHost), rigidBody.GetPath(), pushDirection);
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
  private void ApplyImpulseOnHost(NodePath targetPath, Vector3 pushDirection)
  {
    if (!Multiplayer.IsServer()) return;

    if (GetNodeOrNull(targetPath) is RigidBody3D rb)
    {
      // Apply locally on host
      rb.ApplyCentralImpulse(pushDirection * ObjectKnockbackForce); 
      
      // Tell everyone else to apply the same impulse
      Rpc(nameof(BroadcastImpulse), targetPath, pushDirection);
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
  private void BroadcastImpulse(NodePath targetPath, Vector3 pushDirection)
  {
    if (GetNodeOrNull(targetPath) is RigidBody3D rb)
    {
      rb.ApplyCentralImpulse(pushDirection * ObjectKnockbackForce);
    }
  }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void SyncPlayerAnimation(string animName)
  {
    // Extra safety check so we don't restart an animation that's already playing
    if (_animationPlayer.CurrentAnimation != animName)
    {
      float animSpeed = 1.0f; // 1.0 is the default 100% speed

      // Tweak individual speeds right here!
      switch (animName)
      {
				case "kneesWalk":
					animSpeed = 1.5f;
					break;
        case "backWalking":
					animSpeed = 10.0f;
					break;
        case "crouchWalkingBackward":
          animSpeed = 10.0f; // 2x as fast
          break;
				case "crouchWalking":
					animSpeed = 10.0f;
					break;
        case "initalJump":
        case "landingJump":
          animSpeed = 7.0f; 
          break;
      }

      // The second parameter '-1' tells Godot to use the default animation blending
      _animationPlayer.Play(animName, -1, animSpeed);
    }
  }
	// this changes the player look speed
	private void ChangePlayerLookSpeed(float newSpeed)
	{
		// make sure this is the current player's instance
		if (IsMultiplayerAuthority())
		{
			MouseSens = newSpeed;
		}
	}

	private void ApplySavedLocalSettings()
	{
		if (!IsMultiplayerAuthority())
		{
			return;
		}

		var savedSettings = ResourceLoader.Load<Resource>("user://user_settings_prefs.tres");
		if (savedSettings == null)
		{
			return;
		}

		MouseSens = (float)savedSettings.Get("look_speed");
		float savedFov = (float)savedSettings.Get("player_fov");
		ChangePlayerFov(savedFov);
	}

	private void ChangePlayerFov(float newFov)
	{
		float clampedFov = Mathf.Clamp(newFov, 1.0f, 179.0f);
		if (IsMultiplayerAuthority() && _playerCamera != null)
		{
			_playerCamera.Fov = clampedFov;
		}
	}

	// function to set their gamertag
	public void SetUsername(string username)
	{
		if (_gamerTag != null)
		{
			_gamerTag.Text = username;
		}
		if (IsMultiplayerAuthority())
		{
			// Set the exported variable so it broadcasts to all other clients!
			_gamerTag.Visible = false; // don't wanna see it locally
		}
	}

	// This built-in function automatically runs the exact moment the node is queued for deletion
  public override void _ExitTree()
  {
		// Unsubscribe from signals
		GlobalSignalServer.Instance.RespawnPlayer -= OnPauseUIRespawnPlayer;
		GlobalSignalServer.Instance.ApplyPlayerLookSpeed -= ChangePlayerLookSpeed;
		GlobalSignalServer.Instance.ApplyPlayerFov -= ChangePlayerFov;
		GlobalSignalServer.Instance.AssignGamertag -= SetUsername;
		GlobalSignalServer.Instance.AssignPlayerColor -= SetPlayerColor;
    GlobalSignalServer.Instance.PlayerLoudness -= OnPlayerLoudness;
    GlobalSignalServer.Instance.EndGame -= OnEndGameTriggered;

    // If the player was rowing when they disconnected/were deleted
    if (CurrPlayerState == PlayerState.Rowing && _boat != null)
    {
      // 1. Manually free the seat in the boat arrays locally for every client
      _boat.OccupiedSeats[(int)_seat] = false;
      _boat.HasOarInSeat[(int)_seat] = false;

      // 2. Stop the rowing physics (Only the server needs to emit this)
      if (Multiplayer.IsServer())
      {
        GlobalSignalServer.Instance.EmitSignal(GlobalSignalServer.SignalName.Rowing, (int)_seat, false, false);
      }

      // 3. Stop the oar animation locally for everyone
      // (Using 1 for direction is fine just to trigger the stop command)
      GlobalSignalServer.Instance.EmitSignal("AnimateOar", (int)_seat, 1, false);
    }

		Input.MouseMode = Input.MouseModeEnum.Visible;
  }

	// function to set their color from the RPC
  public void SetPlayerColor(int multiplayerID, string colorHex)
  {
		if (Name != multiplayerID.ToString())
		{
			return;
		}

		// Apply only to the targeted player's node.
		CurrentColorHex = colorHex;
		ApplyMaterialColor(colorHex);
		_lastAppliedColor = colorHex;
  }

  // Helper function to actually change the 3D meshes
  private void ApplyMaterialColor(string colorHex)
  {
    Color newColor = new Color(colorHex);

    // We MUST duplicate the material! Otherwise, changing one player's color changes ALL players.
    if (_bodyMesh.GetActiveMaterial(0) is StandardMaterial3D baseBodyMat)
    {
      StandardMaterial3D bodyMat = baseBodyMat.Duplicate() as StandardMaterial3D;
      bodyMat.AlbedoColor = newColor;
      _bodyMesh.SetSurfaceOverrideMaterial(0, bodyMat);
    }

    if (_headMesh.GetActiveMaterial(0) is StandardMaterial3D baseHeadMat)
    {
      StandardMaterial3D headMat = baseHeadMat.Duplicate() as StandardMaterial3D;
      headMat.AlbedoColor = newColor;
      _headMesh.SetSurfaceOverrideMaterial(0, headMat);
    }
  }

	// --- LOUDNESS RPC FUNCTIONS ---
  private void OnPlayerLoudness(float loudness)
  {
    // ONLY the local player listens to their own mic volume signal.
    if (IsMultiplayerAuthority())
    {
      // Tell EVERYONE (including ourselves via CallLocal) to change the target scale
      Rpc(nameof(RpcUpdateHeadScale), loudness);
    }
  }

  // CallLocal = true ensures the host also sees their own head expand
  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void RpcUpdateHeadScale(float loudness)
  {
    // Average loudness is usually a small float (like 0.05 to 0.2).
    // Set the target scale (Base scale of 1.0 + the loudness multiplied by our custom multiplier)
    _targetHeadScale = 1.0f + (loudness * VoiceScaleMultiplier);
  }

	// UI HANDLER STUFF
	// This built-in function only catches inputs that UI menus haven't eaten yet!
  public override void _UnhandledInput(InputEvent @event)
  {
    if (@event.IsActionPressed("ui_cancel"))
    {
      // If we are currently playing, pause the game
      if (CurrGameState == GameState.Playing)
      {
        CurrGameState = GameState.Menu;
      }
      // If we are already in the menu (and the input made it this far)
      else if (CurrGameState == GameState.Menu)
      {
        CurrGameState = GameState.Playing;

        // Hide Inventory if open
        if (_invUI.isOpen())
        {
          _invUI.Close();
        }
      }
    }

    if (@event.IsActionPressed("action_key"))
    {
      if (CurrGameState == GameState.Menu && _invUI.isOpen())
      {
        CurrGameState = GameState.Playing;
        _invUI.Close();
      }
    }
  }

	// endgame trigger logic
	private void OnEndGameTriggered()
  {
    Rpc(nameof(EndGame));
  }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void EndGame ()
  {
    // Put them in the EndGame state so they can't move or pause
    // CurrGameState = GameState.EndGame;
    _endGameUi.Visible = true;

		if (_endGameMusic != null && !_endGameMusic.Playing)
		{
			_endGameMusic.Play();
		}
    
    // Hide the normal gameplay UI
    _hud.Visible = false;
    _pauseUICanvas.Visible = false;
  }
}
