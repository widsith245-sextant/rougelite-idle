using Godot;

namespace RougeliteIdle.Run;

public partial class PortraitWindowManager : Node
{
	private Window? _window;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>("res://scenes/ui/popup/portrait_window.tscn");
		if (scene == null)
		{
			GD.PushWarning("Portrait window scene not found.");
			return;
		}

		_window = scene.Instantiate<Window>();
		_window.Visible = false;
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _window);
	}

	public void ShowPortrait(string unitId, bool damaged = false)
	{
		if (_window == null)
		{
			return;
		}

		if (_window.HasMethod("show_portrait"))
		{
			_window.Call("show_portrait", unitId, damaged);
		}
	}
}
