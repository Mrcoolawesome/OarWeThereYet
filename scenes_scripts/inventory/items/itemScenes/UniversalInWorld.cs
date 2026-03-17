using Godot;
using System;

public partial class UniversalInWorld : RigidBody3D, Interactable
{
  [Export] public InvItem ItemObject { get; set; }
  [Export] public int ItemCount { get; set; } = 1;
  public InvSlot Item { get; set; }
  public string PromptMessage { get; set; } = "Pick Up";
  public string PromptInput { get; set; } = "action_key";

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
  }

  public override void _Process(double delta)
  {
    if (Item == null) return;
    PromptMessage = "Pick Up (" + Item.Amount + ")";
  }

  public void Interact(Player player)
  {
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
	private void DeleteItem()
  {
    QueueFree();
  }
}
