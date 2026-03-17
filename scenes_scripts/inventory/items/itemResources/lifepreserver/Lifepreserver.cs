using Godot;
using System;

[GlobalClass]
public partial class Lifepreserver : ItemAction
{
  public override void Use(Player _player, ArmNode _arm)
  {
    GD.Print("Throw item");
  }
}
