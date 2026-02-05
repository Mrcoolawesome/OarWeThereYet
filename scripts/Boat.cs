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
    [Export] public float RowForce = 10.0f;

    [Signal] public delegate void SeatEnteredEventHandler(Vector3 seatPosition);

    private Node3D _boatFloatProbesContainer;
    private Node3D _oarProbesContainer;
    private float _gravity;

    // booleans to apply rowing force to specific spots on the boat
    private bool[] _rowingStates = new bool[4]; // state to say if one of the oars is rowing or not
    private bool[] _rowingStatesDirection = new bool[4]; // direction of rowing: backward = false | forward = true

    /*
        front left localShapeIndex: 0
        front right localShapeIndex: 1
        back right localShapeIndex: 2
        back left localShapeIndex: 3
    */
    public enum SeatIndicies
    {
        FrontLeft = 0,
        FrontRight = 1,
        BackRight = 2,
        BackLeft = 3
    }

    public override void _Ready()
    {
        _boatFloatProbesContainer = GetNode<Node3D>("BoatFloatProbesContainer");
        _oarProbesContainer = GetNode<Node3D>("OarProbesContainer");
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (Marker3D probe in _boatFloatProbesContainer.GetChildren().OfType<Marker3D>())
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

        // i don't think we need all that OfType stuff as so long as we don't add anything that isn't a marker3d in this container which we shouldn't
        foreach (Marker3D probe in _oarProbesContainer.GetChildren().OfType<Marker3D>())
        {
            // this value is the relative position to the boat
            Vector3 relativePosition = probe.GlobalPosition - GlobalPosition; // im making this into a new variable so it's more clear on what it is
            // get the forward direction of the boat
            Vector3 forwardDirection = -GlobalBasis.X;
            Vector3 finalForce = Vector3.Zero;

            // check if we're allowed to row this oar given their state
            // also im doing an else-if tree because there shouldn't be a reason for more than one of these if statements to apply at the same time
            if (probe.Name == "OarFrontRight" && _rowingStates[(int)SeatIndicies.FrontRight])
            {
                // are they going forward or backwards?
                bool direction = _rowingStatesDirection[(int)SeatIndicies.FrontRight];
                
                // go forward or backward
                finalForce = direction ? -forwardDirection : forwardDirection;
                
                // multiply it by the row force
                finalForce *= RowForce;
            }
            else if (probe.Name == "OarBackRight" && _rowingStates[(int)SeatIndicies.BackRight])
            {
                // are they going forward or backwards?
                bool direction = _rowingStatesDirection[(int)SeatIndicies.BackRight];
                
                // go forward or backward
                finalForce = direction ? -forwardDirection : forwardDirection;
                
                // multiply it by the row force
                finalForce *= RowForce;
            }
            else if (probe.Name == "OarFrontLeft" && _rowingStates[(int)SeatIndicies.FrontLeft])
            {
                // are they going forward or backwards?
                bool direction = _rowingStatesDirection[(int)SeatIndicies.FrontLeft];
                
                // go forward or backward
                finalForce = direction ? -forwardDirection : forwardDirection;
                
                // multiply it by the row force
                finalForce *= RowForce;
            }
            else if (probe.Name == "OarBackLeft" && _rowingStates[(int)SeatIndicies.BackLeft])
            {
                // are they going forward or backwards?
                bool direction = _rowingStatesDirection[(int)SeatIndicies.BackLeft];
                
                // go forward or backward
                finalForce = direction ? -forwardDirection : forwardDirection;
                
                // multiply it by the row force
                finalForce *= RowForce;
            }

            if (finalForce.LengthSquared() > 0.01f) 
            {
                // Draw an arrow starting at the Oar (probe) and pointing in the direction of the force
                // We scale finalForce * 0.5f just to keep the arrow from being too huge on screen if RowForce is high
                DebugDraw3D.DrawArrow(
                    probe.GlobalPosition,                   // Start
                    probe.GlobalPosition + finalForce,      // End (Tip of arrow)
                    Colors.Yellow,                          // Color
                    0.2f                                    // Arrow head size
                );
            }

            ApplyForce(finalForce, relativePosition); // finally apply the force
        }
    }

    private Vector3 waterNormal(Vector3 globalPos)
    {
        Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);
        Vector3 waterRight = flowDirection.Cross(Vector3.Up);
        return waterRight.Cross(flowDirection);
    }

    /*
        (all of this is assuming you're facing the front)
        front left: 4, 2, -2
        front right: 4, 2, 2
        back left: 0, 2, -2
        back right: 0, 2, 2

        front left localShapeIndex: 0
        front right localShapeIndex: 1
        back right localShapeIndex: 2
        back left localShapeIndex: 3
    */

    // Trigger player logic for player entering a chair
    public void SeatAreaBodyShapeEntered(Rid bodyRid, Node3D body, int bodyShapeIndex, int localShapeIndex)
	{
		if (body is Player player)
		{
            SeatIndicies seat = (SeatIndicies)localShapeIndex;
            // tell the player to run code to look for the e key and set their relative position and reparent themselves
            player.SetRowingState(true, seat);
		}
	}

    // Trigger player logic for player leaving a chair
    public void SeatAreaBodyShapeExited(Rid bodyRid, Node3D body, int bodyShapeIndex, int localShapeIndex)
	{
		if (body is Player player)
		{
            player.SetRowingState(true, SeatIndicies.FrontLeft); // set default of FrontLeft ig
		}
	}

    // helper function to get the relative positions of each of the seats
    public Vector3 GetSeatOffset(SeatIndicies seat)
{
        return seat switch
        {
            SeatIndicies.FrontLeft  => new Vector3(2, 1, -1),
            SeatIndicies.FrontRight => new Vector3(2, 1, 1),
            SeatIndicies.BackLeft   => new Vector3(0, 1, -1),
            SeatIndicies.BackRight  => new Vector3(0, 1, 1),
            _ => Vector3.Zero // Default fallback
        };
    }

    // getting the signals to row forward or to stop
    public void OnPlayerRowing(int seat, bool stopStart, bool backForward)
    {
        // set the rowing state to be true for whichever seat is being sat in
        _rowingStates[seat] = stopStart;
        _rowingStatesDirection[seat] = backForward;
    }
}