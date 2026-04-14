using Godot;
using System;

[GlobalClass]
public partial class Oar : ItemAction
{
  private static Boat FindBoatAncestor(Node node)
  {
    Node currentNode = node;

    while (currentNode != null)
    {
      if (currentNode is Boat boat)
      {
        return boat;
      }

      currentNode = currentNode.GetParent();
    }

    return null;
  }

  public override void Use(Player _player, ArmNode _arm)
  {
    GodotObject observedObject = _player.GetRaycastObject();

    if (observedObject is not Node3D)
    {
      _player.TriggerPlayerHitSwoosh();
      return;
    }

    if (observedObject is Node3D targetNode)
    {
      if (targetNode is CharacterBody3D)
      {
        _player.TriggerPlayerHitSomething();
      }

      if (FindBoatAncestor(targetNode) != null)
      {
        _player.TriggerPlayerHitBoat();
      }

      // Calculate the exact direction from the attacker to the target
      Vector3 pushDirection = (targetNode.GlobalPosition - _player.GlobalPosition).Normalized();

      if (targetNode is RigidBody3D rigidBody)
      {
        _player.ApplyKnockbackRigidBodies(rigidBody, pushDirection);
      }
      else if (targetNode is Player targetPlayer) // Cast the target as a Player!
      {
        // THIS IS THE FIX: Call the network request on the TARGET'S node!
        targetPlayer.ApplyKnockbackOnClient(targetPlayer.Name, pushDirection);
      }
    }
  }
}