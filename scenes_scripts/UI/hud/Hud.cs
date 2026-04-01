using Godot;
using System;

public partial class Hud : CanvasLayer
{
	// Rename this to make more sense for your health bar
	private Control _boatHealthBar;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Grab the custom GDScript health bar node
		_boatHealthBar = GetNode<Control>("BoatHealthBar");

		// Subscribe to the boat health update
		GlobalSignalServer.Instance.UpdateBoatHealth += UpdateBoatHealthUi;
	}

	// Updates the boat health ui
	private void UpdateBoatHealthUi(int newHealth)
	{
		// Call the GDScript function we just made.
		// Argument 1: The target health (cast to float)
		// Argument 2: The duration of the animation in seconds (e.g., 0.5f)
		_boatHealthBar.Call("set_health_smoothly", (float)newHealth, 0.5f);
	}
}