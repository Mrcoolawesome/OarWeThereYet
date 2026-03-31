using Godot;
using Godot.Collections;
using System;
using Waterways;

public partial class TestLevel : Node
{
	[Signal] public delegate void BoatReadyEventHandler();

	[Export] public int SaveSlot = 0;
	public bool IsBoatReady { get; private set; } = false;

	// boat object 
	[Export] private PackedScene BoatScene;
	private Boat _boat;

	// Items to serialize and save
	private Inventory _inventory = new();
	private ItemContainer _itemContainer = new();

	// Game saves object and slot tracker
	private GameSaves _gameSaves;

	// Checkpoint container
	private Node3D _checkpointContainer;

	private RiverFloatSystem _river;

	private bool _hostNetworkInitialized = false;
	private bool _clientNetworkInitialized = false;

  private string _anchorSpawnPath = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
  {
    _checkpointContainer = GetNode<Node3D>("CheckpointContainer");
    _itemContainer = GetNode<ItemContainer>("ItemContainer");

    // load or create save slot
    _gameSaves = GameSaves.LoadOrCreate(SaveSlot);
    if (_gameSaves.CheckpointNum <= 0)
      _gameSaves.CheckpointNum = 1;
    
    // attach the reset function to the signal from the signal server script
    GlobalSignalServer.Instance.ResetLevel += LoadGame;
    GlobalSignalServer.Instance.BoatDeath += LoadGame;

    GlobalSignalServer.Instance.LoadGame += LoadGame;
    GlobalSignalServer.Instance.SaveGame += SaveGame;

    // Get river
    _river = GetNode<RiverFloatSystem>("RiverManager/RiverFloatSystem");

    if (BoatScene == null)
    {
      GD.PushError("BoatScene is not assigned on TestLevel.");
      return;
    }
    
    // 1. Instantiate the boat
    _boat = BoatScene.Instantiate<Boat>();
    _boat.River = _river;
    
    // 2. Give it a name and authority (crucial for manual spawning!)
    _boat.Name = "Boat";
    _boat.SetMultiplayerAuthority(1);
    
    // 3. ADD IT TO THE TREE IMMEDIATELY. 
    // No RPCs can happen until this is done!
    AddChild(_boat);
    
    _inventory = _boat.GetNode<Inventory>("DryBox/Inventory");

    IsBoatReady = true;
    EmitSignal(SignalName.BoatReady);

    // 4. Subscribe to connection signals. 
    // We subscribe regardless of IsServer() here because IsServer() is true when peer is null.
    Multiplayer.ConnectedToServer += OnClientConnected;
  }

