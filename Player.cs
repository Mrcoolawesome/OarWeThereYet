using Godot;
using System;
using System.Numerics;

public partial class Player : CharacterBody3D
{
  // How fast the player moves in m/s
  [Export]
  public int Speed { get; set; } = 14;

  // How fast the player falls in m/s^2
  [Export]
  public int FallAccel { get; set; } = 75;

  // Acceleration on the xz plane
  [Export]
  public float Accel { get; set; } = 10.0f;

  // Friction 
  [Export]
  public float Friction { get; set; } = 10.0f;

  // Camera
  [Export]
  public Camera3D Camera { get; set; }

  // Jump impulse in m/s
  public int JumpImpulse { get; set; } = 20;

  // player's velocity
  private Godot.Vector3 _playerVelocity = Godot.Vector3.Zero;

  // Do the physics stuff
  public override void _PhysicsProcess(double delta)
  {
    
    // stores the direction we're going
    Godot.Vector3 direction = Godot.Vector3.Zero;

    if (Input.IsActionPressed("move_forward")) {
      // movement in z direction
      direction.Z -= 1.0f;
    };
    if (Input.IsActionPressed("move_backward"))
    {
      // movement in -z direction
      direction.Z += 1.0f;
    } 
    if (Input.IsActionPressed("move_right"))
    {
      // movement in x direction
      direction.X += 1.0f;
    }
    if (Input.IsActionPressed("move_left"))
    {
      // movement in -x direction
      direction.X -= 1.0f;
    }

    // normalize the vector to be of length 1 if it's greater than zero
    // this is mainly for when they're pressing two buttons at once
    if (direction != Godot.Vector3.Zero)
    {
      direction = direction.Normalized();

      // make the character look in the normalized direction
      // we use the 'basis' property to set where they're looking
      // GetNode<Node3D>("Pivot").Basis = Camera.Basis;
    }

    // Momentum logic
    // Current velocity
    Godot.Vector2 currVelocity = new Godot.Vector2(_playerVelocity.X, _playerVelocity.Z);
    // Target velocity
    Godot.Vector2 targetVelocity = new Godot.Vector2(direction.X, direction.Z) * Speed;
    // targetVelocity = targetVelocity.Rotated(GlobalRotation.Y);

    // if the direction vector is nothing
    if (direction != Godot.Vector3.Zero)
    {
      // We're accelerating towards the target speed
      // We're using MoveTowards for lerping
      // currVelocity = currVelocity.MoveToward(targetVelocity, Accel * (float)delta);
    }
    else
    {
      // slow down
      // currVelocity = currVelocity.MoveToward(Godot.Vector2.Zero, Friction * (float)delta);
    }

    // Apply movement velocity (the ground is the XZ plane)
    _playerVelocity.X = targetVelocity.X;
    _playerVelocity.Z = targetVelocity.Y; // currVelcity is a Vector 2 so it only has X and Y

    // Gravity
    if (!IsOnFloor())
    {
      _playerVelocity.Y -= FallAccel * (float)delta;
    }

    // jump
    if (IsOnFloor() && Input.IsActionPressed("jump"))
    {
      _playerVelocity.Y = JumpImpulse;
    }

    _playerVelocity = _playerVelocity.Rotated(Godot.Vector3.Up,GlobalRotation.Y);

    GD.Print($"GlobalRotation.Y: {GlobalRotation.Y}");
    GD.Print($"player velocity {_playerVelocity}");

    // VERY IMPORTANT THIS ACTUALLY MOVES THE PLAYER
    Velocity = _playerVelocity;
    MoveAndSlide();
  }
}
