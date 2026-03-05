using Godot;

[GlobalClass]
public abstract partial class ItemAction : Resource
{
  // Called when player left clicks while holding an item
	public abstract void Use(Player player, ArmNode arm);
}
