using Godot;
using Godot.Collections;

public partial class GameSaves : Resource
{
	[Export] public int CheckpointNum { get; set; } = 0;
	[Export] public Array<Dictionary<string, Variant>> BoatInventory { get; set; } = new();
	[Export] public Array<Dictionary<string, Variant>> WorldItems { get; set; } = new();

	private const string SaveDir = "user://saves/";

	public void Save(int slot, Inventory inventory, ItemContainer itemContainer,
		Array<Dictionary<string, Variant>> heldItems = null)
	{
    BoatInventory = inventory.SerializeInventory();
    WorldItems.Clear();
    itemContainer.CollectWorldItems(WorldItems);

		if (heldItems != null)
		{
			foreach (var item in heldItems)
				WorldItems.Add(item);
		}

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

	public Array<int> ListSaves()
	{
		Array<int> saves = new();

		// Go through the three save slots
		for (int i = 0; i < 3; i++)
		{
			// Append CheckpointNum to saves
			GameSaves save = LoadOrCreate(i);
			saves.Add(save.CheckpointNum);
		}

		return saves;
	}
}
