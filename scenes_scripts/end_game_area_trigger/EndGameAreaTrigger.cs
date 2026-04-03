using Godot;
using System;

public partial class EndGameAreaTrigger : Area3D
{
  public override void _Ready()
  {
    // Connect the body entered signal
    BodyEntered += OnBodyEntered;
  }

  private void OnBodyEntered(Node3D body)
  {
		GD.Print("HELLO");
    // Make sure it is actually the boat hitting the trigger!
    if (body.Name == "Boat" || body is Boat)
    {
			GD.Print("BOAT");
      // Safely turn off monitoring so this area can't trigger the signal twice
      SetDeferred("monitoring", false);
      
      // Fire the global signal!
      GlobalSignalServer.Instance.EmitSignal(GlobalSignalServer.SignalName.EndGame);
    }
  }
}