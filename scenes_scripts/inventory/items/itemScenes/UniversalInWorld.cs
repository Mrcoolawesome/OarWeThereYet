using Godot;
using System;

public partial class UniversalInWorld : RigidBody3D, Interactable
{
  [Export] public InvItem Item { get; set; }
  public string PromptMessage { get; set; } = "Pick Up";
  public string PromptInput { get; set; } = "action_key";

  public override void _Ready()
  {
    if (Item == null) return;

    GetNode<MeshInstance3D>("MeshInstance3D").Mesh = Item.ItemMesh;
    GetNode<CollisionShape3D>("CollisionShape3D").Shape = Item.ItemCollider;
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
    // Get player and their arm
    int playerID = Multiplayer.GetRemoteSenderId();
    ArmNode playerArm = GetNode<ArmNode>("/root/GameManager/Level/DemoLevel/" + playerID + "/Head/ArmNode");

    // If player isn't holding an item
    if (playerArm.Item == null)
    {
      // Give player item
      string itemPath = Item.ResourcePath;
      playerArm.Rpc(nameof(playerArm.SetItem), itemPath);

      // Delete item in world
      Rpc(nameof(DeleteItem));
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void DeleteItem()
  {
    QueueFree();
  }
}
