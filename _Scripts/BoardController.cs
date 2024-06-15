using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BoardController : Panel
{
	[Export]
	private Texture2D tokenTexture;

	[Export]
	private Node gridContainer;
	[Export]
	private Label scoreLabel;

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

	private int score = 0;
	private int scoreMultiplierAccumulator = 0;
	private float scoreMultiplier = 1;
	private TokenType preferredToken;
	private TokenType dislikedToken;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		preferredToken = (TokenType)rand.Next(4);
		dislikedToken = (TokenType)(((int)preferredToken + 2) % 4);
		GD.Print($"good: {preferredToken}, bad: {dislikedToken}");

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

	internal async Task EndTokenDrag(BoardToken target)
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

		var matches = FindMatches(startPos, endPos);
		if (matches.Count > 0)
		{
			ScoreMatches(matches);
			await ApplyGravity(matches);
			await RefillEmptyTiles();

			//continue chains as long as matches exist.
			int iteration = 0;
			while (matches.Count > 0)
			{
				await Task.Delay(600);
				GD.Print($"FindAllMatches({iteration++})");
				matches = FindAllMatches();
				ScoreMatches(matches);
				await ApplyGravity(matches);
				await RefillEmptyTiles();
			}

		}
	}

	//finds ALL matches on the board.
	//important note: this does NOT maximize match sizes!
	//if some tokens drop into a n L shape (5-match), it will only find one direction, and leave the other 2 tokens intact.
	private List<TokenMatch> FindAllMatches()
	{
		List<TokenMatch> matches = new(boardSize * 3);
		for(int x = 0; x < boardSize; x++)
		{
			matches.AddRange(FindMatches(new(x, 0), new(x, boardSize - 1)));
		}
		return matches;
	}

	/// <summary>
	/// finds matches on a line (based on a move) and marks matched tokens as "Empty"
	/// </summary>
	/// <param name="start"></param>
	/// <param name="end"></param>
	private List<TokenMatch> FindMatches(Vector2I start, Vector2I end)
	{
		var offset = end - start;
		int distance = Mathf.Abs(offset.X) + Mathf.Abs(offset.Y);
		List<TokenMatch> matches = new(distance);
		int xdirection = Mathf.Sign(offset.X);
		int ydirection = Mathf.Sign(offset.Y);
		int posX, posY;
		int xsize,ysize;

		for (int i = 0; i <= distance; i++)
		{
			xsize = 0;
			ysize = 0;
			posX = start.X + xdirection * i;
			posY = start.Y + ydirection * i;
			var matchType = board[posX, posY].TokenType;

			if (matchType == TokenType.Empty)
				continue;

			//GD.Print($"check for match in ({posX}, {posY})");
			for (int x = posX - 1; x >= 0 && board[x, posY].TokenType == matchType; x--)
				xsize++;
			int xoffset = -xsize;
			for (int x = posX + 1; x < boardSize && board[x, posY].TokenType == matchType; x++)
				xsize++;

			for (int y = posY - 1; y >= 0 && board[posX, y].TokenType == matchType; y--)
				ysize++;
			int yoffset = -ysize;
			for (int y = posY + 1; y < boardSize && board[posX, y].TokenType == matchType; y++)
				ysize++;

			bool isMatch = ysize >= 2 || xsize >= 2;
			//reset sizes to 0 if not a match in that direction.
			if (ysize < 2) ysize = 0;
			if (xsize < 2) xsize = 0;
			//matchType and matchSize together determine what you get for a match.
			int matchSize = 1 + ysize + xsize;

			if(isMatch)
			{
				matches.Add(new()
				{
					totalSize = matchSize,
					pos = new(posX, posY),
					xSize = xsize + 1,
					ySize = ysize + 1,
					xOffset = xoffset,
					yOffset = yoffset,
					tokenType = matchType
				});
				GD.Print($"Found match around ({posX}, {posY}) with sizes ({xsize}, {ysize}) and token {matchType}");
				if (xsize >= 2)
				{
					for(int dx = 0; dx <= xsize; dx++)
					{
						int x = posX + xoffset + dx;
						//GD.Print($"empty ({x}, {start.Y})");
						board[x, posY].TokenType = TokenType.Empty;
						board[x, posY].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
				if (ysize >= 2)
				{
					for (int dy = 0; dy <= ysize; dy++)
					{
						int y = posY + yoffset + dy;
						//GD.Print($"empty ({start.X}, {y})");
						board[posX, y].TokenType = TokenType.Empty;
						board[posX, y].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
			}
		}
		return matches;
	}

	//searches through the rows and randomizes any empty tiles as other tokens.
	private async Task RefillEmptyTiles()
	{
		bool rowHadEmpty;
		for(int y = 0; y < boardSize; y++)
		{
			rowHadEmpty = false;
			for(int x = 0; x < boardSize; x++)
			{
				if (board[x,y].TokenType == TokenType.Empty)
				{
					board[x, y].SelfModulate = GetRandomTokenColor(out var tokenType);
					board[x, y].TokenType = tokenType;
					rowHadEmpty = true;
					await Task.Delay(200);
				}
			}
			if (!rowHadEmpty)
				return;
		}
		await Task.Delay(1000);
	}

	//just grants scores and bonuses etc. depending on match type and match size.
	private void ScoreMatches(List<TokenMatch> matches)
	{
		foreach(var match in matches)
		{
			if (match.tokenType <= TokenType.Green)
			{
				score += (int)(scoreMultiplier * multiplier(match.totalSize) * tokenMultiplier(match.tokenType) * match.totalSize * 10);
			}
			else if (match.tokenType == TokenType.Pink)
			{
				scoreMultiplierAccumulator += (int)(multiplier(match.totalSize) * match.totalSize * 5);
				if(scoreMultiplierAccumulator > 30)
				{
					scoreMultiplierAccumulator -= 30;
					scoreMultiplier += 0.25f;
				}
			}
		}
		scoreLabel.Text = $"{score} (x{scoreMultiplier:0.00})";

		float multiplier(int matchSize) => matchSize switch
		{
			3 => 1,
			4 => 1.15f,
			5 => 1.20f,
			6 => 1.25f,
			7 => 1.30f,
			_ =>1,

		};
		float tokenMultiplier(TokenType t) => (t == preferredToken) ? 1.2f : (t == dislikedToken) ? 0.7f : 1f; 
	}

	//the list of matches can be used to generate a list of tokens to move down based on y positions.
	private async Task ApplyGravity(List<TokenMatch> matches)
	{
		await Task.Delay(600);
		matches.Sort((a, b) => a.pos.Y.CompareTo(b.pos.Y));
		//move tokens above the match down by the matches (ysize + 1)
		foreach(var match in matches)
		{
			for(int x = match.pos.X + match.xOffset; x < match.pos.X + match.xOffset + match.xSize; x++)
			{
				if (x == match.pos.X)
				{
					for (int y = match.pos.Y + match.yOffset - 1; y >= 0; y--)
					{
						board[x, y + match.ySize].TokenType = board[x, y].TokenType;
						board[x, y + match.ySize].SelfModulate = board[x, y].SelfModulate;

						board[x, y].TokenType = TokenType.Empty;
						board[x, y].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
				else
				{
					for (int y = match.pos.Y - 1; y >= 0; y--)
					{
						board[x, y + 1].TokenType = board[x, y].TokenType;
						board[x, y + 1].SelfModulate = board[x, y].SelfModulate;

						board[x, y].TokenType = TokenType.Empty;
						board[x, y].SelfModulate = Color.FromHsv(0, 0, 1);
					}
				}
				await Task.Delay(600);
			}
		}
	}

	private struct TokenMatch
	{
		public Vector2I pos;
		public int xOffset, yOffset, xSize, ySize, totalSize;
		public TokenType tokenType;
	}
}


public enum TokenType
{
	Red, Orange, Blue, Green, Purple, Teal, Pink, Gold, Empty = -1
}
