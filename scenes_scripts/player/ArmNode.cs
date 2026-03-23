using Godot;
using System;

public partial class ArmNode : MeshInstance3D
{
	[Export] public float MaxThrowVelocity = 7.0f;
	[Export] public float LifepreserverThrowVelocity = 10.0f;
	[Export] public float MaxLifepreserverRange = 10.0f;
	[Export] public float PullStrength = 0.1f;
	[Export] public float RetractThreshold = 0.5f;
	[Export] public float PlayerSpawnOffset = 2.0f;

	public InvSlot Item { get; set; }
	private static int _dropCounter = 0;
	private UniversalInWorld _activeLifepreserverNode = null;
	private Player _capturedPlayerNode = null;
	private Player _player;

	// Used to compute the arm's velocity from frame-to-frame position changes
	private Vector3 _previousGlobalPosition;
	private Vector3 _armVelocity;

	// Current range of life preserver
	private float _currLifepreserverRange = 0.0f;

	// Node for the rope visual
	private Node3D _ropeRoot = null;
	private MeshInstance3D _ropeMeshInstance = null;
	private CylinderMesh _ropeMesh = null;

	// Hint labels
	private Label _hint1;
	private Label _hint2;

	public override void _Ready()
	{
		Mesh = null;
		_previousGlobalPosition = GlobalPosition;

		// Create a root node for the rope mesh at the world origin
		_ropeRoot = new Node3D();
		_ropeRoot.Name = "RopeRoot";
		_ropeRoot.GlobalPosition = Vector3.Zero;
		GetTree().Root.AddChild(_ropeRoot);

		_ropeMesh = new CylinderMesh();
		_ropeMesh.TopRadius = 0.05f;
		_ropeMesh.BottomRadius = 0.05f;
		_ropeMesh.Height = 1.0f;
		_ropeMesh.RadialSegments = 8;
		_ropeMeshInstance = new MeshInstance3D();
		_ropeMeshInstance.Mesh = _ropeMesh;
		_ropeMeshInstance.Visible = false;
		// Assign a visible unshaded yellow material
		var ropeMaterial = new StandardMaterial3D();
		ropeMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		ropeMaterial.AlbedoColor = new Color(1, 1, 1); // Yellow
		_ropeMeshInstance.SetSurfaceOverrideMaterial(0, ropeMaterial);
		_ropeRoot.AddChild(_ropeMeshInstance);

		_player = GetParent().GetParent<Player>();

		// Get hint labels
		_hint1 = GetNode<Label>("ControlHints/Control/VBoxContainer/Hint1");
		_hint2 = GetNode<Label>("ControlHints/Control/VBoxContainer/Hint2");

		HintLabels(false);
	}

