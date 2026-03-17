using Godot;
using System;

public partial class ArmNode : MeshInstance3D
{
	[Export] public float MaxThrowVelocity = 7.0f;

	public InvSlot Item { get; set; }
	private static int _dropCounter = 0;

	// Used to compute the arm's velocity from frame-to-frame position changes
	private Vector3 _previousGlobalPosition;
	private Vector3 _armVelocity;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Mesh = null;
		_previousGlobalPosition = GlobalPosition;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Compute the arm's velocity from its change in global position
		if (delta > 0)
		{
			_armVelocity = (GlobalPosition - _previousGlobalPosition) / (float)delta;
		}
		_previousGlobalPosition = GlobalPosition;

		if (Item != null)
		{
			Mesh = Item.Data.ItemMesh;
		}
		else
		{
			Mesh = null;
		}

		if (GetParent().GetParent<Node>().IsMultiplayerAuthority())
		{
			if (Input.IsActionPressed("right_click"))
			{
				// Get uncapped platform velocity from the player's moving platform (e.g. boat)
				CharacterBody3D player = GetParent().GetParent<CharacterBody3D>();
				Vector3 platformVelocity = player.GetPlatformVelocity();

				// Subtract platform contribution so we only cap the player's own throw velocity
				Vector3 throwVelocity = (_armVelocity - platformVelocity).LimitLength(MaxThrowVelocity);

				// Add uncapped platform velocity back on top
				RequestDropItem(throwVelocity + platformVelocity);
			}

			if (Input.IsActionJustPressed("left_click") && Item?.Data?.UseAction != null)
			{
				Player player = GetParent().GetParent<Player>();
				if (player.CurrGameState == Player.GameState.Playing)
				{
					Item.Data.UseAction.Use(player, this);
				}
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetItem(string itemPath, int itemCount)
	{
		if (string.IsNullOrEmpty(itemPath))
		{
			Item = null;
		}
		else
		{
			Item = new InvSlot(GD.Load<InvItem>(itemPath), itemCount);
		}
	}

	private void RequestDropItem(Vector3 dropVelocity)
	{
		RpcId(1, MethodName.DropItem, dropVelocity);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void DropItem(Vector3 dropVelocity)
	{
		if (Item != null)
		{
			string itemPath = Item.Data.ResourcePath;
			int itemCount = Item.Amount;
			Vector3 dropPosition = GlobalPosition;
			string uniqueName = $"DroppedItem_{Multiplayer.GetUniqueId()}_{_dropCounter++}";

			// Tell all peers to spawn the item and clear the arm
			Rpc(nameof(SpawnDroppedItem), itemPath, itemCount, dropPosition, uniqueName, dropVelocity);
			Rpc(nameof(SetItem), "", 0);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SpawnDroppedItem(string itemPath, int itemCount, Vector3 position, string nodeName, Vector3 dropVelocity)
	{
		PackedScene inWorldScene = GD.Load<PackedScene>("res://scenes_scripts/inventory/items/itemScenes/UniversalInWorld.tscn");
		UniversalInWorld inWorldNode = inWorldScene.Instantiate<UniversalInWorld>();

		inWorldNode.Name = nodeName;
		inWorldNode.ItemObject = GD.Load<InvItem>(itemPath);
		inWorldNode.ItemCount = itemCount;
		inWorldNode.Position = position;
		inWorldNode.LinearVelocity = dropVelocity;

		// Remove MultiplayerSynchronizer — items spawned via RPC (not MultiplayerSpawner)
		// cause path resolution errors in the multiplayer cache
		inWorldNode.GetNodeOrNull("MultiplayerSynchronizer")?.QueueFree();

		Node levelNode = GetParent()?.GetParent()?.GetParent();
		Node itemContainer = levelNode?.GetNodeOrNull("ItemContainer");
		if (itemContainer == null) return;

		itemContainer.AddChild(inWorldNode);
	}
}
