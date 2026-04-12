using Godot;
using Waterways; 

public partial class AmbientRiverAudio : AudioStreamPlayer3D
{
	[Export] public RiverManager RiverNode;

	// We no longer export this; we will find it dynamically
	private Player _playerTarget;

	public override void _PhysicsProcess(double delta)
	{
		if (RiverNode == null || RiverNode.Curve == null) return;

		// Try to find the local player if they haven't spawned or been assigned yet
		if (_playerTarget == null)
		{
			FindLocalPlayer();
		}

		// If the player still hasn't spawned into the tree, exit early
		if (_playerTarget == null) return;

		// 1. Get the player's global position
		Vector3 playerGlobalPos = _playerTarget.GlobalPosition;

		// 2. Convert player position into the river's local space
		Vector3 localPlayerPos = RiverNode.ToLocal(playerGlobalPos);

		// 3. Let the native Curve3D find the closest baked point
		float closestOffset = RiverNode.Curve.GetClosestOffset(localPlayerPos);
		Vector3 closestLocalPoint = RiverNode.Curve.SampleBaked(closestOffset);

		// 4. Move THIS node directly to that exact spot on the spline
		GlobalPosition = RiverNode.ToGlobal(closestLocalPoint);
	}

	private void FindLocalPlayer()
	{
		// Grab the grandparent node based on your scene hierarchy
		// THIS ASSUMES THAT THIS AMBIENT RIVER NODE IS INSTANTIATED UNDER SOME 'audio container' THAT'S THE CHILD OF THE MAIN MAP SCENE NODE
		Node grandparent = GetParent()?.GetParent();
		if (grandparent == null) return;

		// Iterate through all children of the grandparent
		foreach (Node child in grandparent.GetChildren())
		{
			// Check if the child is of your specific Player class
			if (child is Player player)
			{
				// Verify if this client has network authority over this specific player instance
				if (player.IsMultiplayerAuthority())
				{
					_playerTarget = player;
					break; // Target acquired, stop iterating
				}
			}
		}
	}
}