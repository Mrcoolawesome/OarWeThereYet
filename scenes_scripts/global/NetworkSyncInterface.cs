using Godot;

// this was heavily inspired by these two links:
// https://forum.godotengine.org/t/jittery-rigidbody-movement-on-client-side/38377/3
// https://gafferongames.com/categories/networked-physics/

public interface ISyncBuffer
{
  // Define the property signature. 
  // We use Godot.Collections.Array holding Variants to allow mixed data types.
  // this will contain things in this order:
  // position, quaternionRotation, LinearVelocity, AngularVelocity
  Godot.Collections.Array<Variant> State { get; set; } // THIS NEEDS TO BE AN EXPORTED VARIABLE THAT'S SYNCED WITH A SYNCHRONIZER

  // set the state array if you are the host
  void SetStateArray();

  // this is only ever called when the 'delta_synchronize()' call comes from the multiplayer spawner
  // we use the 'delta_synchronize()' function because we set the synchronizer to synchronize the State array only on change, if we switched it to update 'always' we'd have to use the 'synchronize()' signal
  void SyncPosIfNeeded();
}