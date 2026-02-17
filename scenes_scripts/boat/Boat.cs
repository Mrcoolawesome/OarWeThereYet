using Godot;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D
{
    //TODO: Change boat to face negative Z direction
    [Export] public RiverFloatSystem River;
    [Export] public float FloatForce = 1.0f;
    [Export] public float RiverSpeed = 1.0f;
    [Export] public float WaterDrag = 2.0f;
    [Export] public float RowForce = 10.0f;
    [Export] public float ImpactVelocityThreshold = 10.0f;
    [Export] public int MaxHealth = 100;
    [Export] public int ImpactDamage = 10;

    [Signal] public delegate void SeatEnteredEventHandler(Vector3 seatPosition);

    private Node3D _boatFloatProbesContainer;
    private Node3D _oarProbesContainer;
    private float _gravity;
    private Vector3 _collisionObjectPosition; // position of the object that is colliding with us
    private Node _expectedCollisionObject; // collision object given when an object enters our collision
    // THIS NEVER GETS RESET WHICH IS PROBABLY BAD
    private Health _healthComponent = new Health();

    // booleans to apply rowing force to specific spots on the boat
    private bool[] _rowingStates = new bool[4]; // state to say if one of the oars is rowing or not
    private bool[] _rowingStatesDirection = new bool[4]; // direction of rowing: backward = false | forward = true

    // boat reset position and rotation
	private Vector3 _boatResetPosition = new Vector3(0.0f, 0.0f, -8.0f);
	private Vector3 _boatResetRotation = new Vector3(0.0f, Mathf.DegToRad(90), 0.0f);


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
        _healthComponent.Name = "HealthComponent"; 
        AddChild(_healthComponent);

        // subscribe to the Rowing signal from the singleton script
        GlobalSignalServer.Instance.Rowing += OnPlayerRowing;

        // initalize the health stuff
        _healthComponent.Initalize(MaxHealth); // initalize with 100 health

        // listen to the health changed signal sent from the health component
        // AnnounceHealthUpdate will send a signal to the server that updates the ui to show the new health
        _healthComponent.HealthChanged += AnnounceHealthUpdate;

        // announce if the boat died
        _healthComponent.Die += AnnounceDeath;

        // need to enable these things to enable collision detection
        ContactMonitor = true;
        MaxContactsReported = 5;
    }

    // physics process along with all its associated functions
    public override void _PhysicsProcess(double delta)
    {
        // do the math to make the probes float
        ProbeBouyancyPhysicsProcess();

        // apply the rowing forces if the player is rowing
        ApplyRowingForcePhysicsProcess();
    }

    // does the bouyancy stuff for the probes
    private void ProbeBouyancyPhysicsProcess()
    {
        foreach (Marker3D probe in _boatFloatProbesContainer.GetChildren().OfType<Marker3D>())
        {
            Vector3 globalPos = probe.GlobalPosition;
            Vector3 relativePos = probe.GlobalPosition - GlobalPosition;
            Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);

            float waterHeight = River.GetWaterHeight(globalPos);
            float depth = waterHeight - globalPos.Y;
            float buoyancyMultiplier = 2 - Mathf.Exp(-depth + 0.6f);
            Vector3 buoyancyForce = WaterNormal(globalPos) * _gravity * FloatForce * buoyancyMultiplier;

            Vector3 currentVelocity = LinearVelocity + AngularVelocity.Cross(relativePos);
            Vector3 frictionForce = -currentVelocity * depth * WaterDrag;

            if (depth > 0)
            {
                ApplyForce(buoyancyForce + frictionForce, relativePos);
                ApplyForce(flowDirection * RiverSpeed, relativePos);
            }
        }
    }

    // function to get the normal vector of the water at a given point
    private Vector3 WaterNormal(Vector3 globalPos)
    {
        Vector3 flowDirection = River.GetWaterFlowDirection(globalPos);
        Vector3 waterRight = flowDirection.Cross(Vector3.Up);
        return waterRight.Cross(flowDirection);
    }

    // does all the rowing force stuff for when a player is rowing
    private void ApplyRowingForcePhysicsProcess()
    {
        // i don't think we need all that OfType stuff as so long as we don't add anything that isn't a marker3d in this container which we shouldn't
        foreach (Marker3D probe in _oarProbesContainer.GetChildren().OfType<Marker3D>())
        {
            // this value is the relative position to the boat
            Vector3 relativePosition = probe.GlobalPosition - GlobalPosition; // im making this into a new variable so it's more clear on what it is
            // get the forward direction of the boat
            Vector3 forwardDirection = GlobalBasis.X;
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

            // only apply the force if the probe (oar) is in the water
            float riverHeight = River.GetWaterHeight(probe.GlobalPosition);
            float depth = riverHeight - probe.GlobalPosition.Y;
            if (depth > 0)
            {
                ApplyForce(finalForce, relativePosition); // finally apply the force
            }
        }
    }

    // all the code associated with doing anything not needed to be done on the physics time
    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    // getting the signals to row forward or to stop
    // the parameter types of this function MUST be identical to the Rowing signal from the singleton server/script
    // needs to be an RPC call so the server knows to update the states
    // CallLocal is false, because if it were true then the function would run on the peer and not the server, which is not what we want
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)] 
    private void OnPlayerRowing(int seat, bool stopStart, bool backForward)
    {
        // set the rowing state to be true for whichever seat is being sat in
        _rowingStates[seat] = stopStart;
        _rowingStatesDirection[seat] = backForward;
    }

    public void Reset()
	{
		// Only the server should issue this command
		if (Multiplayer.IsServer())
		{
			// Tell EVERYONE (including the server) to run the SyncReset function
			RpcId(1, nameof(SyncReset));
		}
	}

	// reset function that gets called by the level script
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)] // only update the server so the CallLocal should be false i think
	private void SyncReset()
	{
		// set the player into the standing state and reset their position and velocity
		_rowingStates = [false, false, false, false];
		GlobalPosition = _boatResetPosition;
		GlobalRotation = _boatResetRotation;
		LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
	}

    // we need to use integrate forces to get the exact position of the colliding object 
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // only the server can do boat health stuff 
        if (Multiplayer.IsServer())
        {
            // get the position of the object that just entered
            for (int i = 0; i < state.GetContactCount(); i++)
            {
                // if we're colliding with a player, ignore them
                if (state.GetContactColliderObject(i) is CharacterBody3D)
                {
                    continue;
                }

                // get the impact velocity at the collision point
                float impactVelocity = state.GetContactLocalVelocityAtPosition(i).Length();

                // if the impact velocity at that point is greater than the threshold then remove health points from the boat health
                if (impactVelocity > ImpactVelocityThreshold)
                {
                    // update our health, this automatically sends out a signal that the health has been updated
                    _healthComponent.UpdateHealth(-ImpactDamage);
                }
            }
        }
    }
    // Emitted when the health changes. triggered by the health component's signal
    // this is just to update the ui, the health component already updates everyone's local health variables automatically
    public void AnnounceHealthUpdate(int newHealth)
    {
        // announce this update to the signal server to update the ui
        GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.UpdateBoatHealth), newHealth);
    }

    // announces to the signal server that the boat died so the ui can update
    // this is triggered by a signal sent from the health component that's connected in the _Ready function of this code
    public void AnnounceDeath()
    {
        // announce the death to the global signal server
        GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.BoatDeath));
    }
}