using Godot;
using System;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{
    [Export] public RiverFloatSystem River;
    [Export] public float FloatForce = 1.0f;
    [Export] public float RiverSpeed = 1.0f;
    [Export] public float WaterDrag = 2.0f;
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
            Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);

            float waterHeight = River.GetWaterHeight(globalPos);
            float depth = waterHeight - globalPos.Y;
            float buoyancyMultiplier = 2 - Mathf.Exp(-depth + 0.6f);
            Vector3 buoyancyForce = waterNormal(globalPos) * _gravity * FloatForce * buoyancyMultiplier;

            Vector3 currentVelocity = LinearVelocity + AngularVelocity.Cross(relativePos);
            Vector3 frictionForce = -currentVelocity * depth * WaterDrag;

            if (depth > 0)
            {
                ApplyForce(buoyancyForce + frictionForce, relativePos);
                ApplyForce(flowDirection * RiverSpeed, relativePos);
            }
        }
    }

    private Vector3 waterNormal(Vector3 globalPos)
    {
        Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);
        Vector3 waterRight = flowDirection.Cross(Vector3.Up);
        return waterRight.Cross(flowDirection);
    }

    // Logic for player entering a chair
    public void OnChairsBodyEntered(PhysicsBody3D body)
    {
        GD.Print("GAMING");
    }
}