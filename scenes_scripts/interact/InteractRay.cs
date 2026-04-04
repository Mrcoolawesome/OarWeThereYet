using Godot;
using System;

public partial class InteractRay : RayCast3D
{
	[Export] Player Player { get; set; }
	private Label _prompt;
    private Interactable _currentInteractable;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_prompt = GetNode<Label>("Prompt");
	}

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
      _prompt.Text = "";
      Interactable hitObject = null;

      if (IsColliding())
      {
        GodotObject collider = GetCollider();

        if (collider is Interactable io)
        {
          hitObject = io;

          if (hitObject.PromptMessage == "Sit" && Player.IsSwimming) return;

          _prompt.Text = hitObject.GetMessage();
        
          if (Input.IsActionJustPressed(hitObject.PromptInput))
          {
            hitObject.Interact(Player);
                      hitObject.StartInteract(Player);
                      _currentInteractable = hitObject;
          }
        }
      }

      if (_currentInteractable != null)
      {
          if (Input.IsActionJustReleased(_currentInteractable.PromptInput) || hitObject != _currentInteractable)
          {
              _currentInteractable.StopInteract(Player);
              _currentInteractable = null;
          }
      }
	}
}
