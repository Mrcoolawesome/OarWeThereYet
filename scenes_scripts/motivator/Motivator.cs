using Godot;
using System;
using System.Collections.Generic;
using Waterways;

public partial class Motivator : Area3D
{
  [Export] public float Speed = 1.0f;
  [Export] public RiverManager RiverNode;

  [Export] public float CurrentOffset = 0f;

  [ExportGroup("Audio Scaling Settings")]
  [Export] public float ScaleMultiplier = 15.0f; 
  
  public float _targetScale = 1.0f;
  private float _currentScale = 1.0f;
  private int _busIndex;

  // meshes
  private MeshInstance3D _body;
  private MeshInstance3D _eyesWhiteLeft;
  private MeshInstance3D _eyesWhiteRight;
  private MeshInstance3D _eyesPupilLeft;
  private MeshInstance3D _eyesPupilRight;
  private MeshInstance3D _eyebrows;

  private bool _isMoving = true;
  private bool _subscribedToMotivatorSignals = false;
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
  private AudioStreamPlayer3D _doom;

  private const double AudioSyncRetryIntervalSeconds = 0.35;
  private readonly Dictionary<long, int> _audioSyncAckByPeer = new Dictionary<long, int>();
  private int _audioSyncSequence = 0;
  private bool _desiredDoomPlaying = false;
  private double _audioSyncRetryTimer = 0.0;
  private bool _audioSyncAwaitingAck = false;

  public override void _EnterTree()
  {
    if (_subscribedToMotivatorSignals || !IsInstanceValid(GlobalSignalServer.Instance))
    {
      return;
    }

    GlobalSignalServer.Instance.StartMotivator += OnStartMotivator;
    GlobalSignalServer.Instance.StopMotivator += OnStopMotivator;
    _subscribedToMotivatorSignals = true;
  }

  public override void _Ready()
  {
    if (RiverNode != null && RiverNode.Curve != null)
    {
      Vector3 localPos = RiverNode.ToLocal(GlobalPosition);
      CurrentOffset = RiverNode.Curve.GetClosestOffset(localPos);
    }

    BodyEntered += OnBodyEntered;

    _animationPlayer = GetNodeOrNull<AnimationPlayer>("fish/AnimationPlayer");

    _doom = GetNode<AudioStreamPlayer3D>("Doom");

    // Get the audio bus index for our private Doom bus
    _busIndex = AudioServer.GetBusIndex("Doom");

    // get the meshes
    _body = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/Cube");
    _eyesWhiteLeft = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/EyeWhiteLeft");
    _eyesWhiteRight = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/EyeWhiteRight"); 
    _eyesPupilLeft = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/EyePupilLeft");
    _eyesPupilRight = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/EyePupilRight");
    _eyebrows = GetNode<MeshInstance3D>("fish/Armature/Skeleton3D/Plane");

    Visible = false;
    IsMoving = false;
    _targetScale = 1.0f;
    _currentScale = 1.0f;
    UpdateAnimationState();

    if (Multiplayer.IsServer())
    {
      Multiplayer.PeerConnected += OnPeerConnected;
    }
  }

