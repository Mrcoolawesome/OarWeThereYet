using Godot;
using System;

public interface Interactable
{
	public string PromptMessage { get; }
	public string PromptInput { get; }
	[Signal] public delegate void InteractedEventHandler();

	public string GetMessage()
	{
		var events = InputMap.ActionGetEvents(PromptInput);
		string keyName = "";

		foreach (var inputEvent in events)
		{
			if (inputEvent is InputEventKey inputKey)
			{
				keyName = inputKey.PhysicalKeycode.ToString();
			}
		}

		return PromptMessage + "\n [" + keyName + "]";
	}

	public void Interact();
}
