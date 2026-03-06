using Godot;
using Godot.Collections;

public partial class GameSaves : Resource
{
	[Export] public int CheckpointNum { get; set; } = 0;
	[Export] public Array<Dictionary<string, Variant>> BoatInventory { get; set; } = new();
	[Export] public Array<Dictionary<string, Variant>> WorldItems { get; set; } = new();

	private const string SaveDir = "user://saves/";

	public void Save(int slot, Inventory inventory, ItemContainer itemContainer)
	{
    BoatInventory = inventory.SerializeInventory();
    itemContainer.CollectWorldItems(WorldItems);

		DirAccess.MakeDirRecursiveAbsolute(SaveDir);
		ResourceSaver.Save(this, GetPath(slot));
	}

	public static GameSaves LoadOrCreate(int slot)
	{
		string path = GetPath(slot);
		if (ResourceLoader.Exists(path))
		{
			var res = ResourceLoader.Load<GameSaves>(path);
			if (res != null)
				return res;
		}
		return new GameSaves();
	}

	public static void DeleteSave(int slot)
	{
		string path = GetPath(slot);
		if (FileAccess.FileExists(path))
			DirAccess.RemoveAbsolute(path);
	}

	public static bool HasSave(int slot)
	{
		return FileAccess.FileExists(GetPath(slot));
	}

	private static string GetPath(int slot)
	{
		return $"{SaveDir}save_{slot}.tres";
	}
}
