using Godot;
using System;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{
    [Export] public RiverFloatSystem River { get; set;}
    [Export] public float WaterDensity = 25.0f;
    [Export] public float FlowForceMultiplier = 10.0f;
    [Export] public float WaterDrag = 2.0f;
    [Export] public float FlowSpeed = 5.0f;
    
    private Node3D _probeContainer;

    public override void _Ready()
    {
        Node3D _probeContainer = GetNode<Node3D>("ProbeContainer");
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        foreach (Marker3D probe in _probeContainer.GetChildren().OfType<Marker3D>())
        {
            GD.Print("node here");
        }
    }
}