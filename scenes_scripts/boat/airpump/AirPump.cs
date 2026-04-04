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
    private AudioStreamPlayer3D _pumpingAudio;

    public override void _Ready()
    {
        _boat = GetParent<Boat>();
        _animationPlayer = GetNode("airPump").GetNode<AnimationPlayer>("AnimationPlayer");
        _pumpingAudio = GetNode<AudioStreamPlayer3D>("PumpingIt");
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.IsServer()) return;
        if (!_isPumping) return;

        // Note: The animation is playing 3x faster now, but the boat will 
        // still only heal every 2.0 seconds based on this timer.
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
            // Play the animation at 3x speed (-1 is the default blend time)
            if (_animationPlayer.HasAnimation("pumping"))
            {
                _animationPlayer.Play("pumping", -1, 5.0f);
            }

            // Play the pumping audio if it isn't already playing
            if (!_pumpingAudio.Playing)
            {
                _pumpingAudio.Play();
            }
        }
        else
        {
            // Stop both the animation and the audio when the player lets go
            _animationPlayer.Stop();
            _pumpingAudio.Stop();
        }
    }
}