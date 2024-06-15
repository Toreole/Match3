using Godot;
using System;

public partial class BoardToken : TextureRect
{
	private TextureRect? tempRect;

	internal BoardController BoardController { get; set; }
	internal Vector2I GridPosition { get; set; }
	internal TokenType TokenType { get; set; }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

	}

	public override void _GuiInput(InputEvent @event)
	{
		base._GuiInput(@event);
		if(@event is InputEventMouseButton mouse)
		{
			if(mouse.IsPressed())
			{
				GD.Print($"Start on {this.Name}");
			}
			else
			{
				GD.Print($"End on {this.Name}");
			}
		}
	}
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
			BoardController.EndTokenDrag(this);
			GD.Print($"{Name}: drop received from {data.AsString()}");
		}
	}

	struct TokenInfo
	{
		public StringName name;
		public int x, y;
		public TokenType type;
	}
}
