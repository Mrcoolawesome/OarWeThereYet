using Godot;
using System;

[GlobalClass]
public partial class InvItem : Resource
{
  [ExportGroup("UI Data")]
  [Export] public string Name = "ItemDefault";
  [Export] public Texture Icon = null;
  [Export] public int MaxStackSize = 1;
  [Export] public string Description = "ItemDefault";

  [ExportGroup("3D Visuals")]
  [Export] public Mesh ItemMesh { get; set; }
  [Export] public Shape3D ItemCollider { get; set; }
  [Export] public Vector3 InHandPosition = Vector3.Zero;
  [Export] public Vector3 InHandRotation = Vector3.Zero;

  [ExportGroup("Functionality")]
  [Export] public ItemAction UseAction { get; set; }

  [ExportGroup("Hints")]
  [Export] public string Hint1 = "";
  [Export] public string Hint2 = "";
  [Export] public string HintAlt1 = "";
  [Export] public string HintAlt2 = "";
}
