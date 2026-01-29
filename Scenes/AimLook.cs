using Godot;
using System;
using System.Numerics;

public partial class AimLook : Node
{
	// This makes a new export group for all the exports that come after this delcaration
	// To end a group without making a new one you can just delcare a new group with an empty name
	[ExportGroup("Nodes")]
	[Export]
	public CharacterBody3D Character { get; set; } = new CharacterBody3D();
	[Export]
	public Node3D Head { get; set; } = new Node3D();

	// New group for settings, with a subgroup specficially for clamp settings
	[ExportGroup("Settings")]
	[ExportSubgroup("Mouse settings")]
	[Export(PropertyHint.Range, "1,100,1")] // don't want a value of zero. have steps of 1
	public int MouseSensitivity { get; set; } = 50;
	[ExportSubgroup("Clamp Settings")]
	[Export]
	public float MaxPitch { get; set; } = 89; // max pitch in degrees
	[Export]
	public float MinPitch{ get; set; } = -89; // min pitch in degrees

  // We're using 'unhandled input' instead of 'input' bc we don't wanna interfere with the mouse clicking on things like ui elements
  public override void _UnhandledInput(InputEvent @event)
  {
		// this means we're in the menu if this is true
		if (Input.MouseMode != Input.MouseModeEnum.Captured)
		{
			// if we're in the menu (mouse mode is not in the captured state) then we can simply quit the game if escape is hit in the main menu
			if (@event is InputEventKey key0)
			{
				if (key0.IsActionPressed("ui_cancel"))
				{
					GetTree().Quit(); // quit the game if they press escape in the menu
				}
			}

			// capture the mouse if the left mouse button is being pressed
			if (@event is InputEventMouseButton button)
			{
				if (button.ButtonIndex == MouseButton.Left)
				{
					Input.MouseMode = Input.MouseModeEnum.Captured;
				}

				return; // don't wanna run the next if statement after checking which mouse button
				// processing input can be expensive apparently, especially with mouse accumulation disabled, so returning asap is a good idea for performance
			}
		}

		// Releasing the mouse
		if (@event is InputEventKey key)
		{
			if (key.IsActionPressed("ui_cancel")) // this is the escape button by default, the name is misleading this is to access the ui im pretty sure
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
		}

		// do the actual aim looking
		if (@event is InputEventMouseMotion input)
		{
			_AimLook(input);
		}
  }

	// rotate the character around local y axis. we will do pitch rotation via the camera
	// the reason for this is to prevent gimbal lock, which will happen if two axes align, but if we're using two seperate gimbals (aka seperating the yaw and pitch between the head and the body) this can't happen 
	private void AddYaw(float degrees) // idk if this should be a float or not
	{
		if (Mathf.IsZeroApprox(degrees)) 
		{
			return;
		}

		// im using the built-in rotation transformation functions because that's what godot recomends
		Character.RotateY(-Mathf.DegToRad(degrees));
		Character.Orthonormalize();
	}

	// rotate the head around the local x axis in degrees to get the pitch
	private void AddPitch(float degrees)
	{
		if (Mathf.IsZeroApprox(degrees)) 
		{
			return;
		}
		// im using the built-in rotation transformation functions because that's what godot recomends
		Head.RotateX(-Mathf.DegToRad(degrees));
		Head.Orthonormalize(); 
		/*
			Orthonormalized is used to avoid floating point precisions errors from accumulation over time (the term for this is 'deformation over time').
			This means that the scale of each axis won't be exactly 1.0 anymore, and they might not be exactly 90 degrees from each other.
			By orthonormalizing the *transformation* of an object, all axes are set to a length of 1.0 and orthogonal (90 degrees) to each other again.
			Something to note here is that the scaling done by the transformation will be lost when 'Orthonormalize()' is ran.
				This is fine because it's not reccomended to scale nodes directly, rather the child of the node (like its MeshInstance3D) should be scaled.
				If you do have to scale a node you should reapply the scaling after running Orthonormalize().
		*/
	}

	// need to clamp the pitch of the head so they can't just keep moving their head up and down infinitely
	// This might be a bad implementation because according to the Godot docs you should never directly edit the Rotation parameter of a Node3D object:
	// https://docs.godotengine.org/en/latest/tutorials/3d/using_transforms.html
	// Okay so now it uses RotateObjectLocal instead of just directly setting the rotation value for X so it should be better now.
	private void _ClampPitch()
	{
		// clamp the up and down movement by doing nothing if it's past the limit
		if (Head.Rotation.X > Mathf.DegToRad(MinPitch) && Head.Rotation.X < Mathf.DegToRad(MaxPitch))
		{
			return;
		}

		// min and max pitch's in radians
		float radMinPitch = Mathf.DegToRad(MinPitch);
		float radMaxPitch = Mathf.DegToRad(MaxPitch);

		float clampedHeadXRotation = Mathf.Clamp(Head.Rotation.X, radMinPitch, radMaxPitch);

		// set the rotation if it's within the max and min bounds
		// Head.RotateX(clampedHeadXRotation);
		// Head.RotateObjectLocal(Godot.Vector3.Left, radMaxPitch); // I tried to not directly edit Head.Rotation since godot says not to but this doesn't work. all i need to do is 
		Head.Rotation = new Godot.Vector3(clampedHeadXRotation, Head.Rotation.Y, Head.Rotation.Z); 
		/* i think this only works bc it doesn't allow for input to be captured after applying it 
		bc when i try to use anything else to do the same thing it just makes it (the clamped X rotation) fluctuate between the min and max value 
		which i think is a problem of the user still pushing input when they look all the way down or up and 
		the input is read as being too far in the oppisite direction since they went past the max but for some reason 
		that issue doesn't occur when you just directly set the rotation
		*/
		Head.Orthonormalize(); 
	}

	// Function that actually does the aiming
	private void _AimLook(InputEventMouseMotion @event)
	{

		// get the viewport's transformation so that it's size independant
		Transform2D viewportTransform = GetTree().Root.GetFinalTransform(); // literally just gets the transform from the viewports' coordinate system

		Godot.Vector2 motion = ((InputEventMouseMotion)@event.XformedBy(viewportTransform)).Relative; // this is also so that it's size independant

		// i this is assuming a mouse of 1000DPI. it's 0.1% of a degree per unit. it should work nicely
		float degreesPerUnit = 0.001f; // i should make this something that they can change in the menu bc if you have a higher dpi i think this needs to be smaller
		// note that godot only allows 3 decimals by default, so idk if you actually can go lower than this

		// multiply the motion by the mouse sensitivity and then the degrees per unit
		motion *= MouseSensitivity;
		motion *= degreesPerUnit;

		// call the way and pitch methods
		AddYaw(motion.X);
		AddPitch(motion.Y);
		_ClampPitch();
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// disable mouse accumulated input becauase processing all the mouse events can translate to thousands of calls per second which is costly
		Input.UseAccumulatedInput = false;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
