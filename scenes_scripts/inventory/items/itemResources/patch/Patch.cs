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
    }
  }
}