  public override void _ExitTree()
  {
    if (_subscribedToMotivatorSignals && IsInstanceValid(GlobalSignalServer.Instance))
    {
      GlobalSignalServer.Instance.StartMotivator -= OnStartMotivator;
      GlobalSignalServer.Instance.StopMotivator -= OnStopMotivator;
    }

    _subscribedToMotivatorSignals = false;

    if (Multiplayer.IsServer())
    {
      Multiplayer.PeerConnected -= OnPeerConnected;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (RiverNode == null || RiverNode.Curve == null) return;

    if (Multiplayer.IsServer())
    {
      TickAudioSyncRetry(delta);
    }

    if (IsMoving && Multiplayer.IsServer())
    {
      CurrentOffset += Speed * (float)delta;
    }
    
    Vector3 localPoint = RiverNode.Curve.SampleBaked(CurrentOffset);
    GlobalPosition = RiverNode.ToGlobal(localPoint);

    Vector3 nextLocalPoint = RiverNode.Curve.SampleBaked(CurrentOffset + 0.1f);
    Vector3 nextGlobalPoint = RiverNode.ToGlobal(nextLocalPoint);
    
    if (GlobalPosition.DistanceSquaredTo(nextGlobalPoint) > 0.0001f)
    {
      LookAt(nextGlobalPoint, Vector3.Up);
    }

    MeshAnimationPhysicsProcess(delta);
  }

  private void MeshAnimationPhysicsProcess(double delta)
  {
    // If the doom sound is playing, calculate the loudness from its isolated bus
    if (_doom != null && _doom.Playing)
    {
      float volumeDb = AudioServer.GetBusPeakVolumeLeftDb(_busIndex, 0);
      float loudness = Mathf.DbToLinear(volumeDb);
      
      float newTargetScale = 1.0f + (loudness * ScaleMultiplier);

      if (newTargetScale > _targetScale)
      {
        _targetScale = newTargetScale;
      }
    }

    _currentScale = Mathf.Lerp(_currentScale, _targetScale, (float)delta * 20.0f);
    
    Vector3 newScale = new Vector3(_currentScale, 1.0f, _currentScale);
    
    // Apply to the meshes
    if (_body != null) _body.Scale = newScale;
    if (_eyesWhiteLeft != null) _eyesWhiteLeft.Scale = newScale;
    if (_eyesWhiteRight != null) _eyesWhiteRight.Scale = newScale;
    if (_eyesPupilLeft != null) _eyesPupilLeft.Scale = newScale;
    if (_eyesPupilRight != null) _eyesPupilRight.Scale = newScale;
    if (_eyebrows != null) _eyebrows.Scale = newScale;

    _targetScale = Mathf.Lerp(_targetScale, 1.0f, (float)delta * 10.0f);
  }

  private void OnStartMotivator()
  {
    Visible = true;
    IsMoving = true;
    _targetScale = Mathf.Max(_targetScale, 1.0f);

    if (Multiplayer.IsServer())
    {
      StartAudioSync(true);
    }
    else
    {
      SetDoomPlaying(true);
    }
  }

  private void OnStopMotivator()
  {
    Visible = false;
    IsMoving = false;
    _targetScale = 1.0f;
    _currentScale = 1.0f;

    if (Multiplayer.IsServer())
    {
      StartAudioSync(false);
    }
    else
    {
      SetDoomPlaying(false);
    }
  }

  private void StartAudioSync(bool shouldPlay)
  {
    if (!Multiplayer.IsServer())
    {
      return;
    }

    _desiredDoomPlaying = shouldPlay;
    _audioSyncSequence++;
    _audioSyncRetryTimer = 0.0;
    _audioSyncAwaitingAck = true;

    SetDoomPlaying(shouldPlay);
    SendAudioSyncToPendingPeers();
  }

  private void TickAudioSyncRetry(double delta)
  {
    if (!_audioSyncAwaitingAck)
    {
      return;
    }

    if (AllPeersAckedCurrentAudioSync())
    {
      _audioSyncAwaitingAck = false;
      return;
    }

    _audioSyncRetryTimer += delta;
    if (_audioSyncRetryTimer < AudioSyncRetryIntervalSeconds)
    {
      return;
    }

    _audioSyncRetryTimer = 0.0;
    SendAudioSyncToPendingPeers();
  }

  private bool AllPeersAckedCurrentAudioSync()
  {
    foreach (long peerId in Multiplayer.GetPeers())
    {
      if (!_audioSyncAckByPeer.TryGetValue(peerId, out int ackedSequence) || ackedSequence != _audioSyncSequence)
      {
        return false;
      }
    }

    return true;
  }

  private void SendAudioSyncToPendingPeers()
  {
    foreach (long peerId in Multiplayer.GetPeers())
    {
      if (_audioSyncAckByPeer.TryGetValue(peerId, out int ackedSequence) && ackedSequence == _audioSyncSequence)
      {
        continue;
      }

      RpcId(peerId, nameof(ReceiveDoomAudioState), _audioSyncSequence, _desiredDoomPlaying);
    }
  }

  private void SetDoomPlaying(bool shouldPlay)
  {
    if (_doom == null)
    {
      return;
    }

    if (shouldPlay)
    {
      if (!_doom.Playing)
      {
        _doom.Play();
      }
      return;
    }

    _doom.Stop();
  }

  private void OnPeerConnected(long peerId)
  {
    if (!Multiplayer.IsServer())
    {
      return;
    }

    _audioSyncAckByPeer.Remove(peerId);
    _audioSyncAwaitingAck = true;
    _audioSyncRetryTimer = 0.0;
    RpcId(peerId, nameof(ReceiveDoomAudioState), _audioSyncSequence, _desiredDoomPlaying);
  }

  [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
  private void ReceiveDoomAudioState(int sequence, bool shouldPlay)
  {
    SetDoomPlaying(shouldPlay);
    RpcId(1, nameof(AcknowledgeDoomAudioState), sequence, shouldPlay);
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
  private void AcknowledgeDoomAudioState(int sequence, bool shouldPlay)
  {
    if (!Multiplayer.IsServer())
    {
      return;
    }

    if (sequence != _audioSyncSequence || shouldPlay != _desiredDoomPlaying)
    {
      return;
    }

    long senderId = Multiplayer.GetRemoteSenderId();
    _audioSyncAckByPeer[senderId] = sequence;
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