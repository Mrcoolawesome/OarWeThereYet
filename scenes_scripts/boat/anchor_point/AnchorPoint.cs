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

  // Node for the rope visual
  private Node3D _ropeRoot = null;
  private MeshInstance3D _ropeMeshInstance = null;
  private CylinderMesh _ropeMesh = null;

  public override void _Ready()
  {
    _anchor = GetNode<StaticBody3D>("Anchor");

    GlobalSignalServer.Instance.SetAnchor += RequestSetAnchor;

    // Initialize rope visual (similar to ArmNode.cs)
    _ropeRoot = new Node3D();
    _ropeRoot.Name = "RopeRoot";
    _ropeRoot.GlobalPosition = Vector3.Zero;
    GetTree().Root.AddChild(_ropeRoot);

    _ropeMesh = new CylinderMesh();
    _ropeMesh.TopRadius = 0.05f;
    _ropeMesh.BottomRadius = 0.05f;
    _ropeMesh.Height = 1.0f;
    _ropeMesh.RadialSegments = 8;
    _ropeMeshInstance = new MeshInstance3D();
    _ropeMeshInstance.Mesh = _ropeMesh;
    _ropeMeshInstance.Visible = false;

    // Black unshaded material for the rope
    var ropeMaterial = new StandardMaterial3D();
    ropeMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
    ropeMaterial.AlbedoColor = new Color(0, 0, 0); // Black
    _ropeMeshInstance.SetSurfaceOverrideMaterial(0, ropeMaterial);
    _ropeRoot.AddChild(_ropeMeshInstance);
  }

  public override void _Process(double delta)
  {
    if (_deployed && IsInstanceValid(_deployedAnchor))
    {
      UpdateRopeMesh();
    }
    else
    {
      _ropeMeshInstance.Visible = false;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (!Multiplayer.IsServer()) return;

    if (_deployed && IsInstanceValid(_deployedAnchor))
    {
      float distance = GlobalPosition.DistanceTo(_deployedAnchor.GlobalPosition);

      // Keep boat within MaxRopeRange
      if (distance > MaxRopeRange)
      {
        RigidBody3D boat = GetParent<RigidBody3D>();
        if (boat != null)
        {
          Vector3 directionToAnchor = GlobalPosition.DirectionTo(_deployedAnchor.GlobalPosition);
          float pullStrength = (distance - MaxRopeRange) * 500.0f; // Scale pull by excess distance
          
          // Apply force at the anchor point's position relative to the boat
          Vector3 relativePos = GlobalPosition - boat.GlobalPosition;
          boat.ApplyForce(directionToAnchor * pullStrength, relativePos);
        }
      }
    }
  }

  private void UpdateRopeMesh()
  {
    if (!IsInstanceValid(_deployedAnchor))
    {
      _ropeMeshInstance.Visible = false;
      return;
    }

    Vector3 start = GlobalPosition;
    Vector3 end = _deployedAnchor.GlobalPosition;
    Vector3 mid = (start + end) * 0.5f;
    Vector3 dir = end - start;
    float length = dir.Length();

    if (length < 0.01f)
    {
      _ropeMeshInstance.Visible = false;
      return;
    }

    _ropeMeshInstance.Visible = true;
    _ropeMesh.Height = length;
    
    // Align the cylinder with the direction vector
    var up = Vector3.Up;
    var axis = up.Cross(dir.Normalized());
    float angle = Mathf.Acos(up.Dot(dir.Normalized()));
    var rotation = axis.LengthSquared() > 0.0001f ? new Quaternion(axis.Normalized(), angle) : Quaternion.Identity;
    
    _ropeMeshInstance.GlobalTransform = new Transform3D(new Basis(rotation), mid);
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
      ResetAnchor();
    }
    else
    {
      player.ArmNode.Rpc(nameof(player.ArmNode.SetItem), "res://scenes_scripts/inventory/items/itemResources/anchor/anchor.tres", 1);
      _anchor.Visible = false;
      _deployed = true;
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  public void DeleteAnchor()
  {
    if (!Multiplayer.IsServer()) return;
    if (!IsInstanceValid(_deployedAnchor)) return;

    // Remove item from world or remove from player's ArmNode
    // Make sure you use the proper rpc calls to do this
    if (_deployedAnchor is MeshInstance3D)
    {
      ArmNode arm = _deployedAnchor.GetNodeOrNull<ArmNode>("../../../../../Head/ArmNode");
      if (arm != null)
      {
        arm.Rpc(nameof(arm.SetItem), "", 0);
      }
    }

    if (_deployedAnchor is UniversalInWorld anchorInWorld)
    {
      anchorInWorld.Rpc(nameof(anchorInWorld.DeleteItem));
    }
  }

  public void ResetAnchor()
  {
    if (_deployed)
    {
      if (Multiplayer.IsServer())
      {
        DeleteAnchor();
      }
      _deployedAnchor = null;
      _anchor.Visible = true;
      _deployed = false;
      if (_ropeMeshInstance != null)
      {
        _ropeMeshInstance.Visible = false;
      }
    }
  }

  public void RequestSetAnchor(string anchorNodePath)
	{
		Rpc(nameof(SetAnchor), anchorNodePath);
	}
	
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  public void SetAnchor(string anchorNodePath)
  {
    if (string.IsNullOrEmpty(anchorNodePath))
    {
      _deployedAnchor = null;
    }
    else
    {
      _deployedAnchor = GetNodeOrNull<Node3D>(anchorNodePath);
    }
  }
}
