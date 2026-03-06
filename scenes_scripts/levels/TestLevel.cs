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

	// Game saves object
	private GameSaves _gameSaves;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// attach the reset function to the signal from the signal server script
		GlobalSignalServer.Instance.ResetLevel += _InitateReset; // might be a problem to directly call an Rpc function
		GlobalSignalServer.Instance.BoatDeath += _InitateReset;

		GlobalSignalServer.Instance.LoadGame += LoadGame;
		GlobalSignalServer.Instance.SaveGame += SaveGame;

		// load or create save slot 0
		_gameSaves = GameSaves.LoadOrCreate(0);

		// set the boat variable
		_boat = GetNode<Boat>("Boat");

		_inventory = _boat.GetNode<Inventory>("DryBox/Inventory");
		_itemContainer = GetNode<ItemContainer>("ItemContainer");

		// late-joining clients ask the server for the current world state
		if (!Multiplayer.IsServer())
			GetNode<ItemContainer>("ItemContainer").RpcId(1, nameof(ItemContainer.RequestWorldState));
	}

	// ───────────────────────────────────────────────
	// Reset
	// ───────────────────────────────────────────────

	private void _InitateReset()
	{
		RpcId(1, MethodName._Reset);
	}

	// still only want the server to execute this stuff, so even though CallLocal is set to true this
	// method should ONLY EVER BE ACCESSED BY THE SERVER - hence you must always use RpcId with an id of 1 
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void _Reset()
	{
		// extra check to make sure only the server can do this
		if (!Multiplayer.IsServer()) return;

		_boat.Reset();

		// reset the players by calling the 'ResetToStart' function on all of them
		GetTree().CallGroup("players", "Reset");
	}

	// ───────────────────────────────────────────────
	// Saving and Loading Game
	// ───────────────────────────────────────────────
	private void SaveGame() 
	{
		_gameSaves.Save(0, _inventory, _itemContainer);
		GD.Print("Saved game");
	}

	private void LoadGame()
	{
		_gameSaves = GameSaves.LoadOrCreate(0);
		GD.Print("Loaded game");

		_inventory.DeserializeInventory(_gameSaves.BoatInventory);
		_itemContainer.ReceiveWorldItems(_gameSaves.WorldItems);
	}
}
