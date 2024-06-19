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
	// using the Godot class for its .State property.
	private RandomNumberGenerator rand = new();

	private int score = 0;
	private int scoreMultiplierAccumulator = 0;
	private float scoreMultiplier = 1;
	private TokenType preferredToken;
	private TokenType dislikedToken;

	private bool acceptsInput = true;

	private TextureRect imposter;

	private TokenType[,] snapshot;
	private ulong randomState;

	BoardToken hoveringTile = null;
	private BoardToken draggedToken = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		imposter = CreateUninteractableTokenAsChild();
		imposter.Visible = false;

		preferredToken = (TokenType)rand.RandiRange(0, 3);
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
			tile.MouseEntered += () => SetCurrentHover(tile);

			//initialize gameplay stuff. to be moved elsewhere
			tile.SelfModulate = GetRandomTokenColor(out var t);
			tile.TokenType = t;
		}
	}

	private Color GetRandomTokenColor(out TokenType tokenType)
	{
		int r = rand.RandiRange(0, weightTotal+1);
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
		imposter.GlobalPosition = GetGlobalMousePosition() - new Vector2(25, 25);
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
	}

	public override void _GuiInput(InputEvent @event)
	{
		base._GuiInput(@event);
	}

	internal void StartTokenDrag(BoardToken token)
	{
		if (!acceptsInput)
			return;
		var tilePos = token.GridPosition;
		//init vistokens for move.
		for (int x = 0; x < boardSize; x++)
		{
			if (x == tilePos.X) continue;
			tempVisTokens.Add(CreateVisToken(board[x, tilePos.Y]));
		}
		for (int y = 0;  y < boardSize; y++)
		{
			if (y == tilePos.Y) continue;
			tempVisTokens.Add(CreateVisToken(board[tilePos.X, y]));
		}

		draggedToken = token;
		imposter.Visible = true;
		imposter.SelfModulate = draggedToken.SelfModulate;
		draggedToken.SelfModulate = Color.FromHsv(0, 0, 0, 0);
	}

	private TempVisToken CreateVisToken(BoardToken tile)
	{
		var visToken = new TempVisToken()
		{
			original = tile,
			fakeVisual = CreateTemporaryToken(tile.TokenType),
			currentGridPos = tile.GridPosition
		};
		visToken.fakeVisual.GlobalPosition = tile.GlobalPosition;
		tile.SelfModulate = Color.FromHsv(0, 0, 0, 0);
		return visToken;
	}
	private TextureRect CreateUninteractableTokenAsChild()
	{
		var rect = new TextureRect
		{
			Texture = tokenTexture,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new(50, 50),
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		AddChild(rect);
		return rect;
	}

	internal async Task EndTokenDrag(BoardToken target)
	{
		if (!acceptsInput || draggedToken == null)
			return;

		//cleanup tempVisTokens at end of move.
		foreach (var visToken in tempVisTokens)
		{
			visToken.original.SelfModulate = tokenColors[(int)visToken.original.TokenType];
			visToken.fakeVisual.QueueFree();
		}
		tempVisTokens.Clear();

		imposter.Visible = false;
		var startPos = draggedToken.GridPosition;
		var endPos = target.GridPosition;

		GD.Print($"try moving {startPos} to {endPos}");
		//same position, or they dont share any coordinate
		if (startPos == endPos || (startPos.X != endPos.X && startPos.Y != endPos.Y))
		{
			draggedToken.SelfModulate = tokenColors[(int)draggedToken.TokenType];
			draggedToken = null;
			return;
		}

		var tokenType = draggedToken.TokenType;
		var tokenColor = tokenColors[(int)tokenType];

		acceptsInput = false;

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
			RefillEmptyTiles(); //refill takes 0.8s

			//continue chains as long as matches exist.
			int iteration = 0;
			while (matches.Count > 0)
			{
				await Task.Delay(900);
				GD.Print($"FindAllMatches({iteration++})");
				matches = FindAllMatches();
				ScoreMatches(matches);
				await ApplyGravity(matches);
				RefillEmptyTiles(); //refill takes 0.8s
			}

		}
		acceptsInput = true;
		draggedToken = null;
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
			//reset sizes and offset to 0 if not a match in that direction.
			if (ysize < 2) { ysize = 0; yoffset = 0; }
			if (xsize < 2) { xsize = 0; xoffset = 0; }
			//matchType and matchSize together determine what you get for a match.
			int matchSize = 1 + ysize + xsize;

			if (!isMatch)
				continue;
			
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
					EmptyTile(x, posY);
				}
			}
			if (ysize >= 2)
			{
				for (int dy = 0; dy <= ysize; dy++)
				{
					int y = posY + yoffset + dy;
					//GD.Print($"empty ({start.X}, {y})");
					EmptyTile(posX, y);
				}
			}
			
		}
		return matches;
	}

	//searches through the rows and randomizes any empty tiles as other tokens.
	private void RefillEmptyTiles()
	{
		bool rowHadEmpty;
		for(int y = 0; y < boardSize; y++)
		{
			rowHadEmpty = false;
			for(int x = 0; x < boardSize; x++)
			{
				if (board[x, y].TokenType != TokenType.Empty)
					continue;
				
				var tokenColor = GetRandomTokenColor(out var tokenType);
				//tween in a new token
				TextureRect rec = CreateUninteractableTokenAsChild();
				rec.SelfModulate = tokenColor;
				rec.GlobalPosition = board[x, y].GlobalPosition + new Vector2(0, -500);

				var tween = GetTree().CreateTween();
				tween.TweenProperty(rec, "global_position", board[x, y].GlobalPosition, 0.8f)
					.SetEase(Tween.EaseType.InOut)
					.SetTrans(Tween.TransitionType.Sine);
				tween.TweenCallback(Callable.From(rec.QueueFree));
				int bufferx = x, buffery = y;
				tween.TweenCallback(Callable.From(() => {
					board[bufferx, buffery].SelfModulate = tokenColor;
					board[bufferx, buffery].TokenType = tokenType;
				}));

				rowHadEmpty = true;
				//await Task.Delay(200);
				
			}
			if (!rowHadEmpty)
				return;
		}
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
				while(scoreMultiplierAccumulator > 30)
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
		matches.Sort((a, b) => a.pos.Y.CompareTo(b.pos.Y));
		//move tokens above the match down by the matches (ysize + 1)
		foreach(var match in matches)
		{
			for(int x = match.pos.X + match.xOffset; x < match.pos.X + match.xOffset + match.xSize; x++)
			{
				for (int y = match.pos.Y + ((x == match.pos.X) ? match.yOffset : 0) - 1; y >= 0; y--)
				{
					var rec = CreateUninteractableTokenAsChild();
					rec.SelfModulate = board[x, y].SelfModulate;
					rec.GlobalPosition = board[x, y].GlobalPosition;

					int xbuffer = x, ybuffer = y + ((x == match.pos.X)? match.ySize : 1);
					var tokenType = board[x, y].TokenType;

					var tween = GetTree().CreateTween();
					tween.TweenProperty(rec, "global_position", board[xbuffer, ybuffer].GlobalPosition, 0.2)
						.SetEase(Tween.EaseType.InOut)
						.SetTrans(Tween.TransitionType.Sine);
					tween.TweenCallback(Callable.From(() => {
						board[xbuffer, ybuffer].SelfModulate = tokenColors[(int)tokenType];
					}));
					tween.TweenCallback(Callable.From(rec.QueueFree));

					board[xbuffer, ybuffer].TokenType = tokenType;
					//board[xbuffer, ybuffer].SelfModulate = board[x, y].SelfModulate;

					EmptyTile(x, y);
				}
			}
		}
		await Task.Delay(300);
	}

	private void EmptyTile(int x, int y)
	{
		board[x, y].TokenType = TokenType.Empty;
		board[x, y].SelfModulate = Color.FromHsv(0, 0, 0, 0);
	}

	private List<TempVisToken> tempVisTokens = new();

	internal void SetCurrentHover(BoardToken tile)
	{
		if (draggedToken == null)
		{
			if (hoveringTile != tile)
			{
				if (hoveringTile != null)
					hoveringTile.Modulate = Color.FromHsv(0, 0, 1f);
				hoveringTile = tile;
				hoveringTile.Modulate = Color.FromHsv(0, 0, 0.8f);
				GD.Print($"Now hovering over {tile.Name}");
			}
			return;
		}
		if (!acceptsInput)
			return;
		UpdateTempVisTokens(tile.GridPosition);
	}

	private void UpdateTempVisTokens(Vector2I hoverPosition)
	{
		// currently dragging a token. animate other tokens on its path to preemptively move
		var dragStartPos = draggedToken.GridPosition;
		//skip if they dont share a lane.
		if (dragStartPos.X != hoverPosition.X && dragStartPos.Y != hoverPosition.Y)
			return;

		foreach (var visToken in tempVisTokens)
		{
			var originalPos = visToken.original.GridPosition;
			var currentPos = visToken.currentGridPos;
			if (hoverPosition == draggedToken.GridPosition)
			{
				visToken.currentGridPos = originalPos;
			}
			// set new position for tile when necessary.
			else if (dragStartPos.Y == originalPos.Y)
			{
				if (dragStartPos.X < originalPos.X) //dragged from left to right. should move left.
				{
					visToken.currentGridPos = (hoverPosition.X >= originalPos.X) ? (originalPos + new Vector2I(-1, 0)) : originalPos;
				}
				else if (dragStartPos.X > originalPos.X) //dragged from right to left. should move right.
				{
					visToken.currentGridPos = (hoverPosition.X <= originalPos.X) ? (originalPos + new Vector2I(1, 0)) : originalPos;
				}
			}
			else //if (dragStartPos.X == originalPos.X) //condition is guaranteed by precondition above.
			{
				if (dragStartPos.Y < originalPos.Y) //dragged from top to bottom. should move up.
				{
					visToken.currentGridPos = (hoverPosition.Y >= originalPos.Y) ? (originalPos + new Vector2I(0, -1)) : originalPos;
				}
				else if (dragStartPos.Y > originalPos.Y)//dragged from bottom to top. should move down
				{
					visToken.currentGridPos = (hoverPosition.Y <= originalPos.Y) ? (originalPos + new Vector2I(0, 1)) : originalPos;
				}
			}
			// check for change, animate to new position.
			if (visToken.currentGridPos != currentPos)
			{
				var tween = GetTree().CreateTween();
				tween.TweenProperty(visToken.fakeVisual, "global_position", board[visToken.currentGridPos.X, visToken.currentGridPos.Y].GlobalPosition, 0.15f)
					.SetEase(Tween.EaseType.InOut)
					.SetTrans(Tween.TransitionType.Sine);
			}
		}
	}

	private TextureRect CreateTemporaryToken(TokenType type)
	{
		var val = new TextureRect
		{
			Texture = tokenTexture,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new(50, 50),
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			SelfModulate = tokenColors[(int)type]
		};
		AddChild(val);
		return val;
	}

	private class TempVisToken
	{
		public BoardToken original;
		public TextureRect fakeVisual;
		public Vector2I currentGridPos;
	}

	public void OnSnapshotButtonDown()
	{
		snapshot = new TokenType[boardSize, boardSize];
		for (int x = 0; x < boardSize; x++)
			for (int y = 0; y < boardSize; y++)
				snapshot[x, y] = board[x, y].TokenType;
		randomState = rand.State;
	}

	public void OnReloadButtonDown()
	{
		if (!acceptsInput)
			return;
		rand.State = randomState;
		foreach(var tile in board)
		{
			tile.TokenType = snapshot[tile.GridPosition.X, tile.GridPosition.Y];
			tile.SelfModulate = this.tokenColors[(int)tile.TokenType];
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
