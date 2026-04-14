using Godot;
using System;
using Waterways;

public partial class Motivator : Area3D
{
	[Export] public float Speed = 1.0f;
	[Export] public RiverManager RiverNode;

	private float _currentOffset = 0f;
	private bool _isMoving = false;

	public override void _Ready()
	{
		GlobalSignalServer.Instance.StartMotivator += OnStartMotivator;
		GlobalSignalServer.Instance.StopMotivator += OnStopMotivator;

		if (RiverNode != null && RiverNode.Curve != null)
		{
			Vector3 localPos = RiverNode.ToLocal(GlobalPosition);
			_currentOffset = RiverNode.Curve.GetClosestOffset(localPos);
		}

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isMoving && RiverNode != null && RiverNode.Curve != null)
		{
			_currentOffset += Speed * (float)delta;
			
			Vector3 localPoint = RiverNode.Curve.SampleBaked(_currentOffset);
			GlobalPosition = RiverNode.ToGlobal(localPoint);
		}
	}

	private void OnStartMotivator()
	{
		_isMoving = true;
	}

	private void OnStopMotivator()
	{
		_isMoving = false;
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