using Godot;
using Godot.Collections;
using System;

public partial class TestLevel : Node
{

	// boat object 
	private Boat _boat = new Boat();

	// Items to serialize and save
	private Inventory _inventory = new();
	private ItemContainer _itemContainer = new();

	// Game saves object and slot tracker
	private GameSaves _gameSaves;
	public int SaveSlot = 0;

	// Checkpoint container
	private Node3D _checkpointContainer;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// load or create save slot
		_gameSaves = GameSaves.LoadOrCreate(SaveSlot);
		
		// attach the reset function to the signal from the signal server script
		GlobalSignalServer.Instance.ResetLevel += InitateReset; // might be a problem to directly call an Rpc function
		GlobalSignalServer.Instance.BoatDeath += InitateReset;

		GlobalSignalServer.Instance.LoadGame += RequestLoadGame;
		GlobalSignalServer.Instance.SaveGame += RequestSaveGame;

		// set the boat variable
		_boat = GetNode<Boat>("Boat");
		
		_inventory = _boat.GetNode<Inventory>("DryBox/Inventory");
		_itemContainer = GetNode<ItemContainer>("ItemContainer");

		_checkpointContainer = GetNode<Node3D>("CheckpointContainer");
		
		// Set boat spawn location
		if (Multiplayer.IsServer())
			SetBoatSpawn();

		// late-joining clients ask the server for the current world state
		if (!Multiplayer.IsServer())
			GetNode<ItemContainer>("ItemContainer").RpcId(1, nameof(ItemContainer.RequestWorldState));
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
		GetTree().CallGroup("players", "Reset");
	}

	private void SetBoatSpawn()
	{
		Node3D boatSpawn = null;
		// Find the boat spawn node of current checkpoint
		foreach (Checkpoint child in _checkpointContainer.GetChildren())
		{
			if (child.CheckpointNum == _gameSaves.CheckpointNum)
			{
				boatSpawn = child.GetNode<Node3D>("BoatSpawn");
			}
		}
		// Set BoatResetVector to that node
		_boat.BoatResetPosition = boatSpawn.GlobalPosition;
		_boat.BoatResetRotation = boatSpawn.GlobalRotation;
	}

	// ───────────────────────────────────────────────
	// Saving and Loading Game
	// ───────────────────────────────────────────────
	private void RequestSaveGame(int checkpointNum)
	{
		RpcId(1, nameof(SaveGame), checkpointNum);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SaveGame(int checkpointNum) 
	{
		if (!Multiplayer.IsServer()) return;
		if (_gameSaves.CheckpointNum == checkpointNum) return;

		// Set new checkpoint
		_gameSaves.CheckpointNum = checkpointNum;

		// Set new boat spawn
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
					{ "pos_x", _boat.GlobalPosition.X },
					{ "pos_y", _boat.GlobalPosition.Y + 1},
					{ "pos_z", _boat.GlobalPosition.Z },
				});
			}
		}

		_gameSaves.Save(SaveSlot, _inventory, _itemContainer, heldItems);
	}

	private void RequestLoadGame()
	{
		RpcId(1, nameof(LoadGame));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void LoadGame()
	{
		if (!Multiplayer.IsServer()) return;

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

		_inventory.DeserializeInventory(_gameSaves.BoatInventory);
		_itemContainer.ReceiveWorldItems(_gameSaves.WorldItems);

		// Broadcast world items to all clients
		_itemContainer.Rpc(ItemContainer.MethodName.ReceiveWorldItems, _gameSaves.WorldItems);
	}
}
