using Godot;
using System;

[GlobalClass]
public partial class Gabe : ItemAction
{
  public override void Use(Player _player, ArmNode _arm)
  {
    GD.Print("I love you");
  }
}
