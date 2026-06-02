using Godot;

namespace RougeliteIdle.Run;

public partial class BroadcastWindowManager : Node
{
	private Window? _window;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>("res://scenes/layers/broadcast_window.tscn");
		if (scene == null)
		{
			GD.PushWarning("Broadcast window scene not found.");
			return;
		}

		_window = scene.Instantiate<Window>();
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _window);
	}
}
