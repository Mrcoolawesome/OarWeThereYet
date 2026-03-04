using Godot;
using Godot.Collections;
using System;

public partial class TestLevel : Node
{

	// boat object 
	private Boat _boat = new Boat();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// attach the reset function to the signal from the signal server script
		GlobalSignalServer.Instance.ResetLevel += _InitateReset; // might be a problem to directly call an Rpc function
		GlobalSignalServer.Instance.BoatDeath += _InitateReset;

		// set the boat variable
		_boat = GetNode<Boat>("Boat");

		// late-joining clients ask the server for the current world state
		if (!Multiplayer.IsServer())
			RpcId(1, MethodName.RequestWorldState);
	}

	// ───────────────────────────────────────────────
	// Late-join sync: world items + held items
	// ───────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestWorldState()
	{
		if (!Multiplayer.IsServer()) return;
		long senderId = Multiplayer.GetRemoteSenderId();

		// --- Sync world items ---
		var items = new Array<Dictionary<string, Variant>>();
		CollectWorldItems(this, items);
		RpcId(senderId, MethodName.ReceiveWorldItems, items);

		// --- Sync held items for every player ---
		foreach (Node player in GetTree().GetNodesInGroup("players"))
		{
			ArmNode arm = player.GetNode<ArmNode>("Head/ArmNode");
			if (arm.Item != null)
			{
				RpcId(senderId, MethodName.ReceiveHeldItem,
					player.Name.ToString(),
					arm.Item.Data.ResourcePath,
					arm.Item.Amount);
			}
		}
	}

	private void CollectWorldItems(Node parent, Array<Dictionary<string, Variant>> items)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is UniversalInWorld item && item.Item != null)
			{
				items.Add(new Dictionary<string, Variant>
				{
					{ "name",  item.Name.ToString() },
					{ "path",  item.Item.Data.ResourcePath },
					{ "count", item.Item.Amount },
					{ "pos_x", item.GlobalPosition.X },
					{ "pos_y", item.GlobalPosition.Y },
					{ "pos_z", item.GlobalPosition.Z },
				});
			}
			else
			{
				// recurse into non-item children to find nested scene-placed items
				CollectWorldItems(child, items);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveWorldItems(Array<Dictionary<string, Variant>> items)
	{
		// build a set of item names the server still has
		var serverItemNames = new System.Collections.Generic.HashSet<string>();
		foreach (Dictionary<string, Variant> data in items)
			serverItemNames.Add((string)data["name"]);

		// remove local items that no longer exist on the server (picked up)
		RemoveMissingWorldItems(this, serverItemNames);

		// collect names of items that already exist locally
		var localItemNames = new System.Collections.Generic.HashSet<string>();
		CollectLocalItemNames(this, localItemNames);

		// spawn only items the client doesn't already have (dropped items)
		PackedScene scene = GD.Load<PackedScene>(
			"res://scenes_scripts/inventory/items/itemScenes/UniversalInWorld.tscn");

		foreach (Dictionary<string, Variant> data in items)
		{
			string itemName = (string)data["name"];
			if (localItemNames.Contains(itemName)) continue;

			UniversalInWorld node = scene.Instantiate<UniversalInWorld>();
			node.Name = itemName;
			node.ItemObject = GD.Load<InvItem>((string)data["path"]);
			node.ItemCount = (int)data["count"];
			node.Position = new Vector3(
				(float)data["pos_x"],
				(float)data["pos_y"],
				(float)data["pos_z"]);
			AddChild(node);
		}
	}

	private void RemoveMissingWorldItems(Node parent, System.Collections.Generic.HashSet<string> serverItemNames)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is UniversalInWorld)
			{
				if (!serverItemNames.Contains(child.Name.ToString()))
					child.QueueFree();
			}
			else
			{
				RemoveMissingWorldItems(child, serverItemNames);
			}
		}
	}

	private void CollectLocalItemNames(Node parent, System.Collections.Generic.HashSet<string> names)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is UniversalInWorld)
				names.Add(child.Name.ToString());
			else
				CollectLocalItemNames(child, names);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveHeldItem(string playerId, string itemPath, int itemCount)
	{
		ArmNode arm = GetNodeOrNull<ArmNode>(playerId + "/Head/ArmNode");
		arm?.SetItem(itemPath, itemCount);
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
}
