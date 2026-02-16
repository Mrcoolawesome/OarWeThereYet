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

  public override void _Ready()
  {
    // if we're a client that's just connecting, request the current health from the server
		if (!Multiplayer.IsServer())
		{
			// send a request to the server
			RpcId(1, nameof(RequestHealthSync));
		}
  }

	// We don't use _Ready anymore, we use this initalize function first to connect the signal from the damaging/healing object
	public void Initalize(int maxHealth)
	{
		// set the max health
		_maxHealth = maxHealth;
		_currHealth = maxHealth;
	}

	// this gets ran as soon as someone connects
  private void OnPeerConnected(long id)
  {
		// send the rpc call to the specific client that just joined so they're up-to-date using the current health
    RpcId(id, nameof(SyncHealth), _currHealth);
  }

	// if they're the server, update the specific client
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestHealthSync()
	{
		if (Multiplayer.IsServer())
		{
			// Find out which client just asked us for the health
			long senderId = Multiplayer.GetRemoteSenderId();
			
			// Send the health ONLY to that specific client
			RpcId(senderId, nameof(SyncHealth), _currHealth);
		}
	}

	// only runs on the newly joined client
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void SyncHealth(int startingHealth)
	{
		// update the client's local boat health
		_currHealth = startingHealth;
    // Check if the boat was already dead before they joined
    if (_currHealth <= 0)
    {
        _currHealth = 0;
        EmitSignal(nameof(Die));
    }

    // Force their local UI to update immediately
    EmitSignal(nameof(HealthChanged), _currHealth);
	}

	// this can take a + or - value to change their health
	public void UpdateHealth(int healthChange)
	{
		Rpc(nameof(SyncUpdateHealth), healthChange);
	}

	// want this to sync everywhere, but only the server can do it hence why we set the mode to 'RpcMode.Authority'
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void SyncUpdateHealth(int healthChange)
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