	public override void _Process(double delta)
	{
		// Compute the arm's velocity from its change in global position
		if (delta > 0)
		{
			_armVelocity = (GlobalPosition - _previousGlobalPosition) / (float)delta;
		}
		_previousGlobalPosition = GlobalPosition;

		// Hide the mesh if the lifepreserver is active
		if (_activeLifepreserverNode != null)
		{
			Mesh = null;
			// Show and update the rope mesh
			_ropeMeshInstance.Visible = true;
			UpdateRopeMesh();
		}
		else
		{
			if (Item != null)
			{
				if (Item.Data.UseAction is Oar && _player.CurrPlayerState == Player.PlayerState.Rowing)
				{
					Mesh = null;
				}
				else
				{
					Mesh = Item.Data.ItemMesh;
				}
			}
			else
			{
				Mesh = null;
			}
			// Hide the rope mesh
			_ropeMeshInstance.Visible = false;
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

		// Hint text logic
		if (_activeLifepreserverNode != null || _player.CurrPlayerState == Player.PlayerState.Rowing)
		{
			HintLabels(true);
		}
		else
		{
			HintLabels(false);
		}
	}

	// Draws a line between the arm and the active lifepreserver
	void UpdateRopeMesh()
	{
		if (_activeLifepreserverNode == null || !IsInstanceValid(_activeLifepreserverNode))
		{
			_ropeMeshInstance.Visible = false;
			return;
		}
		Vector3 start = GlobalPosition;
		Vector3 end = _activeLifepreserverNode.GlobalPosition;
		Vector3 mid = (start + end) * 0.5f;
		Vector3 dir = end - start;
		float length = dir.Length();
		if (length < 0.01f)
		{
			_ropeMeshInstance.Visible = false;
			return;
		}
		_ropeMeshInstance.Visible = true;
		_ropeMesh.Height = length;
		_ropeMeshInstance.GlobalPosition = mid;
		// Align the cylinder with the direction vector (default cylinder points up, so align Vector3.Up to dir)
		var up = Vector3.Up;
		var axis = up.Cross(dir.Normalized());
		float angle = Mathf.Acos(up.Dot(dir.Normalized()));
		var rotation = axis.LengthSquared() > 0.0001f ? new Quaternion(axis.Normalized(), angle) : Quaternion.Identity;
		_ropeMeshInstance.GlobalTransform = new Transform3D(new Basis(rotation), mid);
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

		if (_capturedPlayerNode != null && _activeLifepreserverNode != null && _capturedPlayerNode.CurrPlayerState == Player.PlayerState.Standing)
		{
			_capturedPlayerNode.GlobalPosition = _activeLifepreserverNode.GlobalPosition;
			_capturedPlayerNode.GlobalRotation = _activeLifepreserverNode.GlobalRotation;

			int capturedAuthorityId = _capturedPlayerNode.GetMultiplayerAuthority();
			_capturedPlayerNode.RpcId(capturedAuthorityId, nameof(Player.SyncCapturedTransform), _activeLifepreserverNode.GlobalPosition, _activeLifepreserverNode.GlobalRotation);
		}

		Rpc(nameof(SyncWorldItemState), _activeLifepreserverNode?.Name.ToString() ?? "", activeLifepreserver.GlobalPosition, activeLifepreserver.LinearVelocity, activeLifepreserver.GlobalRotation);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SyncWorldItemState(string nodeName, Vector3 globalPosition, Vector3 linearVelocity, Vector3 globalRotation)
	{
		if (string.IsNullOrEmpty(nodeName)) return;

		Node itemContainer = GetItemContainerNode();
		UniversalInWorld itemNode = itemContainer?.GetNodeOrNull<UniversalInWorld>(nodeName);
		if (itemNode == null) return;

		itemNode.GlobalPosition = globalPosition;
		itemNode.LinearVelocity = linearVelocity;
		itemNode.GlobalRotation = globalRotation;
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

	public void RequestToggleLifepreserverThrow(Vector3 throwDirection)
	{
		if (!IsMultiplayerAuthority()) return;
		RpcId(1, MethodName.ToggleLifepreserverThrow, throwDirection);
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
			if (_capturedPlayerNode != null)
			{
				int capturedAuthorityId = _capturedPlayerNode.GetMultiplayerAuthority();
				_capturedPlayerNode.SetCapturedByLifepreserver(false);
				if (capturedAuthorityId != Multiplayer.GetUniqueId())
				{
					_capturedPlayerNode.RpcId(capturedAuthorityId, nameof(Player.SetCapturedByLifepreserver), false);
				}
				// Spawn the player slightly above the last preserver position to avoid ground collision
				_capturedPlayerNode.GlobalPosition += new Vector3(0, PlayerSpawnOffset, 0);
				_capturedPlayerNode.GlobalRotation = Vector3.Zero;
			}
			_capturedPlayerNode = null;
			return;
		}

		Node itemContainer = GetItemContainerNode();
		_activeLifepreserverNode = itemContainer?.GetNodeOrNull<UniversalInWorld>(nodeName);
		// Hide the arm mesh when the lifepreserver is thrown
		Mesh = null;
		// Show rope mesh if preserver is active
		if (!string.IsNullOrEmpty(nodeName))
		{
			_ropeMeshInstance.Visible = true;
		}
		else
		{
			_ropeMeshInstance.Visible = false;
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
		int capturedAuthorityId = _capturedPlayerNode.GetMultiplayerAuthority();
		_capturedPlayerNode.SetCapturedByLifepreserver(true);
		if (capturedAuthorityId != Multiplayer.GetUniqueId())
		{
			_capturedPlayerNode.RpcId(capturedAuthorityId, nameof(Player.SetCapturedByLifepreserver), true);
		}
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

		// Teleport captured player to the holder's position plus offset, then reset rotation and node
		if (_capturedPlayerNode != null)
		{
			// Get the player holding the preserver (the parent of this ArmNode)
			Player holder = GetParent()?.GetParent<Player>();
			Vector3 newPosition = _capturedPlayerNode.GlobalPosition;
			if (holder != null)
			{
				newPosition = holder.GlobalPosition + new Vector3(0, PlayerSpawnOffset, 0);
				_capturedPlayerNode.GlobalPosition = newPosition;
			}
			int capturedAuthorityId = _capturedPlayerNode.GetMultiplayerAuthority();
			_capturedPlayerNode.SetCapturedByLifepreserver(false);
			if (capturedAuthorityId != Multiplayer.GetUniqueId())
			{
				_capturedPlayerNode.RpcId(capturedAuthorityId, nameof(Player.SetCapturedByLifepreserver), false);
				// Tell the client to sync their position and rotation
				_capturedPlayerNode.RpcId(capturedAuthorityId, nameof(Player.SyncCapturedTransform), newPosition, Vector3.Zero);
			}
			_capturedPlayerNode.GlobalRotation = Vector3.Zero;
		}
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

	private void HintLabels(bool alt)
	{
		// If not holding anything
		if (Item == null || _player.CurrGameState == Player.GameState.Menu)
		{
			_hint1.Visible = false;
			_hint2.Visible = false;
			_hint1.Text = "";
			_hint2.Text = "";
		}
		else
		{
			if (!alt)
			{
				_hint1.Text = Item.Data.Hint1;
				_hint2.Text = Item.Data.Hint2;
			}
			else
			{
				_hint1.Text = Item.Data.HintAlt1;
				_hint2.Text = Item.Data.HintAlt2;
			}
			_hint1.Visible = true;
			_hint2.Visible = true;
		}
	}
}
