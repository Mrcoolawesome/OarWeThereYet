using Godot;
using System;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{
    [Export] public RiverFloatSystem River { get; set;}
    private Node3D _probeContainer;

    public override void _Ready()
    {
        _probeContainer = GetNode<Node3D>("ProbeContainer");
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        foreach (Marker3D probe in _probeContainer.GetChildren().OfType<Marker3D>())
        {
            
        }
    }
}