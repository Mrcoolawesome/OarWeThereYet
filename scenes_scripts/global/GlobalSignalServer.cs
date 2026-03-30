using System.ComponentModel;
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

  // signal for respawning the player
  [Signal]
  public delegate void RespawnPlayerEventHandler(int multiplayerID);

  // signal for hosting game with steam
  [Signal]
  public delegate void HostGameSteamEventHandler(bool isPublic, string name);

  // signal for hosting game with Enet
  [Signal]
  public delegate void HostGameEnetEventHandler();

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

  [Signal]
  public delegate void OpenInventoryEventHandler(int playerID);

  // Saving and loading games
  [Signal]
  public delegate void SaveGameEventHandler(int checkpointNum);
  [Signal]
  public delegate void LoadGameEventHandler();
  [Signal]
  public delegate void GoToMainMenuEventHandler();
  [Signal]
  public delegate void ShowLoadingScreenEventHandler();
  [Signal]
  public delegate void DoneLoadingMapEventHandler();

  // signal to apply the look speed for the player
  [Signal]
  public delegate void ApplyPlayerLookSpeedEventHandler(float multiplier);

  public int Health { get; set; }

  public override void _Ready()
  {
    Instance = this;
  }
}