using Godot;
using System;

public partial class OarsContainer : Node3D
{

	// the 4 oars that we have
	Node3D oarBackRight = new Node3D();
	Node3D oarBackLeft = new Node3D();
	Node3D oarFrontRight = new Node3D();
	Node3D oarFrontLeft = new Node3D();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// set the oar variables to actually be the right nodes
		oarBackRight = GetNode<Node3D>("OarBackRight");
		oarBackLeft = GetNode<Node3D>("OarBackLeft");
		oarFrontRight = GetNode<Node3D>("OarFrontRight");
		oarFrontLeft = GetNode<Node3D>("OarFrontLeft");

		// subscribe to the oar rowing signal from the global signal server
		GlobalSignalServer.Instance.AnimateOar += AnimateOar;
	}

	/*
	* Given a seat index and a direciton, determine how to play the animation.
	* int direction: 1 = forward | -1 = backwards
	* bool startStop: true = start animation | false = stop animation
	*/
	private void AnimateOar(int seat, int direction, bool startStop)
	{
		// this is just making the seat index into an enum so the if statement is more readable
		Boat.SeatIndicies seatIndex = (Boat.SeatIndicies)seat;

		int leftOarDirection = -direction; // the left oars are mirrored so they need to play the animation backwards

		// to reset the postition of the oar we need to play the 'RESET' animation
		string animationName = startStop ? "oar_rowing" : "idle";

		// transition time
		float transitionTime = 0.5f;

		// animation players
		AnimationPlayer oarFrontRightAnimationPlayer = oarFrontRight.GetNode<AnimationPlayer>("AnimationPlayer");
		AnimationPlayer oarFrontLeftAnimationPlayer = oarFrontLeft.GetNode<AnimationPlayer>("AnimationPlayer");
		AnimationPlayer oarBackRightAnimationPlayer = oarBackRight.GetNode<AnimationPlayer>("AnimationPlayer");
		AnimationPlayer oarBackLeftAnimationPlayer = oarBackLeft.GetNode<AnimationPlayer>("AnimationPlayer");

		// based on the seat and direction, trigger the animation
		// this can be an else if block because only one will be triggered per call to this function
		// only trigger the animation if we're not already animating it
		if (seatIndex == Boat.SeatIndicies.FrontRight && oarFrontRightAnimationPlayer.CurrentAnimation != animationName)
		{
			oarFrontRightAnimationPlayer.Play(animationName, customSpeed: direction, customBlend: transitionTime);
		}
		else if (seatIndex == Boat.SeatIndicies.BackRight && oarBackRightAnimationPlayer.CurrentAnimation != animationName)
		{
			oarBackRightAnimationPlayer.Play(animationName, customSpeed: direction, customBlend: transitionTime);
		}
		// the left side needs to be played in reverse to look like its rowing forward
		else if (seatIndex == Boat.SeatIndicies.FrontLeft && oarFrontLeftAnimationPlayer.CurrentAnimation != animationName)
		{
			oarFrontLeftAnimationPlayer.Play(animationName, customSpeed: leftOarDirection, customBlend: transitionTime);
		}
		else if (seatIndex == Boat.SeatIndicies.BackLeft && oarBackLeftAnimationPlayer.CurrentAnimation != animationName)
		{
			oarBackLeftAnimationPlayer.Play(animationName, customSpeed: leftOarDirection, customBlend: transitionTime);
		}
	}
}
