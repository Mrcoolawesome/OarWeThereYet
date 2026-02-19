using Godot;

// this was heavily inspired by these two links:
// https://forum.godotengine.org/t/jittery-rigidbody-movement-on-client-side/38377/3
// https://gafferongames.com/categories/networked-physics/

public interface ISyncBuffer
{
  // Define the property signature. 
  // We use Godot.Collections.Array holding Variants to allow mixed data types.
  // this will contain things in this order:
  // frameCount, position, quaternionRotation, LinearVelocity, AngularVelocity
  Godot.Collections.Array<Variant> State { get; set; } // THIS NEEDS TO BE AN EXPORTED VARIABLE THAT'S SYNCED WITH A SYNCHRONIZER

  int PhysicsFrameCount { get; set; } // local frame count variable

  // set the state array if you are the host
  void SetStateArray();

  // apply the state array if you're a client
  void ApplyStateArray();

  // make sure there's at least two states in the buffer at a time
  void MaintainBuffer();

  // do the movement for client side
  void MoveAndLerp();

  // is on the same frame 
}