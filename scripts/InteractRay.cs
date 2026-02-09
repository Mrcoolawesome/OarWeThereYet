using Godot;
using System;

public partial class InteractRay : RayCast3D
{
	private Label _prompt;
	private Player _player;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_prompt = GetNode<Label>("Prompt");
		_player = GetParent<Player>();
	}

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
		_prompt.Text = "";

		if (IsColliding())
		{
			GodotObject collider = GetCollider();

			if (collider is Interactable hitObject)
			{
				_prompt.Text = hitObject.GetMessage();
			
				if (Input.IsActionJustPressed(hitObject.PromptInput))
				{
					hitObject.Interact(_player);
				}
			}
		}
	}
}
