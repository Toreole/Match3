using Godot;
using System;
using static Godot.OpenXRInterface;

public partial class BoardController : Panel
{
	[Export]
	private Texture2D tokenTexture;

	[Export]
	private Node gridContainer;

	[Export]
	private int boardSize = 10;
	[Export]
	private int tileSize = 50;

	[Export]
	private Color[] tokenColors;
	[Export]
	private int[] tokenWeights;

	private int weightTotal;
	private TextureRect[,] board;
	Random rand = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		board = new TextureRect[boardSize, boardSize];
		int i = 0, j = 0;
		foreach (var child in gridContainer.GetChildren())
		{
			board[i++, j] = child as TextureRect;
			if (i == boardSize)
			{
				i = 0; 
				j++;
			}
		}

		weightTotal = 0;
		foreach (int num in tokenWeights) weightTotal += num;

		foreach (var tile in board)
		{
			tile.Texture = tokenTexture;
			tile.Size = new Vector2(tileSize, tileSize);
			tile.SelfModulate = GetRandomTokenColor();
		}

	}

	private Color GetRandomTokenColor()
	{
		int r = rand.Next(weightTotal);
		for (int i = 0; i < tokenWeights.Length; i++)
		{
			r -= tokenWeights[i];
			if (r < 0)
				return tokenColors[i];
		}
		return Color.FromHsv(0, 1, 1);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (dragging)
		this.GlobalPosition = GetGlobalMousePosition() + mouseOffset;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		return (Variant)(int)TokenType.Green;
	}

	private Vector2 mouseOffset;
	private bool dragging = false;

	public override void _GuiInput(InputEvent @event)
	{
		base._GuiInput(@event);
		if( @event is InputEventMouseButton mouse)
		{
			GD.Print(mouse.AsText());
			if(mouse.IsPressed())
			{
				this.mouseOffset = this.GlobalPosition - mouse.GlobalPosition;
				dragging = true;
			}
			else
			{
				dragging = false;
			}
		}
	}
	public override bool _HasPoint(Vector2 point)
	{
		if (dragging) return true;
		return base._HasPoint(point);
	}
}

public enum TokenType
{
	Green, Blue, Orange, Red, Pink, Purple, Teal, Gold
}
