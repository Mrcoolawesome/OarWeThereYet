using Godot;
using System;

public partial class ArmNode : MeshInstance3D
{
	[Export] public float MaxThrowVelocity = 7.0f;
	[Export] public float LifepreserverThrowVelocity = 10.0f;
	[Export] public float MaxLifepreserverRange = 10.0f;
	[Export] public float PullStrength = 0.1f;
	[Export] public float RetractThreshold = 0.5f;

	public InvSlot Item { get; set; }
	private static int _dropCounter = 0;
	private UniversalInWorld _activeLifepreserverNode = null;
	private Player _capturedPlayerNode = null;

	// Used to compute the arm's velocity from frame-to-frame position changes
	private Vector3 _previousGlobalPosition;
	private Vector3 _armVelocity;

	// Current range of life preserver
	private float _currLifepreserverRange = 0.0f;

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

			if (Input.IsActionPressed("left_click"))
			{
				// If preserver is active, hold left_click to pull it closer continuously
				if (_activeLifepreserverNode != null)
				{
					RequestPullLifepreserver();
				}
			}

			if (Input.IsActionJustPressed("left_click"))
			{
				// If no preserver is active, use the item on fresh click
				if (_activeLifepreserverNode == null && Item?.Data?.UseAction != null)
				{
					Player player = GetParent().GetParent<Player>();
					if (player.CurrGameState == Player.GameState.Playing)
					{
						Item.Data.UseAction.Use(player, this);
					}
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Host authoritative simulation: clients only render replicated state.
		if (!Multiplayer.IsServer()) return;

		UniversalInWorld activeLifepreserver = GetActiveLifepreserverNode();
		if (activeLifepreserver == null) return;

		float distance = GlobalPosition.DistanceTo(activeLifepreserver.GlobalPosition);

		// Apply auto-pull toward arm when range is exceeded
		if (distance >= _currLifepreserverRange)
		{
			Vector3 directionToArm = activeLifepreserver.GlobalPosition.DirectionTo(GlobalPosition);
			Vector3 carrierVelocity = GetCarrierVelocity();

			const float pullSpeed = 14.0f;
			const float pullBlend = 8.0f;
			Vector3 desiredVelocity = directionToArm * pullSpeed + carrierVelocity;

			activeLifepreserver.LinearVelocity = activeLifepreserver.LinearVelocity.Lerp(desiredVelocity, (float)delta * pullBlend);
		}

		if (_capturedPlayerNode != null && _activeLifepreserverNode != null) 
		{
			_capturedPlayerNode.GlobalPosition = _activeLifepreserverNode.GlobalPosition;
			_capturedPlayerNode.GlobalRotation = _activeLifepreserverNode.GlobalRotation;
		}

		Rpc(nameof(SyncWorldItemState), _activeLifepreserverNode?.Name.ToString() ?? "", activeLifepreserver.GlobalPosition, activeLifepreserver.LinearVelocity);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SyncWorldItemState(string nodeName, Vector3 globalPosition, Vector3 linearVelocity)
	{
		if (string.IsNullOrEmpty(nodeName)) return;

		Node itemContainer = GetItemContainerNode();
		UniversalInWorld itemNode = itemContainer?.GetNodeOrNull<UniversalInWorld>(nodeName);
		if (itemNode == null) return;

		itemNode.GlobalPosition = globalPosition;
		itemNode.LinearVelocity = linearVelocity;
	}


	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetItem(string itemPath, int itemCount)
	{
		string currentItemPath = Item?.Data?.ResourcePath ?? "";
		if (Multiplayer.IsServer() && currentItemPath != itemPath && _activeLifepreserverNode != null)
		{
			Rpc(nameof(DeleteWorldItemByName), _activeLifepreserverNode.Name.ToString());
			Rpc(nameof(SetActiveLifepreserverNode), "");
		}

		if (string.IsNullOrEmpty(itemPath))
		{
			Item = null;
		}
		else
		{
			Item = new InvSlot(GD.Load<InvItem>(itemPath), itemCount);
		}
	}

	public void RequestToggleLifepreserverThrow(Vector3 throwDirection)
	{
		if (!IsMultiplayerAuthority()) return;
		RpcId(1, MethodName.ToggleLifepreserverThrow, throwDirection);
	}

	private void RequestPullLifepreserver()
	{
		if (!IsMultiplayerAuthority()) return;
		RpcId(1, MethodName.PullLifepreserver);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void PullLifepreserver()
	{
		if (!Multiplayer.IsServer()) return;
		if (_activeLifepreserverNode == null) return;

		_currLifepreserverRange -= PullStrength;

		if (_currLifepreserverRange <= RetractThreshold)
		{
			RetractLifepreserver();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void ToggleLifepreserverThrow(Vector3 throwDirection)
	{
		if (!Multiplayer.IsServer()) return;

		int senderId = Multiplayer.GetRemoteSenderId();
		// Get id of sender if sender is the host
		if (senderId == 0)
		{
			senderId = Multiplayer.GetUniqueId();
		}

		// Validate ownership and item type before touching world state.
		Node playerNode = GetParent()?.GetParent();
		if (playerNode == null || playerNode.Name.ToString() != senderId.ToString()) return;
		if (Item?.Data == null) return;
		if (Item.Data.UseAction is not Lifepreserver) return;

		Node itemContainer = GetItemContainerNode();
		if (itemContainer == null) return;

		// Toggle-off path: if one is already active, delete it and clear active state.
		if (_activeLifepreserverNode != null)
		{
			if (itemContainer.GetNodeOrNull(_activeLifepreserverNode.Name.ToString()) != null)
			{
				Rpc(nameof(DeleteWorldItemByName), _activeLifepreserverNode.Name.ToString());
			}

			Rpc(nameof(SetActiveLifepreserverNode), "");
			return;
		}

		// Toggle-on path: 
		// Set current range to max
		_currLifepreserverRange = MaxLifepreserverRange;

		// Compute launch velocity from aim direction plus platform movement.
		CharacterBody3D player = playerNode as CharacterBody3D;
		Vector3 platformVelocity = player?.GetPlatformVelocity() ?? Vector3.Zero;
		Vector3 launchDirection = throwDirection.Normalized();
		if (launchDirection == Vector3.Zero)
		{
			// Fall back to forward-facing direction if no aim vector was provided.
			launchDirection = player != null ? -player.GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;
		}

		Vector3 launchVelocity = launchDirection * LifepreserverThrowVelocity + platformVelocity;
		string uniqueName = $"LifepreserverThrown_{senderId}_{_dropCounter++}";

		// Spawn on all peers and store the active node name so the next toggle can retract it.
		Rpc(nameof(SpawnThrownLifepreserver), Item.Data.ResourcePath, GlobalPosition, uniqueName, launchVelocity);
		Rpc(nameof(SetActiveLifepreserverNode), uniqueName);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SetActiveLifepreserverNode(string nodeName)
	{
		if (string.IsNullOrEmpty(nodeName))
		{
			_activeLifepreserverNode = null;
			_capturedPlayerNode.GlobalRotation = Vector3.Zero;
			_capturedPlayerNode = null;
			return;
		}

		Node itemContainer = GetItemContainerNode();
		_activeLifepreserverNode = itemContainer?.GetNodeOrNull<UniversalInWorld>(nodeName);
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
			if (_activeLifepreserverNode != null)
			{
				Rpc(nameof(DeleteWorldItemByName), _activeLifepreserverNode.Name.ToString());
				Rpc(nameof(SetActiveLifepreserverNode), "");
			}

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

		Node itemContainer = GetItemContainerNode();
		if (itemContainer == null) return;

		itemContainer.AddChild(inWorldNode);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SpawnThrownLifepreserver(string itemPath, Vector3 position, string nodeName, Vector3 launchVelocity)
	{
		PackedScene inWorldScene = GD.Load<PackedScene>("res://scenes_scripts/inventory/items/itemScenes/UniversalInWorld.tscn");
		UniversalInWorld inWorldNode = inWorldScene.Instantiate<UniversalInWorld>();

		inWorldNode.Name = nodeName;
		inWorldNode.ItemObject = GD.Load<InvItem>(itemPath);
		inWorldNode.ItemCount = 1;
		inWorldNode.Position = position;
		inWorldNode.LinearVelocity = launchVelocity;
		// Player is on physics layer 4; include it so the hook can attach on contact.
		inWorldNode.CollisionMask |= 1u << 2;
		inWorldNode.CanBePickedUp = false;
		inWorldNode.ContactMonitor = true;
		inWorldNode.MaxContactsReported = 4;

		if (Multiplayer.IsServer())
		{
			inWorldNode.BodyEntered += OnLifepreserverBodyEntered;
		}

		inWorldNode.GetNodeOrNull("MultiplayerSynchronizer")?.QueueFree();

		Node itemContainer = GetItemContainerNode();
		if (itemContainer == null) return;

		if (itemContainer.GetNodeOrNull(nodeName) != null) return;
		itemContainer.AddChild(inWorldNode);
	}

	private void OnLifepreserverBodyEntered(Node body)
	{
		if (!Multiplayer.IsServer()) return;
		if (_activeLifepreserverNode == null) return;
		if (_capturedPlayerNode != null) return;

		Player hitPlayer = ResolvePlayerFromCollisionBody(body);
		if (hitPlayer == null) return;

		_capturedPlayerNode = hitPlayer;
	}

	private Player ResolvePlayerFromCollisionBody(Node body)
	{
		for (Node curr = body; curr != null; curr = curr.GetParent())
		{
			if (curr is Player player)
			{
				return player;
			}
		}

		return null;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void DeleteWorldItemByName(string nodeName)
	{
		if (string.IsNullOrEmpty(nodeName)) return;

		Node itemContainer = GetItemContainerNode();
		Node itemNode = itemContainer?.GetNodeOrNull(nodeName);
		itemNode?.QueueFree();
	}

	private Node GetItemContainerNode()
	{
		Node levelNode = GetParent()?.GetParent()?.GetParent();
		return levelNode?.GetNodeOrNull("ItemContainer");
	}

	private UniversalInWorld GetActiveLifepreserverNode()
	{
		if (_activeLifepreserverNode == null) return null;
		if (!IsInstanceValid(_activeLifepreserverNode))
		{
			_activeLifepreserverNode = null;
			return null;
		}

		return _activeLifepreserverNode;
	}

	private Vector3 GetCarrierVelocity()
	{
		CharacterBody3D player = GetParent()?.GetParent<CharacterBody3D>();
		if (player == null) return _armVelocity;

		// Keep platform motion in the pull-back velocity so behavior matches throw motion.
		return player.Velocity + player.GetPlatformVelocity();
	}

	private void RetractLifepreserver()
	{
		UniversalInWorld activePreserver = GetActiveLifepreserverNode();
		if (activePreserver == null) return;

		// Reset captured player rotation and node
		_capturedPlayerNode.GlobalRotation = Vector3.Zero;
		_capturedPlayerNode = null;

		// Store the item info before deletion
		InvItem itemObject = activePreserver.ItemObject;
		int itemCount = activePreserver.ItemCount;

		// Delete the preserver from the world and clear active state
		Rpc(nameof(DeleteWorldItemByName), activePreserver.Name.ToString());
		Rpc(nameof(SetActiveLifepreserverNode), "");

		// Return the item to the player's hand
		if (itemObject != null)
		{
			Rpc(nameof(SetItem), itemObject.ResourcePath, itemCount);
		}
	}
}
