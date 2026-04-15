using Godot;
using System;

public partial class PauseUi : Control
{
  [Signal] public delegate void ResumeEventHandler();
  [Signal] public delegate void ExitEventHandler();
  
  // get the settings menu
  private Control _settingsMenu = new Control();
  private Control _resetMenu = new Control();
  private MarginContainer _mainContainer = new MarginContainer();

  // some gemini thing so that player.cs knows the state of the pause menu
  public bool IsSettingsOpen => _settingsMenu.Visible;

  // Called when the node enters the scene tree for the first time.
  public override void _Ready()
  {
    // get the stuff from the tree
    _settingsMenu = GetNode<Control>("PanelContainer/SettingsMenu");
    _resetMenu = GetNode<Control>("PanelContainer/ResetMenu");
    _mainContainer = GetNode<MarginContainer>("PanelContainer/PauseButtonMainContainer");
  }

  // --- NEW INPUT HANDLER ---
  public override void _Input(InputEvent @event)
  {
    // Check if Escape was pressed. Godot defaults this to "ui_cancel".
    // If you use a custom input action to pause (like "pause_game"), change "ui_cancel" to that!
    if (@event.IsActionPressed("ui_cancel"))
    {
      // If the settings menu is currently open...
      if (_settingsMenu.Visible)
      {
        // 1. Consume the input so your main game script doesn't see it and close the whole pause menu!
        GetViewport().SetInputAsHandled();

        // 2. Call the GDScript's back button function so it handles your unsaved changes logic
        _settingsMenu.Call("_on_back_button_pressed");
      }
      else if (_resetMenu.Visible)
      {
        // 1. Consume the input so your main game script doesn't see it and close the whole pause menu!
        GetViewport().SetInputAsHandled();

        // 2. Call the GDScript's back button function so it handles your unsaved changes logic
        OnSettingsBackButtonPressed();
      }
    }
  }
  // -------------------------

  private void OnResumeButtonPressed()
  {
    EmitSignal(SignalName.Resume);
  }

  private void OnExitButtonPressed()
  {
    // Whether we are the Host or the Client, the process is now exactly the same!
    // 1. Tell the local game manager to go to the main menu
    GlobalSignalServer.Instance.EmitSignal(nameof(GlobalSignalServer.GoToMainMenu));
  }

  private void OnSettingsButtonPressed()
  {
    _settingsMenu.Visible = true;
    _mainContainer.Visible = false;
  }

  private void OnSettingsBackButtonPressed()
  {
    _resetMenu.Visible = false;
    _settingsMenu.Visible = false;
    _mainContainer.Visible = true;
  }

  private void OnResetButtonPressed()
  {
    _resetMenu.Visible = true;
    _mainContainer.Visible = false;
  }
}