	public override void _Process(double delta)
  {
    // The Host doesn't fire a "Connected" signal for itself, so we check 
    // manually until the server peer is fully spun up by the GameManager.
    if (!_hostNetworkInitialized && Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
    {
        _hostNetworkInitialized = true;
        
        // NOW we can safely load the game, move the boat, and fire RPCs!
        LoadGame(); 
    }

    // Fallback for clients: if we are connected but haven't initialized yet
    if (!_clientNetworkInitialized && Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
    {
        if (Multiplayer.GetUniqueId() != 1 && Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
        {
            _clientNetworkInitialized = true;
            OnClientConnected(); }
    }
  }

  private void OnClientConnected()
  {
    // The client runs this when they connect to the host
    // We use a flag to ensure it only runs once
    if (Multiplayer.IsServer()) return; 
    
    _clientNetworkInitialized = true;
    _itemContainer.RpcId(1, nameof(ItemContainer.RequestWorldState));
  }

  public override void _ExitTree()
  {
    // Always clean up your singletons to prevent memory leaks on re-hosting!
    GlobalSignalServer.Instance.ResetLevel -= LoadGame;
    GlobalSignalServer.Instance.BoatDeath -= LoadGame;
    GlobalSignalServer.Instance.LoadGame -= LoadGame;
    GlobalSignalServer.Instance.SaveGame -= SaveGame;
    
    Multiplayer.ConnectedToServer -= OnClientConnected;
  }

	// ───────────────────────────────────────────────
	// Reset
	// ───────────────────────────────────────────────

	private void InitateReset()
	{
		RpcId(1, nameof(Reset));
	}

	// still only want the server to execute this stuff, so even though CallLocal is set to true this
	// method should ONLY EVER BE ACCESSED BY THE SERVER - hence you must always use RpcId with an id of 1 
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void Reset()
	{
		// extra check to make sure only the server can do this
		if (!Multiplayer.IsServer()) return;

		_boat.Reset();

		// reset the players by calling the 'ResetToStart' function on all of them
		GetTree().CallGroup("players", "Reset", _anchorSpawnPath);
	}

	private void SetBoatSpawn()
	{
		Node3D boatSpawn = null;
		Node3D fallbackBoatSpawn = null;

		// Find the boat spawn node of current checkpoint
		foreach (Checkpoint child in _checkpointContainer.GetChildren())
		{
			Node3D childBoatSpawn = child.GetNodeOrNull<Node3D>("BoatSpawn");
			// Currently iterated checkpoint becomes new fallback spawn
			if (fallbackBoatSpawn == null && childBoatSpawn != null)
				fallbackBoatSpawn = childBoatSpawn;

			// If currently iterated checkpoint matches the checkpoint we have saved
			if (child.CheckpointNum == _gameSaves.CheckpointNum)
			{
				boatSpawn = childBoatSpawn;
        if (child.UseAnchor)
        {
          _boat.AnchorPoint.Deployed = true;
          _anchorSpawnPath = child.GetNodeOrNull<Node3D>("AnchorSpawn").GetPath();
        }
        else
        {
          _anchorSpawnPath = null;
        }
				break;
			}
		}

		boatSpawn ??= fallbackBoatSpawn;
		if (boatSpawn == null)
		{
			GD.PushError("No BoatSpawn node found under CheckpointContainer.");
			return;
		}

		// Set BoatResetVector to that node for host and clients
		Rpc(nameof(BroadcastBoatSpawn), boatSpawn.GlobalPosition, boatSpawn.GlobalRotation);
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void BroadcastBoatSpawn(Vector3 position, Vector3 rotation)
	{
		_boat.BoatResetPosition = position;
		_boat.BoatResetRotation = rotation;
	}

	// ───────────────────────────────────────────────
	// Saving and Loading Game
	// ───────────────────────────────────────────────
	private void SaveGame(int checkpointNum) 
	{
		if (!Multiplayer.IsServer()) return;

		// Set new checkpoint
		_gameSaves.CheckpointNum = checkpointNum;

		SetBoatSpawn();

		// Collect held items as world items positioned at the boat
		var heldItems = new Array<Dictionary<string, Variant>>();
		foreach (Node player in GetTree().GetNodesInGroup("players"))
		{
			ArmNode arm = player.GetNode<ArmNode>("Head/ArmNode");
			if (arm.Item != null)
			{
				heldItems.Add(new Dictionary<string, Variant>
				{
					{ "name",  $"held_{player.Name}" },
					{ "path",  arm.Item.Data.ResourcePath },
					{ "count", arm.Item.Amount },
					{ "pos_x", _boat.BoatResetPosition.X },
					{ "pos_y", _boat.BoatResetPosition.Y + 1},
					{ "pos_z", _boat.BoatResetPosition.Z },
				});
			}
		}

		_gameSaves.Save(SaveSlot, _inventory, _itemContainer, heldItems);
	}

	private void LoadGame()
	{
		if (!Multiplayer.IsServer()) return;
		if (_boat == null || _inventory == null || _itemContainer == null || _gameSaves == null) return;

		// Remove held items from players
		foreach (Node player in GetTree().GetNodesInGroup("players"))
		{
			ArmNode arm = player.GetNode<ArmNode>("Head/ArmNode");
			if (arm.Item != null)
			{
				arm.Rpc(nameof(arm.SetItem), "", 0);
			}
		}

		_gameSaves = GameSaves.LoadOrCreate(SaveSlot);

		// If brand new game
		if (_gameSaves.CheckpointNum <= 0)
		{
			_gameSaves.CheckpointNum = 1;
			SaveGame(1);
		}

		SetBoatSpawn();

		_inventory.DeserializeInventory(_gameSaves.BoatInventory);
		_itemContainer.ReceiveWorldItems(_gameSaves.WorldItems);

		// Broadcast world items to all clients
		_itemContainer.Rpc(ItemContainer.MethodName.ReceiveWorldItems, _gameSaves.WorldItems);

		// Reset boat and players
		InitateReset();
	}
}
