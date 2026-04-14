using Godot;
using System;

public partial class Hud : CanvasLayer
{
	// Rename this to make more sense for your health bar
	private Control _boatHealthBar;
	private Label _fish;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TryResolveBoatHealthBar();

		_fish = GetNode<Label>("Fish");
		_fish.Visible = false;


		// Subscribe to the boat health update
		GlobalSignalServer.Instance.UpdateBoatHealth += UpdateBoatHealthUi;

		GlobalSignalServer.Instance.StartMotivator += OnStartMotivator;

		// Initialize with current health
		UpdateBoatHealthUi(GlobalSignalServer.Instance.Health);
	}

	public override void _ExitTree()
	{
		if (GlobalSignalServer.Instance != null)
		{
			GlobalSignalServer.Instance.UpdateBoatHealth -= UpdateBoatHealthUi;
			GlobalSignalServer.Instance.StartMotivator -= OnStartMotivator;
		}
	}

	// Updates the boat health ui
	private void UpdateBoatHealthUi(int newHealth)
	{
		if (!TryResolveBoatHealthBar())
		{
			return;
		}

		// Call the GDScript function we just made.
		// Argument 1: The target health (cast to float)
		// Argument 2: The duration of the animation in seconds (e.g., 0.5f)
		_boatHealthBar.Call("set_health_smoothly", (float)newHealth, 0.5f);
	}

	private bool TryResolveBoatHealthBar()
	{
		if (GodotObject.IsInstanceValid(_boatHealthBar))
		{
			return true;
		}

		_boatHealthBar = GetNodeOrNull<Control>("BoatHealthBar");
		return GodotObject.IsInstanceValid(_boatHealthBar);
	}

	private void OnStartMotivator()
	{
		if (Multiplayer.IsServer())
		{
			Rpc(nameof(ShowFishWarning));
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private async void ShowFishWarning()
	{
		if (_fish == null) return;
		
		// Show fish is coming for 1 second
		_fish.Visible = true;

		await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);

		if (GodotObject.IsInstanceValid(_fish))
		{
			_fish.Visible = false;
		}
	}
}