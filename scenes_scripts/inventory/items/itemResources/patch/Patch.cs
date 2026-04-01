using Godot;
using System;

[GlobalClass]
public partial class Patch : ItemAction
{
  public override void Use(Player player, ArmNode arm)
  {
    GodotObject target = player.GetRaycastObject();

    if (target is Hole hole)
    {
      hole.Rpc(nameof(Hole.RequestPatch));

      // Consume one patch
      int newAmount = arm.Item.Amount - 1;
      if (newAmount <= 0)
      {
        arm.Rpc(nameof(ArmNode.SetItem), "", 0);
      }
      else
      {
        arm.Rpc(nameof(ArmNode.SetItem), arm.Item.Data.ResourcePath, newAmount);
      }
    }
  }
}
