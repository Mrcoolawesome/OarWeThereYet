using Godot;
using System;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{
    [Export] public RiverFloatSystem River;
    [Export] public float FloatForce = 1.0f;
    [Export] public float AngleDamp = 0.1f;
    [Export] public float LineDamp = 0.1f;
    private Node3D _probeContainer;
    private float _gravity;

    public override void _Ready()
    {
        _probeContainer = GetNode<Node3D>("ProbeContainer");
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (Marker3D probe in _probeContainer.GetChildren().OfType<Marker3D>())
        {
            Vector3 globalPos = probe.GlobalPosition;
            Vector3 relativePos = probe.GlobalPosition - GlobalPosition;

            float waterHeight = River.GetWaterHeight(globalPos);
            float depth = (waterHeight - globalPos.Y) / 25.0f;

            Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);
            Vector3 waterRight = flowDirection.Cross(Vector3.Up);
            Vector3 waterNormal = waterRight.Cross(flowDirection);
            
            if (depth > 0)
            {
                ApplyForce(waterNormal * _gravity * FloatForce * depth, relativePos);
            }
        }
    }

    // public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    // {
    //     state.AngularVelocity *= AngleDamp;
    //     state.LinearVelocity *= LineDamp;
    // }
}