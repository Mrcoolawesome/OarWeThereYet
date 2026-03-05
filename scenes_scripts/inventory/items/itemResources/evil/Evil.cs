using Godot;
using System;

[GlobalClass]
public partial class Evil : ItemAction
{
  public override void Use(Player _player, ArmNode _arm)
  {
    GD.Print("I hate you");
  }
}
