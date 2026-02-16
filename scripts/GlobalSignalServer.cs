using Godot;

public partial class GlobalSignalServer : Node
{
  // according to the docs, the checkmark to make it loaded or not doesn't work for c#.
  // but we can do a 'similar' thing by setting 'Instance = this' or not depending on if we want it loaded or not
  public static GlobalSignalServer Instance { get; private set; }

  // signal for rowing
  [Signal]
	public delegate void RowingEventHandler(int seat, bool stopStart, bool backForward);

  // signal for reseting the game
  [Signal]
  public delegate void ResetLevelEventHandler();

  // signal for hosting game
  [Signal]
  public delegate void HostGameEventHandler();

  // signal for using steam or using local ENet multiplayer
  [Signal]
  public delegate void SelectSteamNetworkEventHandler(bool steamSelected);

  // signal for joining game
  [Signal]
  public delegate void JoinGameEventHandler(int lobbyId);

  // signal for updating the boat health ui
  [Signal]
  public delegate void UpdateBoatHealthEventHandler(int newHealth);

  // signal for saying the boat died
  [Signal]
  public delegate void BoatDeathEventHandler();

  // signal for triggering the oar rowing animation for a specific oar
  [Signal]
  public delegate void AnimateOarEventHandler(int seat, int direction, bool startStop);

  public int Health { get; set; }

  public override void _Ready()
  {
    Instance = this;
  }
}