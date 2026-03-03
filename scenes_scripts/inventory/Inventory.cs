using Godot;
using Godot.Collections;

public partial class Inventory : Node
{
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

    foreach (InvSlot slot in Slots)
    {
      var slotData = new Dictionary<string, Variant>();

      if (slot.Data != null)
      {
        slotData["id"] = slot.Data.ResourcePath;
      }
      else
      {
        slotData["id"] = "";
      }

      slotData["amount"] = slot.Amount;

      networkArray.Add(slotData); 
    }

    return networkArray;
  }

  public void DeserializeInventory(Array<Dictionary<string, Variant>> networkArray)
  {
    Slots.Clear();

    foreach (Dictionary<string, Variant> slotData in networkArray)
    {
      string path = (string)slotData["id"];
      int amount = (int)slotData["amount"];

      InvItem item = null;
      if (path != "")
      {
        item = GD.Load<InvItem>(path);
      }

      Slots.Add(new InvSlot(item, amount));
    }
  }


  public void RequestSwapItem(int slot)
  {
    RpcId(1, MethodName.SwapItem, slot);
  }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
  private void SwapItem(int slot)
  {
    // Get player and their arm
    int playerID = Multiplayer.GetRemoteSenderId();
    ArmNode playerArm = GetNode<ArmNode>("/root/GameManager/Level/DemoLevel/" + playerID + "/Head/ArmNode");
    InvSlot playerSlot = playerArm.Item;
    InvSlot invSlot = Slots[slot];

    // If it's a valid slot
    if (slot < Capacity)
    {
      Slots[slot] = playerSlot;
      playerArm.SetItem(
        invSlot.Data.ResourcePath,
        invSlot.Amount
      );
    }
  }
}
