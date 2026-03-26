using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class AnchorPoint : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Reset Anchor";
	public string PromptInput { get; set; } = "action_key";
  private StaticBody3D _anchor;
  private bool _deployed = false;
  private Node3D _deployedAnchor;

  public override void _Ready()
  {
    _anchor = GetNode<StaticBody3D>("Anchor");

    GlobalSignalServer.Instance.SetAnchor += RequestSetAnchor;
  }

  public override void _Process(double delta)
  {
  }


	public void Interact(Player player)
	{
    Rpc(nameof(ToggleAnchor), player.GetPath());
	}

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  public void ToggleAnchor(string playerPath)
  {
    Player player = GetNode<Player>(playerPath);

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

  public void GetAnchorPosition()
  {
    // Get the position of UniversalInWorld or player hand bone
  }

  public void DeleteAnchor()
  {
    // Remove item from world or remove from player's ArmNode
    // Make sure you use the proper rpc calls to do this
  }

  public void RequestSetAnchor(string anchorNodePath)
	{
		RpcId(1, nameof(SetAnchor), anchorNodePath);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
  public void SetAnchor(string anchorNodePath)
  {
    // Set anchor to be the UniversalInWorld or ArmNode
    GD.Print(anchorNodePath);
    _deployedAnchor = GetNode<Node3D>(anchorNodePath);
  }
}
