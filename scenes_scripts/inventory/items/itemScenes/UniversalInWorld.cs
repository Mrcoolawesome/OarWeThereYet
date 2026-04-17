using Godot;
using System;
using Waterways;

public partial class UniversalInWorld : RigidBody3D, Interactable
{
  [Export] public InvItem ItemObject { get; set; }
  [Export] public int ItemCount { get; set; } = 1;
  [Export] public bool CanBePickedUp { get; set; } = true;

  [ExportGroup("Water Physics Settings")]
  [Export] public new float Mass = 10.0f;
	[Export] public float FloatForce = 1.0f;
	[Export] public float RiverSpeed = 1.0f;
	[Export] public float WaterDrag = 2.0f;
  public InvSlot Item { get; set; }
  public string PromptMessage { get; set; } = "Pick Up";
  public string PromptInput { get; set; } = "action_key";
  private WaterPhysics _waterPhysics;
  private RiverFloatSystem _riverFloatSystem;
  private bool _applyWaterPhysicsForce = false;
	private Vector3 _waterPhysicsForce;
	private Vector3 _waterPhysicsForcePosition;
  private bool _applyNewPositionState = false;
  private bool _applyNewRotationState = false;
  private bool _applyNewVelocityState = false;
  private Transform3D _newPositionState;
  private Basis _newRotationState;
  private Vector3 _newLinearVelocityState;
  private Vector3 _newAngularVelocityState;
  private int _stateSequence = 0;
  private bool _hasAppliedState = false;
  private bool _hasLastBroadcastState = false;
  private Vector3 _lastBroadcastPosition;
  private Quaternion _lastBroadcastRotation;
  private Vector3 _lastBroadcastLinearVelocity;
  private Vector3 _lastBroadcastAngularVelocity;

	// Get the terrain
	private Node3D _terrain;
	private GodotObject _terrainData;

