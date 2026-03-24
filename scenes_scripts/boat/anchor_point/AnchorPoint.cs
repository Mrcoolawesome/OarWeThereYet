using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class AnchorPoint : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Reset Anchor";
	public string PromptInput { get; set; } = "action_key";
  public UniversalInWorld _anchor;

  public override void _Ready()
  {
    _anchor = GetNode<UniversalInWorld>("UniversalInWorld");
  }

  public override void _Process(double delta)
  {
    _anchor.GlobalPosition = new Vector3(GlobalPosition.X - 0.3f, GlobalPosition.Y, GlobalPosition.Z);
    _anchor.GlobalRotation = GlobalRotation;
  }


	public void Interact(Player player)
	{
		
	}
}
