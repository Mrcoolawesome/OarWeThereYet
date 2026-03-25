using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class AnchorPoint : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Reset Anchor";
	public string PromptInput { get; set; } = "action_key";
  private StaticBody3D _anchor;
  private bool _deployed = false;

  public override void _Ready()
  {
    _anchor = GetNode<StaticBody3D>("Anchor");
  }

  public override void _Process(double delta)
  {
  }


	public void Interact(Player player)
	{
    if (_deployed)
    {
      // Remove anchor from world or hand
      _anchor.Visible = true;
    }
    else
    {
      player.ArmNode.Rpc(nameof(player.ArmNode.SetItem), "res://scenes_scripts/inventory/items/itemResources/anchor/anchor.tres", 1);
      _anchor.Visible = false;

    }

    _deployed = !_deployed;
	}
}