  public override void _Ready()
  {
    if (ItemObject == null) return;

    SetMultiplayerAuthority(1);

    Item = new InvSlot(ItemObject, ItemCount);

    if (ItemCount > Item.Data.MaxStackSize)
    {
      Item.Amount = Item.Data.MaxStackSize;
    }

    GetNode<MeshInstance3D>("MeshInstance3D").Mesh = Item.Data.ItemMesh;
    GetNode<CollisionShape3D>("CollisionShape3D").Shape = Item.Data.ItemCollider;

    // get the water physics node and set its parameters
    _waterPhysics = GetNode<WaterPhysics>("WaterPhysics");
		_riverFloatSystem = GetNodeOrNull<RiverFloatSystem>("../../RiverManager/RiverFloatSystem");
		
    if (_riverFloatSystem != null)
    {
      _waterPhysics.SetParameters(_riverFloatSystem, FloatForce, RiverSpeed, WaterDrag);
    }

    Freeze = !Multiplayer.IsServer();

    // If anchor, emit setanchor signal (only on server to avoid redundant RPCs)
    if (Multiplayer.IsServer() && Item?.Data.Name == "Anchor")
    {
      GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.SetAnchor), GetPath());
    }
		
		// Get terrain
		_terrain = GetNode<Node3D>("../../Terrain3D");
		_terrainData = _terrain.Get("data").AsGodotObject();

    if (Multiplayer.IsServer())
    {
      Multiplayer.PeerConnected += OnPeerConnected;
    }
  }

  public override void _Process(double delta)
  {
    if (Item == null) return;
    PromptMessage = CanBePickedUp ? "Pick Up (" + Item.Amount + ")" : "";

    if (!Multiplayer.IsServer())
    {
      SyncAndLerpClientState(delta);
      return;
    }

		// If player falls below terrain, teleport them back up
		if (_terrainData != null)
		{
			float terrainHeight = _terrainData.Call("get_height", GlobalPosition).AsSingle();
			if (GlobalPosition.Y - terrainHeight < -0.5f)
			{
				GlobalPosition = new Vector3(GlobalPosition.X, terrainHeight + 1, GlobalPosition.Z);
			}
		}
  }

  public override void _PhysicsProcess(double delta)
  {
    if (!Multiplayer.IsServer()) return;

    if (Item?.Data.Name != "Anchor")
    {
      FloatingPhysicsProcess(delta);
    }

    BroadcastStateIfNeeded();
  }

  public override void _ExitTree()
  {
    if (Multiplayer.IsServer())
    {
      Multiplayer.PeerConnected -= OnPeerConnected;
    }
  }


  public void Interact(Player player)
  {
    if (!CanBePickedUp) return;
    RequestItemPickup();
  }

  private void RequestItemPickup()
  {
    RpcId(1, MethodName.ItemPickup);
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ItemPickup()
  {
    int playerID = Multiplayer.GetRemoteSenderId();
    ArmNode playerArm = GetPlayerArm(playerID);
    if (playerArm == null) return;

    string itemPath = Item.Data.ResourcePath;

    // If player is holding the same item
    if (playerArm.Item != null && playerArm.Item.Data.ResourcePath == itemPath)
    {
      int heldAmount = playerArm.Item.Amount;
      int maxStack = Item.Data.MaxStackSize;
      int spaceLeft = maxStack - heldAmount;

      if (spaceLeft <= 0) return; // Hand is already full

      if (Item.Amount <= spaceLeft)
      {
        // Entire ground stack fits into hand
        playerArm.Rpc(nameof(playerArm.SetItem), itemPath, heldAmount + Item.Amount);
        Rpc(nameof(DeleteItem));
      }
      else
      {
        // Only pick up enough to fill the hand to max
        playerArm.Rpc(nameof(playerArm.SetItem), itemPath, maxStack);
        Rpc(nameof(UpdateAmount), Item.Amount - spaceLeft);
      }
    }
    // If player isn't holding an item
    else if (playerArm.Item == null)
    {
      // Give player item
      int itemCount = Item.Amount;
      playerArm.Rpc(nameof(playerArm.SetItem), itemPath, itemCount);

      // Delete item in world
      Rpc(nameof(DeleteItem));
    }
  }

  private ArmNode GetPlayerArm(int playerID)
  {
    // UniversalInWorld is expected under Level/ItemContainer, so walk to Level first.
    Node levelNode = GetParent()?.GetParent();
    if (levelNode == null) return null;

    return levelNode.GetNodeOrNull<ArmNode>(playerID + "/Head/ArmNode");
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void UpdateAmount(int newAmount)
  {
    Item.Amount = newAmount;
    ItemCount = newAmount;
  }

  private void BroadcastStateIfNeeded()
  {
    if (!Multiplayer.IsServer())
    {
      return;
    }

    Vector3 currentPosition = GlobalPosition;
    Quaternion currentRotation = Quaternion;
    Vector3 currentLinearVelocity = LinearVelocity;
    Vector3 currentAngularVelocity = AngularVelocity;

    bool changed = !_hasLastBroadcastState
      || currentPosition.DistanceTo(_lastBroadcastPosition) > 0.01f
      || Mathf.Abs(currentRotation.AngleTo(_lastBroadcastRotation)) > Mathf.DegToRad(0.5f)
      || currentLinearVelocity.DistanceTo(_lastBroadcastLinearVelocity) > 0.01f
      || currentAngularVelocity.DistanceTo(_lastBroadcastAngularVelocity) > 0.01f;

    if (!changed)
    {
      return;
    }

    _hasLastBroadcastState = true;
    _lastBroadcastPosition = currentPosition;
    _lastBroadcastRotation = currentRotation;
    _lastBroadcastLinearVelocity = currentLinearVelocity;
    _lastBroadcastAngularVelocity = currentAngularVelocity;
    _stateSequence++;

    Rpc(nameof(ApplyWorldItemState), _stateSequence, currentPosition, currentRotation, currentLinearVelocity, currentAngularVelocity);
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DeleteItem()
  {
    QueueFree();
  }

  private void OnPeerConnected(long peerId)
  {
    if (!Multiplayer.IsServer()) return;

    RpcId(peerId, nameof(ApplyWorldItemState), _stateSequence, GlobalPosition, Quaternion, LinearVelocity, AngularVelocity);
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void ApplyWorldItemState(int sequence, Vector3 position, Quaternion rotation, Vector3 linearVelocity, Vector3 angularVelocity)
  {
    if (Multiplayer.IsServer())
    {
      return;
    }

    if (_hasAppliedState && sequence <= _stateSequence)
    {
      return;
    }

    _stateSequence = sequence;
    _hasAppliedState = true;

    float positionDiff = (position - GlobalPosition).Length();
    if (positionDiff > 0.05f)
    {
      _newPositionState = new Transform3D(new Basis(rotation), position);
      _applyNewPositionState = true;
    }

    Quaternion currentRotation = Quaternion;
    if (Mathf.Abs(currentRotation.AngleTo(rotation)) > Mathf.DegToRad(1.0f))
    {
      _newRotationState = new Basis(rotation);
      _applyNewRotationState = true;
    }

    _newLinearVelocityState = linearVelocity;
    _newAngularVelocityState = angularVelocity;
    _applyNewVelocityState = true;
  }

  private void SyncAndLerpClientState(double delta)
  {
    float weight = (float)delta * 10.0f;

    if (_applyNewPositionState)
    {
      GlobalTransform = GlobalTransform.InterpolateWith(_newPositionState, weight);

      if (GlobalTransform.Origin.DistanceTo(_newPositionState.Origin) < 0.01f)
      {
        _applyNewPositionState = false;
      }
    }

    if (_applyNewRotationState)
    {
      Quaternion currentRot = GlobalTransform.Basis.GetRotationQuaternion();
      Quaternion targetRot = _newRotationState.GetRotationQuaternion();
      Quaternion smoothRot = currentRot.Slerp(targetRot, weight);

      Vector3 currentPosition = GlobalTransform.Origin;
      GlobalTransform = new Transform3D(new Basis(smoothRot), currentPosition);

      if (Mathf.Abs(currentRot.AngleTo(targetRot)) < 0.01f)
      {
        _applyNewRotationState = false;
      }
    }

    if (_applyNewVelocityState)
    {
      LinearVelocity = _newLinearVelocityState;
      AngularVelocity = _newAngularVelocityState;
      _applyNewVelocityState = false;
    }
  }

  // function that's called from the water physics node's signal
	private void QueueApplyWaterPhysicsForce(Vector3 force, Vector3 relativePosition)
	{
		// set the apply water physics force boolean to be true so that it can be applied in PhysicsProcess
		_applyWaterPhysicsForce = true;

		// then set the global force and forcePosition variables so that they can be seen by PhysicsProcess
		_waterPhysicsForce = force;
		_waterPhysicsForcePosition = relativePosition;
	}

  private void FloatingPhysicsProcess(double delta)
	{
		if (_applyWaterPhysicsForce)
		{
			// Calculate acceleration: a = F/m
			Vector3 waterAcceleration = _waterPhysicsForce / Mass;
			// Add the acceleration to the velocity over time
			ApplyForce(waterAcceleration * (float)delta);
			// set _applyWaterPhysicsForce back to false
			_applyWaterPhysicsForce = false;
		}
	}
}
