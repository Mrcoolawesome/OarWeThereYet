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

  }
}
