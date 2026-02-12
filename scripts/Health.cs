using Godot;
using System;
using System.Reflection;

[GlobalClass] // this marks it as an actual node
public partial class Health : Node
{
	// signal for when health changes for the implementing class to use to know when to send another signal from themselves if needed
	[Signal]
	public delegate void HealthChangedEventHandler(int newHealth); // healthChange can be + or - to change the overall health
	// signal for when the player dies
	[Signal]
	public delegate void DieEventHandler();

	// want the max health to be variable depending on the thing inherting this 
	private int _maxHealth = 100; // 100 hp by default

	// actual health value
	private int _currHealth;

	// We don't use _Ready anymore, we use this initalize function first to connect the signal from the damaging/healing object
	public void Initalize(int maxHealth)
	{
		// set the max health
		_maxHealth = maxHealth;
	}

	// this can take a + or - value to change their health
	// TODO: make this work with mutliplayer with rpc calls probably
	public void UpdateHealth(int healthChange)
	{
		// update their health
		_currHealth += healthChange;

		// check if they're out of health
		if (_currHealth <= 0)
		{
			EmitSignal(nameof(Die));
			_currHealth = 0; // just set their health to zero so it doesn't show up as negative in the ui
		}
		
		// send out the signal to say that their health has changed with their new health amount
		EmitSignal(nameof(HealthChanged), _currHealth);
	}
}
