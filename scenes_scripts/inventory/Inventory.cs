using Godot;
using Godot.Collections;

public partial class Inventory : Node
{
  [Signal] public delegate void InventoryUpdatedEventHandler();

  [Export] public int Capacity = 10;
  [Export] public Array<Dictionary<string, Variant>> NetworkInventory
  {
    get{ return SerializeInventory(); }
    set{ DeserializeInventory(value); }
  }
  public System.Collections.Generic.List<InvSlot> Slots = new System.Collections.Generic.List<InvSlot>();

  public override void _Ready()
  {
    // Initialize empty slots in inventory
    for (int i = 0; i < Capacity; i++)
    {
      Slots.Add(new InvSlot(null, 0));
    }
  }

  public string Print()
  {
    string message = "";

    foreach (InvSlot slot in Slots)
    {
      if (slot.Data != null && slot.Amount > 0)
      {
        message += slot.Data.Name + " " + slot.Amount + "\n";
      }
      else
      {
        message += "Empty \n";
      }
    }

    return message;
  }

  public Array<Dictionary<string, Variant>> SerializeInventory()
  {
    var networkArray = new Array<Dictionary<string, Variant>>();
    // Always serialize exactly Capacity slots
    for (int i = 0; i < Capacity; i++)
    {
      InvSlot slot = (i < Slots.Count) ? Slots[i] : new InvSlot(null, 0);
      var slotData = new Dictionary<string, Variant>();
      slotData["id"] = slot.Data != null ? slot.Data.ResourcePath : "";
      slotData["amount"] = slot.Amount;
      networkArray.Add(slotData);
    }
    return networkArray;
  }

  public void DeserializeInventory(Array<Dictionary<string, Variant>> networkArray)
  {
    Slots.Clear();
    // Only use up to Capacity slots from the serialized data
    int count = networkArray.Count;
    for (int i = 0; i < Capacity; i++)
    {
      if (i < count)
      {
        Dictionary<string, Variant> slotData = networkArray[i];
        string path = (string)slotData["id"];
        int amount = (int)slotData["amount"];
        InvItem item = null;
        if (!string.IsNullOrEmpty(path))
        {
          item = GD.Load<InvItem>(path);
        }
        Slots.Add(new InvSlot(item, amount));
      }
      else
      {
        // Fill remaining slots with empty
        Slots.Add(new InvSlot(null, 0));
      }
    }
    EmitSignal(SignalName.InventoryUpdated);
  }


  public void RequestSlotClick(int slot)
  {
    RpcId(1, MethodName.SlotClick, slot);
  }

  public void RequestStoreHeldItem()
  {
    RpcId(1, MethodName.StoreHeldItem);
  }

  private ArmNode GetPlayerArm()
  {
    int playerID = Multiplayer.GetRemoteSenderId();

    foreach (Node node in GetTree().GetNodesInGroup("players"))
    {
      if (node.Name == playerID.ToString())
      {
        return node.GetNodeOrNull<ArmNode>("Head/ArmNode");
      }
    }

    return null;
  }

  private void SyncPlayerHand(ArmNode playerArm, InvSlot slot)
  {
    if (slot != null && slot.Data != null && slot.Amount > 0)
    {
      playerArm.Rpc(nameof(playerArm.SetItem), slot.Data.ResourcePath, slot.Amount);
    }
    else
    {
      playerArm.Rpc(nameof(playerArm.SetItem), "", 0);
    }
  }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void SlotClick(int slot)
  {
    if (slot < 0 || slot >= Capacity) return;

    ArmNode playerArm = GetPlayerArm();
    if (playerArm == null) return;
    InvSlot playerSlot = playerArm.Item;
    InvSlot invSlot = Slots[slot];

    bool playerEmpty = playerSlot == null || playerSlot.IsEmpty();
    bool slotEmpty = invSlot == null || invSlot.IsEmpty();
    bool sameItem = !playerEmpty && !slotEmpty
      && playerSlot.Data.ResourcePath == invSlot.Data.ResourcePath;

    if (sameItem)
    {
      int maxStack = invSlot.Data.MaxStackSize;
      int invSpace = maxStack - invSlot.Amount;

      if (invSpace > 0)
      {
        // Merge hand into inventory slot
        int transfer = System.Math.Min(playerSlot.Amount, invSpace);
        invSlot.Amount += transfer;
        playerSlot.Amount -= transfer;
      }
      else
      {
        // Inventory slot is full — pull from it to max out the hand
        int handSpace = maxStack - playerSlot.Amount;
        int transfer = System.Math.Min(invSlot.Amount, handSpace);
        playerSlot.Amount += transfer;
        invSlot.Amount -= transfer;
      }

      SyncPlayerHand(playerArm, playerSlot);
    }
    else
    {
      // Situation 1: Different items (or one/both empty) — swap
      Slots[slot] = playerSlot ?? new InvSlot(null, 0);
      SyncPlayerHand(playerArm, invSlot);
    }

    EmitSignal(SignalName.InventoryUpdated);
  }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void StoreHeldItem()
  {
    ArmNode playerArm = GetPlayerArm();
    if (playerArm == null) return;
    InvSlot playerSlot = playerArm.Item;

    if (playerSlot == null || playerSlot.IsEmpty()) return;

    int remaining = playerSlot.Amount;
    string itemPath = playerSlot.Data.ResourcePath;
    int maxStack = playerSlot.Data.MaxStackSize;

    // First pass: fill existing stacks of the same item
    for (int i = 0; i < Capacity && remaining > 0; i++)
    {
      InvSlot slot = Slots[i];
      if (slot != null && slot.Data != null && slot.Data.ResourcePath == itemPath)
      {
        int space = maxStack - slot.Amount;
        int transfer = System.Math.Min(remaining, space);
        slot.Amount += transfer;
        remaining -= transfer;
      }
    }

    // Second pass: place remainder in empty slots
    for (int i = 0; i < Capacity && remaining > 0; i++)
    {
      InvSlot slot = Slots[i];
      if (slot == null || slot.IsEmpty())
      {
        int transfer = System.Math.Min(remaining, maxStack);
        Slots[i] = new InvSlot(GD.Load<InvItem>(itemPath), transfer);
        remaining -= transfer;
      }
    }

    // Update player's hand with whatever is left
    if (remaining <= 0)
    {
      playerArm.Rpc(nameof(playerArm.SetItem), "", 0);
    }
    else
    {
      playerArm.Rpc(nameof(playerArm.SetItem), itemPath, remaining);
    }

    EmitSignal(SignalName.InventoryUpdated);
  }
}
