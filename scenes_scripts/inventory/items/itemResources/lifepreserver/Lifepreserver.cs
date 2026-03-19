using Godot;
using System;

[GlobalClass]
public partial class Lifepreserver : ItemAction
{
  public override void Use(Player player, ArmNode arm)
  {
    if (player == null || arm == null) return;

    Camera3D camera = player.GetNodeOrNull<Camera3D>("Head/CameraContainer/Camera3D");
    Vector3 throwDirection = camera != null
      ? -camera.GlobalTransform.Basis.Z
      : -player.GlobalTransform.Basis.Z;

    arm.RequestToggleLifepreserverThrow(throwDirection);
  }
}
