using Godot;
using System;

[GlobalClass]
public partial class InvItem : Resource
{
  [ExportGroup("UI Data")]
  [Export] public string Name = "ItemDefault";
  [Export] public Texture Icon = null;
  [Export] public int MaxStackSize = 1;

  [ExportGroup("3D Visuals")]
  [Export] public Mesh ItemMesh { get; set; }
  [Export] public Shape3D ItemCollider { get; set; }
}
