using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class AnchorPoint : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Reset Anchor";
	public string PromptInput { get; set; } = "action_key";
  public StaticBody3D _anchor;

  public override void _Ready()
  {
    _anchor = GetNode<StaticBody3D>("Anchor");
  }

  public override void _Process(double delta)
  {
  }


	public void Interact(Player player)
	{
		_anchor.Visible = !_anchor.Visible;
	}
}
