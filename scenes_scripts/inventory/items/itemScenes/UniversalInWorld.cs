using Godot;
using System;
using Waterways;

public partial class UniversalInWorld : RigidBody3D, Interactable
{
  [Export] public InvItem ItemObject { get; set; }
  [Export] public int ItemCount { get; set; } = 1;
  [Export] public bool CanBePickedUp { get; set; } = true;

  [ExportGroup("Water Physics Settings")]
	[Export] public float Mass = 10.0f;
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

  public override void _Ready()
  {
    if (ItemObject == null) return;

    Item = new InvSlot(ItemObject, ItemCount);

    if (ItemCount > Item.Data.MaxStackSize)
    {
      Item.Amount = Item.Data.MaxStackSize;
    }

    GetNode<MeshInstance3D>("MeshInstance3D").Mesh = Item.Data.ItemMesh;
    GetNode<CollisionShape3D>("CollisionShape3D").Shape = Item.Data.ItemCollider;

    // get the water physics node and set its parameters
    _waterPhysics = GetNode<WaterPhysics>("WaterPhysics");
		_riverFloatSystem = GetNode<RiverFloatSystem>("../../RiverManager/RiverFloatSystem");
		_waterPhysics.SetParameters(_riverFloatSystem, FloatForce, RiverSpeed, WaterDrag);

    // If anchor, emit setanchor signal (only on server to avoid redundant RPCs)
    if (Multiplayer.IsServer() && Item?.Data.Name == "Anchor")
    {
      GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.SetAnchor), GetPath());
    }
  }

  public override void _Process(double delta)
  {
    if (Item == null) return;
    PromptMessage = CanBePickedUp ? "Pick Up (" + Item.Amount + ")" : "";
  }

  public override void _PhysicsProcess(double delta)
  {
    FloatingPhysicsProcess(delta);
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

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DeleteItem()
  {
    QueueFree();
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
