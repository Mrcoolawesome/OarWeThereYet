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

  public int Health { get; set; }

  public override void _Ready()
  {
    Instance = this;
  }
}