using Godot;
using System;

public interface Interactable
{
	public string PromptMessage { get; }

	public void Interact(GodotObject body)
	{
		GD.Print(body.GetType().Name);
	}
}
