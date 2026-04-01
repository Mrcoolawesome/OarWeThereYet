using Godot;
using System;

public partial class AirPump : StaticBody3D, Interactable
{
    [Export] public string PromptMessage { get; set; } = "Pump Air";
    [Export] public int HealRate = 3;
    public string PromptInput { get; set; } = "action_key";

    private bool _isPumping = false;
    private double _timer = 0.0;
    private Boat _boat;
    private AnimationPlayer _animationPlayer;

    public override void _Ready()
    {
        _boat = GetParent<Boat>();
        _animationPlayer = GetNode("airPump").GetNode<AnimationPlayer>("AnimationPlayer");
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        if (!_isPumping) return;

        if ((_timer += delta) >= 2.0)
        {
            _timer -= 2.0;
            _boat.HealthComponent.UpdateHealth(HealRate);
        }
    }

    public void Interact(Player player)
    {
    }

    public void StartInteract(Player player)
    {
        Rpc(nameof(SetPumping), true);
    }

    public void StopInteract(Player player)
    {
        Rpc(nameof(SetPumping), false);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SetPumping(bool pumping)
    {
        _isPumping = pumping;

        if (pumping)
        {
            if (_animationPlayer.HasAnimation("pumping"))
            {
                _animationPlayer.Play("pumping");
            }
        }
        else
        {
            _animationPlayer.Stop();
        }
    }
}
