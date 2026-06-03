using Godot;

namespace RougeliteIdle.Core;

public partial class LogWindowManager : Node
{
	private Window? _window;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>("res://scenes/ui/log/log_window.tscn");
		if (scene == null)
		{
			GD.PushWarning("Log window scene not found.");
			return;
		}

		_window = scene.Instantiate<Window>();
		_window.Visible = false;
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _window);
	}

	public void Show()
	{
		if (_window == null)
		{
			return;
		}

		if (_window.HasMethod("show_log"))
		{
			_window.Call("show_log");
		}
	}

	public void Toggle()
	{
		if (_window == null)
		{
			return;
		}

		if (_window.Visible)
		{
			if (_window.HasMethod("hide_log"))
			{
				_window.Call("hide_log");
			}
			else
			{
				_window.Hide();
			}
		}
		else
		{
			Show();
		}
	}
}
