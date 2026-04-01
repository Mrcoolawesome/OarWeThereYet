using Godot;
using Godot.Collections;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;
using System.Linq;
using Waterways;

public partial class Boat : RigidBody3D, ISyncBuffer
{
    [Export] public RiverFloatSystem River;
    [Export] public float FloatForce = 1.0f;
    [Export] public float RiverSpeed = 1.0f;
    [Export] public float WaterDrag = 2.0f;
    [Export] public float RowForce = 10.0f;
    [Export] public float ImpactVelocityThreshold = 10.0f;
    [Export] public int MaxHealth = 100;
    [Export] public int ImpactDamage = 10;
    [Export] public Array<Variant> State {get; set;} // position, quaternionRotation, LinearVelocity, AngularVelocity
    [Export] public float LerpSpeed = 1.0f;
    // the reset position and rotation for the boat
    [Export] public Vector3 BoatResetPosition = new Vector3(0.0f, 0.0f, 0.0f);
    [Export] public Vector3 BoatResetRotation = new Vector3(0.0f, 0.0f, 0.0f);
    
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
    public bool[] OccupiedSeats = new bool[4];
    public bool[] HasOarInSeat = new bool[4];

    // boolean for checking if a reset is pending
    private bool _resetPending = false;

    // new state that the boat should be set to
    private Transform3D _newPositionState;
    // new rotation state
    private Basis _newRotationState;

    // booleans for making sure we only apply the new state once for client side stuff
    private bool _applyNewPositionState = false;
    private bool _applyNewRotationState = false;
    private bool _applyNewVelocityState = false;

    // boolean for checking if the person spawning this instance is a client or the host
    private bool _clientSpawning = false;

    // timer for not letting the boat take damage for a specified amount of time after getting hit
    private Timer _damageDelayTimer = new Timer();
    // boolean to act as a gate to allow for more damage to be taken
    private bool _damageAllowed = true;

    // get all the oar objects
    private Node3D _frontRightOar;
    private Node3D _frontLeftOar;
    private Node3D _backRightOar;
    private Node3D _backLeftOar;

    public AnchorPoint AnchorPoint;

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
        _damageDelayTimer = GetNode<Timer>("DamageDelayTimer");
        AddChild(_healthComponent);

        // get all the oars
        _backLeftOar = GetNode<Node3D>("OarsContainer/OarBackLeft");
        _backRightOar = GetNode<Node3D>("OarsContainer/OarBackRight");
        _frontRightOar = GetNode<Node3D>("OarsContainer/OarFrontRight");
        _frontLeftOar = GetNode<Node3D>("OarsContainer/OarFrontLeft");
        AnchorPoint = GetNode<AnchorPoint>("AnchorPoint");

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

        // make the boat reset rotation in radians
        BoatResetRotation = new Vector3(
            Mathf.DegToRad(BoatResetRotation.X),
            Mathf.DegToRad(BoatResetRotation.Y),
            Mathf.DegToRad(BoatResetRotation.Z)
        );
        GlobalRotation = BoatResetRotation;

        // set the state if we're the server
        SetStateArray();

