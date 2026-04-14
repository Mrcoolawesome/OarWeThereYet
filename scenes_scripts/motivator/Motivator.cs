using Godot;
using System;
using Waterways;

public partial class Motivator : Area3D
{
	[Export] public float Speed = 1.0f;
	[Export] public RiverManager RiverNode;

	[Export] public float CurrentOffset = 0f;

	private bool _isMoving = true;
	[Export] public bool IsMoving 
	{
		get => _isMoving;
		set 
		{
			if (_isMoving != value)
			{
				_isMoving = value;
				UpdateAnimationState();
			}
		}
	}

	private AnimationPlayer _animationPlayer;

	public override void _Ready()
	{
		GlobalSignalServer.Instance.StartMotivator += OnStartMotivator;
		GlobalSignalServer.Instance.StopMotivator += OnStopMotivator;

		if (RiverNode != null && RiverNode.Curve != null)
		{
			Vector3 localPos = RiverNode.ToLocal(GlobalPosition);
			CurrentOffset = RiverNode.Curve.GetClosestOffset(localPos);
		}

		BodyEntered += OnBodyEntered;

		// Assuming the fish.blend instance contains the AnimationPlayer
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("fish/AnimationPlayer");
		UpdateAnimationState();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (RiverNode == null || RiverNode.Curve == null) return;

		if (IsMoving && Multiplayer.IsServer())
		{
			CurrentOffset += Speed * (float)delta;
		}
		
		// 1. Update Position
		Vector3 localPoint = RiverNode.Curve.SampleBaked(CurrentOffset);
		GlobalPosition = RiverNode.ToGlobal(localPoint);

		// 2. Update Rotation (Look down the river)
		// Get the tangent (forward direction) at the current offset
		// SampleBakedWithRotation or just sampling a point slightly ahead works too
		Vector3 nextLocalPoint = RiverNode.Curve.SampleBaked(CurrentOffset + 0.1f);
		Vector3 nextGlobalPoint = RiverNode.ToGlobal(nextLocalPoint);
		
		// LookAt is a simple way to align the -Z axis with the target. 
		// We use GlobalPosition and nextGlobalPoint.
		if (GlobalPosition.DistanceSquaredTo(nextGlobalPoint) > 0.0001f)
		{
			LookAt(nextGlobalPoint, Vector3.Up);
		}
	}

	private void OnStartMotivator()
	{
		Visible = true;
		IsMoving = true;
	}

	private void OnStopMotivator()
	{
		Visible = false;
		IsMoving = false;
	}

	private void UpdateAnimationState()
	{
		if (_animationPlayer == null) return;

		if (IsMoving)
		{
			if (_animationPlayer.HasAnimation("Swim"))
			{
				_animationPlayer.Play("Swim");
			}
		}
		else
		{
			_animationPlayer.Stop();
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (!Multiplayer.IsServer()) return;

		if (body.Name == "Boat")
		{
			GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.ResetLevel));
		}
	}
}