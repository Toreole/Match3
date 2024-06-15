using Godot;
using System;

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
	private BoardToken[,] board;
	Random rand = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		board = new BoardToken[boardSize, boardSize];
		int i = 0, j = 0;
		foreach (var child in gridContainer.GetChildren())
		{
			var c = child as BoardToken;
			c.GridPosition = new Vector2I(i, j);
			board[i++, j] = c;
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
			tile.BoardController = this;
			tile.Texture = tokenTexture;
			tile.Size = new Vector2(tileSize, tileSize);
			tile.SelfModulate = GetRandomTokenColor(out var t);
			tile.TokenType = t;
		}

	}

	private Color GetRandomTokenColor(out TokenType tokenType)
	{
		int r = rand.Next(weightTotal);
		for (int i = 0; i < tokenWeights.Length; i++)
		{
			r -= tokenWeights[i];
			if (r < 0)
			{
				tokenType = (TokenType)i;
				return tokenColors[i];
			}
		}
		tokenType = 0;
		return Color.FromHsv(0, 1, 1);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		//if (dragging)
		//this.GlobalPosition = GetGlobalMousePosition() + mouseOffset + ((float)delta * mouseVelocity);
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		return (Variant)(int)TokenType.Green;
	}

	private Vector2 mouseOffset;
	private Vector2 mouseVelocity;
	private bool dragging = false;

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (@event is InputEventMouseButton mouse)
		{
			//GD.Print("mouse: " + mouse.IsPressed());
			if(dragging && !mouse.IsPressed())
			{
				dragging = false;
			}
		}
		if (@event is InputEventMouseMotion motion)
		{
			mouseVelocity = motion.Velocity;
		}
	}

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
		}
	}

	BoardToken draggedToken;

	internal void StartTokenDrag(BoardToken token)
	{
		draggedToken = token;
	}

	internal void EndTokenDrag(BoardToken target)
	{
		var startPos = draggedToken.GridPosition;
		var endPos = target.GridPosition;

		//same position, or they dont share any coordinate
		if (startPos == endPos || (startPos.X != endPos.X && startPos.Y != endPos.Y))
			return;

		var tokenColor = draggedToken.SelfModulate;
		var tokenType = draggedToken.TokenType;

		GD.Print($"moving {startPos} to {endPos}");

		// because one is always 0.
		int distance = Mathf.Abs(endPos.X - startPos.X) + Mathf.Abs(endPos.Y - startPos.Y);
		int xdirection = Mathf.Sign(endPos.X - startPos.X);
		int ydirection = Mathf.Sign(endPos.Y - startPos.Y);
		int posX = startPos.X;
		int posY = startPos.Y;

		for (int i = 0; i < distance; i++)
		{
			board[posX, posY].SelfModulate = board[posX + xdirection, posY + ydirection].SelfModulate;
			board[posX, posY].TokenType = board[posX + xdirection, posY + ydirection].TokenType;
			posX += xdirection;
			posY += ydirection;
		}
		target.SelfModulate = tokenColor;
		target.TokenType = tokenType;

		FindCombinations(startPos, endPos);
	}

	private void FindCombinations(Vector2I start, Vector2I end)
	{
		int distance = Mathf.Abs(end.X - start.X) + Mathf.Abs(end.Y - start.Y);
		int xdirection = Mathf.Sign(end.X - start.X);
		int ydirection = Mathf.Sign(end.Y - start.Y);
		int posX = start.X;
		int posY = start.Y;

		for (int i = 0; i <= distance; i++)
		{
			int ysize = 0;
			int xsize = 0;
			var currentToken = board[posX, posY].TokenType;

			GD.Print($"check for match in ({posX}, {posY})");
			for (int x = posX - 1; x >= 0 && board[x, posY].TokenType == currentToken; x--)
				xsize++;
			int xoffset = -xsize;
			for (int x = posX + 1; x < boardSize && board[x, posY].TokenType == currentToken; x++)
				xsize++;

			for (int y = posY - 1; y >= 0 && board[posX, y].TokenType == currentToken; y--)
				ysize++;
			int yoffset = -ysize;
			for (int y = posY + 1; y < boardSize && board[posX, y].TokenType == currentToken; y++)
				ysize++;

			bool isMatch = ysize >= 2 || xsize >= 2;

			int matchSize = isMatch ? 1+(ysize >= 2 ? ysize: 0)+(xsize >= 2 ? xsize : 0) : 0;

			if(isMatch)
			{
				GD.Print($"Found match around ({posX}, {posY}) with sizes ({xsize}, {ysize}) and token {currentToken}");
				if (xsize >= 2)
				{
					for(int dx = 0; dx <= xsize; dx++)
					{
						int x = posX + xoffset + dx;
						GD.Print($"empty ({x}, {start.Y})");
						board[x, posY].TokenType = TokenType.Empty;
						board[x, posY].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
				if (ysize >= 2)
				{
					for (int dy = 0; dy <= ysize; dy++)
					{
						int y = posY + yoffset + dy;
						GD.Print($"empty ({start.X}, {y})");
						board[posX, y].TokenType = TokenType.Empty;
						board[posX, y].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
			}

			posX += xdirection;
			posY += ydirection;
		}
	}

}

public enum TokenType
{
	Red, Orange, Blue, Green, Purple, Teal, Pink, Gold, Empty = -1
}