        // set if this instance is being made by a client
        _clientSpawning = !Multiplayer.IsServer(); 
    }

    // physics process along with all its associated functions
    public override void _PhysicsProcess(double delta)
    {
        // do the math to make the probes float
        ProbeBouyancyPhysicsProcess();

        // apply the rowing forces if the player is rowing
        ApplyRowingForcePhysicsProcess();
    }

  public override void _Process(double delta)
  {
    ChangeOarVisibiltiy();
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

    private void ChangeOarVisibiltiy()
    {
        int i = 0;
        foreach (bool seatHasOar in HasOarInSeat)
        {
            // hide the given oar 
            SeatIndicies convertedSeat = (SeatIndicies)i;
            if (convertedSeat == SeatIndicies.FrontLeft)
            {
                _frontLeftOar.Visible = seatHasOar;
            } else if (convertedSeat == SeatIndicies.FrontRight)
            {
                _frontRightOar.Visible = seatHasOar;
            } else if (convertedSeat == SeatIndicies.BackRight)
            {
                _backRightOar.Visible = seatHasOar;
            } else if (convertedSeat == SeatIndicies.BackLeft)
            {
                _backLeftOar.Visible = seatHasOar;
            }

            i++;
        }
    }

    public void Reset()
	{
		// Only the server should issue this command
		if (Multiplayer.IsServer())
		{
			// Tell EVERYONE (including the server) to run the SyncReset function
			Rpc(nameof(SyncReset));
		}
	}

	// reset function that gets called by the level script
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)] // only update the server so the CallLocal should be false i think
	private void SyncReset()
	{
		// set the player into the standing state and reset their position and velocity
		_rowingStates = [false, false, false, false];

        // Reset occupied seats
        OccupiedSeats = [false, false, false, false];
        
        // Reset Oar visuals
        HasOarInSeat = [false, false, false, false];

        AnchorPoint.ResetAnchor();

        // reset the boat health
        _healthComponent.ResetHealth();

        _resetPending = true; // need to do the reset in the integrate forces function so that you don't have to spam the reset button to get the boat to respawn
	}

    // we need to use integrate forces to get the exact position of the colliding object 
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // If the peer is missing, OR if the ENet socket is closed/disconnected, bail out immediately!
        if (Multiplayer.MultiplayerPeer == null || 
            Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected) 
        {
            return;
        }

        // Process Pending Resets FIRST
        if (_resetPending)
        {
            // Force the velocities to zero
            state.LinearVelocity = Vector3.Zero;
            state.AngularVelocity = Vector3.Zero;

            // Force the transform to the spawn point
            Basis resetBasis = new Basis(Quaternion.FromEuler(BoatResetRotation));
            state.Transform = new Transform3D(resetBasis, BoatResetPosition);

            // Turn off all active network lerping so the boat doesn't try to slide back
            _applyNewPositionState = false;
            _applyNewRotationState = false;
            _applyNewVelocityState = false;

            _resetPending = false;
            
            // If we are the server, immediately update the array with the new 0,0,0 data
            if (Multiplayer.IsServer())
            {
                SetStateArray(); 
            }
            
            return; // Skip the rest of the physics step for this frame
        }
        
        // only the server can do boat health stuff 
        if (Multiplayer.IsServer())
        {
            // update the state table
            SetStateArray();
            // get the position of the object that just entered
            for (int i = 0; i < state.GetContactCount(); i++)
            {
                // if we're colliding with a player, ignore them
                if (state.GetContactColliderObject(i) is CharacterBody3D || state.GetContactColliderObject(i) is UniversalInWorld)
                {
                    continue;
                }

                // get the impact velocity at the collision point
                float impactVelocity = state.GetContactLocalVelocityAtPosition(i).Length();

                // if the impact velocity at that point is greater than the threshold then remove health points from the boat health
                if (impactVelocity > ImpactVelocityThreshold && _damageAllowed) // damageAllowed is switched to true when the _damageDelayTimer is done
                {
                    // update our health, this automatically sends out a signal that the health has been updated
                    _healthComponent.UpdateHealth(-ImpactDamage);

                    // damage is no longer allowed until the timer ends
                    _damageAllowed = false;

                    // also start the delay timer so they don't take damage during this time
                    _damageDelayTimer.Start(); // the delay time is set in the timer node in godot (you can also set it (the time delay) here but i didn't)
                }
            }
        } 
        else // otherwise do client syncing stuff
        {
            // first check if the client is awaiting the first known position given by the server to spawn the boat at
            if (_clientSpawning && _applyNewPositionState) // position is the most important thing, the others will sync
            {
                // teleport to inital position given by the host
                state.Transform = _newPositionState;
                _clientSpawning = false; // we're done with the inital spawn of the boat

                // Turn off the lerp flags so we don't accidentally run the lerp code below!
                _applyNewPositionState = false;
                _applyNewRotationState = false;
            }
            
            // get the 'speed' at which we lerp at 
            float weight = state.Step * LerpSpeed; // state.Step is like the 'delta' parameters given from Process
            // apply the updated state variable if any changes were made
            if (_applyNewPositionState)
            {
                // interpolate to the new position
                state.Transform = state.Transform.InterpolateWith(_newPositionState, weight);
                
                // Only turn off the flag once we are practically touching the target
                // we have to do this because lerping won't instantly snap us to the target postition, so we need to keep going until we're basically right next to it
                if (state.Transform.Origin.DistanceTo(_newPositionState.Origin) < 0.05f)
                {
                    // state.Transform = _newPositionState; // Snaps the final microscopic distance - i don't like this becuase it makes it look like jitter is happening, within 0.05m of the host is close enough
                    _applyNewPositionState = false;      // NOW we stop lerping
                }
            }

            // apply the updated/corrected rotation state if needed
            if (_applyNewRotationState)
            {
                // interpolate to the new rotation (this was written by gemini but it's prolly fine)
                Quaternion currentRot = state.Transform.Basis.GetRotationQuaternion();
                Quaternion targetRot = _newRotationState.GetRotationQuaternion(); // Assuming _newRotationState is a Basis
                
                // Slerp (Spherical Linear Interpolation) calculates the smooth rotation
                Quaternion smoothRot = currentRot.Slerp(targetRot, weight);
                
                Vector3 currentPosition = state.Transform.Origin;
                state.Transform = new Transform3D(new Basis(smoothRot), currentPosition);
                
                
                // Only turn off the flag once the rotational difference is tiny
                if (Mathf.Abs(currentRot.AngleTo(targetRot)) < 0.01f)
                {
                    _applyNewRotationState = false;
                }
            }

            // apply the velocity state if needed
            // don't really need to lerp velocity since it doesn't change a significant enough amount i think
            if (_applyNewVelocityState)
            {
                state.LinearVelocity = (Vector3)State[2];
                state.AngularVelocity = (Vector3)State[3];
                _applyNewVelocityState = false;
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

    // this should only be ran on the server
    public void SetStateArray()
    {
        if (Multiplayer.IsServer())
        {
            State = [Position, Quaternion, LinearVelocity, AngularVelocity];
        }
    }

    // this is only ever called when the 'delta_synchronize()' call comes from the multiplayer spawner
    // we use the 'delta_synchronize()' function because we set the synchronizer to synchronize the State array only on change, if we switched it to update 'always' we'd have to use the 'synchronize()' signal
    public void SyncPosIfNeeded()
    {
        // only ran client side
        if (!Multiplayer.IsServer())
        {
            // Syncing the rotation:
            // make a new basis to use to transform the boat position
            // set to the current rotation by default because we only wanna change it if we're within a threshold
            // get synced rotation and curr rotation
            Quaternion syncedRotation = (Quaternion)State[1];
            Quaternion currRotation = Quaternion;
            // if the difference is greater than some threshold then lerp
            if (Mathf.Abs(currRotation.AngleTo(syncedRotation)) > Mathf.DegToRad(1.0f)) // if the difference is greater than a degree than sync
            {
                // 'return' the rotation state
                _newRotationState = new Basis(syncedRotation);
                _applyNewRotationState = true;
            }

            // Syncing the position:
            // get the synced position
            Vector3 syncedPosition = (Vector3)State[0];
            // difference between our client side position and the host's position
            float posDiff = (syncedPosition - Position).Length();
            // make a new transform, that just uses our current position by default and the synced position if we've deviated too far
            // if it's greater than 0.5 meters apart then lerp ours to the hosts' position
            if (posDiff > 0.5f)
            {
                // this is where the new transformation is 'returned'
                _newPositionState = new Transform3D(Basis, syncedPosition);
                _applyNewPositionState = true;

                // Force the flag back to true so the boat snaps instantly instead of lerping!
                if (posDiff > 10.0f) 
                {
                    _clientSpawning = true;
                }
            }

            // we want this to just always happen when we're updated
            _applyNewVelocityState = true;
        }
    }

    // triggered when the damage delay timer ends
    public void DamageTimerEnded()
    {
        // allow for damage to be taken again
        _damageAllowed = true;
    }

    public int NextAvailableSeat()
    {
        for (int i = 0; i < OccupiedSeats.Length; i++)
        {
            if (!OccupiedSeats[i]) return i;
        }

        return -1;
    }
    
    public bool IsSeatAvailable(int seat)
    {
        return !OccupiedSeats[seat];
    }
}
