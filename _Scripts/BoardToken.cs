using Godot;
using System;

public partial class BoardToken : TextureRect
{
	private TextureRect? tempRect;

	internal BoardController BoardController { get; set; }
	internal Vector2I GridPosition { get; set; }
	internal TokenType TokenType { get; set; }

	public override Variant _GetDragData(Vector2 atPosition)
	{
		BoardController.StartTokenDrag(this);
		GD.Print("Get Drag Data");
		return this.Name;
	}
	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.StringName;
	}
	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if(data.VariantType == Variant.Type.StringName)
		{
			GD.Print($"{Name}: drop received from {data.AsString()}");
			BoardController.EndTokenDrag(this).ConfigureAwait(false);
		}
	}

	struct TokenInfo
	{
		public StringName name;
		public int x, y;
		public TokenType type;
	}
}
