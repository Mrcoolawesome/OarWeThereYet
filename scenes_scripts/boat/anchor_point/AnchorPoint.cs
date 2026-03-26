using Godot;
using System;

public partial class AnchorPoint : StaticBody3D, Interactable
{
  [Export] public string PromptMessage { get; set; } = "Reset Anchor";
  [Export] public float MaxRopeRange = 10.0f;

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
    if (_deployed)
    {
      // Display rope from this node to _deployedAnchor
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_deployed)
    {
      // Keep boat within MaxRopeRange
    }
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
      RpcId(1, nameof(DeleteAnchor));
      _deployedAnchor = null;
      _anchor.Visible = true;
    }
    else
    {
      player.ArmNode.Rpc(nameof(player.ArmNode.SetItem), "res://scenes_scripts/inventory/items/itemResources/anchor/anchor.tres", 1);
      _anchor.Visible = false;
    }

    _deployed = !_deployed;
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  public void DeleteAnchor()
  {
    if (!Multiplayer.IsServer()) return;

    // Remove item from world or remove from player's ArmNode
    // Make sure you use the proper rpc calls to do this
    if (_deployedAnchor is MeshInstance3D)
    {
      ArmNode arm = _deployedAnchor.GetNode<ArmNode>("../../../../../Head/ArmNode");
      arm.Rpc(nameof(arm.SetItem), "", 0);
    }

    if (_deployedAnchor is UniversalInWorld anchorInWorld)
    {
      anchorInWorld.Rpc(nameof(anchorInWorld.DeleteItem));
    }
  }

  public void RequestSetAnchor(string anchorNodePath)
	{
		RpcId(1, nameof(SetAnchor), anchorNodePath);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  public void SetAnchor(string anchorNodePath)
  {
    if (!Multiplayer.IsServer()) return;

    // Set anchor to be the UniversalInWorld or ArmNode
    _deployedAnchor = GetNode<Node3D>(anchorNodePath);
  }
}
