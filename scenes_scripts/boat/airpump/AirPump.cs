using Godot;
using System;

public partial class AirPump : StaticBody3D, Interactable
{
	[Export] public string PromptMessage { get; set; } = "Pump Air";
  [Export] public int HealRate = 1;
	public string PromptInput { get; set; } = "action_key";

  private bool _isPumping = false;
  private double _timer = 0.0;
  private Boat _boat;

  public override void _Ready()
  {
      _boat = GetParent<Boat>();
  }

  public override void _Process(double delta)
  {
      if (!Multiplayer.IsServer()) return;
      if (!_isPumping) return;

      if ((_timer += delta) >= 1.0)
      {
          _timer -= 1.0;
          _boat.HealthComponent.UpdateHealth(HealRate);
      }
  }

	public void Interact(Player player)
	{
	}

  public void StartInteract(Player player)
  {
      RpcId(1, nameof(SetPumping), true);
  }

  public void StopInteract(Player player)
  {
      RpcId(1, nameof(SetPumping), false);
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void SetPumping(bool pumping)
  {
      if (Multiplayer.IsServer())
      {
          _isPumping = pumping;
      }
  }
}
