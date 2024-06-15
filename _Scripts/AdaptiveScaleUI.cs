using Godot;
using System;

public partial class AdaptiveScaleUI : Control
{
	private Vector2I lastScreenSize;
	private float referenceHeight;

	public override void _Ready()
	{
		base._Ready();
		referenceHeight = this.Size.Y;
		lastScreenSize = GetTree().Root.Size;
		//Resize();
	}
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		base._Process(delta);
		var currentSize = GetTree().Root.Size;
		if (currentSize != lastScreenSize)
		{
			lastScreenSize = currentSize;
			//Resize();
		}
	}

	private void Resize()
	{
		float scale = (float)lastScreenSize.Y / referenceHeight;
		Scale = new Vector2(scale, scale);
	}
}
