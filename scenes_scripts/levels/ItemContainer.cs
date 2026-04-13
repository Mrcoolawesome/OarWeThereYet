using Godot;
using System;
using Godot.Collections;

public partial class ItemContainer : Node3D
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestWorldState()
	{
		if (!Multiplayer.IsServer()) return;
		long senderId = Multiplayer.GetRemoteSenderId();

		// --- Sync world items ---
		var items = new Array<Dictionary<string, Variant>>();
		CollectWorldItems(items);
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

	public void CollectWorldItems(Array<Dictionary<string, Variant>> items)
	{
		foreach (Node child in GetChildren())
		{
			if (child is UniversalInWorld item && item.Item != null && item.Item.Data.Name != "Anchor")
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
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveWorldItems(Array<Dictionary<string, Variant>> items)
	{
		// build a set of item names the server still has
		var serverItemNames = new System.Collections.Generic.HashSet<string>();
		foreach (Dictionary<string, Variant> data in items)
			serverItemNames.Add((string)data["name"]);

		// remove local items that no longer exist on the server (picked up)
		RemoveMissingWorldItems(serverItemNames);

		// collect names of items that already exist locally
		var localItemNames = new System.Collections.Generic.HashSet<string>();
		CollectLocalItemNames(localItemNames);

		// spawn only items the client doesn't already have (dropped items)
		PackedScene scene = GD.Load<PackedScene>(
			"res://scenes_scripts/inventory/items/itemScenes/UniversalInWorld.tscn");

		foreach (Dictionary<string, Variant> data in items)
		{
			string itemName = (string)data["name"];
			if (localItemNames.Contains(itemName))
			{
				// Sync count for items that already exist locally
				UniversalInWorld existing = GetNodeOrNull<UniversalInWorld>(itemName);
				if (existing?.Item != null)
				{
					int serverCount = (int)data["count"];
					existing.Item.Amount = serverCount;
					existing.ItemCount = serverCount;
				}
				continue;
			}

			UniversalInWorld node = scene.Instantiate<UniversalInWorld>();
			node.Name = itemName;
			node.ItemObject = GD.Load<InvItem>((string)data["path"]);
			node.ItemCount = (int)data["count"];
			node.Position = new Vector3(
				(float)data["pos_x"],
				(float)data["pos_y"],
				(float)data["pos_z"]);
			node.GetNodeOrNull("MultiplayerSynchronizer")?.QueueFree();
			AddChild(node);
		}
	}

	private void RemoveMissingWorldItems(System.Collections.Generic.HashSet<string> serverItemNames)
	{
		foreach (Node child in GetChildren())
		{
			if (child is UniversalInWorld item)
			{
				// Ignore anchors - they are handled specially by level logic
				if (item.Item?.Data?.Name == "Anchor") continue;

				if (!serverItemNames.Contains(child.Name.ToString()))
					child.QueueFree();
			}
		}
	}

	private void CollectLocalItemNames(System.Collections.Generic.HashSet<string> names)
	{
		foreach (Node child in GetChildren())
		{
			if (child is UniversalInWorld item)
			{
				// Ignore anchors
				if (item.Item?.Data?.Name == "Anchor") continue;

				names.Add(child.Name.ToString());
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveHeldItem(string playerId, string itemPath, int itemCount)
	{
		ArmNode arm = GetNodeOrNull<ArmNode>("../" + playerId + "/Head/ArmNode");
		arm?.SetItem(itemPath, itemCount);
	}

}
