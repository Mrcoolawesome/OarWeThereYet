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
        _probeContainer = GetNode<Node3D>("ProbeContainer");
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        foreach (Marker3D probe in _probeContainer.GetChildren().OfType<Marker3D>())
        {
            Vector3 pos = probe.GlobalPosition;

            Vector3 flowDirection = River.GetWaterFlowDirection(pos);
            Vector3 riverRight = flowDirection.Cross(Vector3.Up).Normalized();
            Vector3 riverNormal = riverRight.Cross(flowDirection).Normalized();
            
            ApplyForce(riverNormal * 2, probe.GlobalPosition - GlobalPosition);
        }
    }
}