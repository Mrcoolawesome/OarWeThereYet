using Godot;
using System;

public partial class Hud : CanvasLayer
{
	// the label that shows the boat health
	Label boatHealthLabel;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// set the boat health label
		boatHealthLabel = GetNode<Label>("BoatHealthLabel");

		// subscribe to the boat health update
		GlobalSignalServer.Instance.UpdateBoatHealth += UpdateBoatHealthUi;
		// subscribe to the boat dying
		GlobalSignalServer.Instance.BoatDeath += DeathScreen;
	}

	// updates the boat health ui
	private void UpdateBoatHealthUi(int newHealth)
	{
		boatHealthLabel.Text = $"Boat Health: {newHealth}";
	}

	// set the death screen
	private void DeathScreen()
	{
		boatHealthLabel.Text = "boat ded";
	}
}
