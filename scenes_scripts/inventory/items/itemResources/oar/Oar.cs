using Godot;
using System;

[GlobalClass]
public partial class Oar : ItemAction
{
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