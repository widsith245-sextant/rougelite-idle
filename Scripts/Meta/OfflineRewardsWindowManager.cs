using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Save;

namespace RougeliteIdle.Meta;

public partial class OfflineRewardsWindowManager : Node
{
	private Window? _window;
	private OfflineIdleResult? _pending;
	private bool _windowReady;

	public bool HasPending => _pending != null;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>("res://scenes/ui/offline/offline_rewards_window.tscn");
		if (scene == null)
		{
			GD.PushWarning("Offline rewards window scene not found.");
			return;
		}

		_window = scene.Instantiate<Window>();
		_window.Visible = false;
		_window.TreeEntered += OnWindowTreeEntered;
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _window);
	}

	public void ShowPending(OfflineIdleResult result)
	{
		_pending = result;
		TryShowWindow();
	}

	public void ShowPendingIfAny()
	{
		TryShowWindow();
	}

	public void ClaimPending()
	{
		if (_pending == null)
		{
			return;
		}

		var offline = GetNode<OfflineIdleManager>("/root/OfflineIdleManager");
		var progression = GetNode<ProgressionManager>("/root/ProgressionManager");
		var roster = GetNode<RosterProgressionManager>("/root/RosterProgressionManager");
		offline.ApplyRewards(_pending, progression, roster);
		_pending = null;
		GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap")?.RequestSave();
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log(
			"Offline",
			GameLogger.LogLevel.Info,
			"Offline rewards claimed");
	}

	public void DismissPending()
	{
		_pending = null;
		if (_window == null)
		{
			return;
		}

		if (_window.HasMethod("hide_window"))
		{
			_window.Call("hide_window");
		}
		else
		{
			_window.Hide();
		}
	}

	private void OnWindowTreeEntered()
	{
		if (_window == null || !_window.IsInsideTree())
		{
			return;
		}

		_windowReady = true;
		TryShowWindow();
	}

	private void TryShowWindow()
	{
		if (_window == null || _pending == null || !_windowReady || !_window.IsInsideTree())
		{
			return;
		}

		var offline = GetNode<OfflineIdleManager>("/root/OfflineIdleManager");
		var payload = offline.ResultToDictionary(_pending);
		if (_window.HasMethod("show_pending"))
		{
			_window.CallDeferred("show_pending", payload);
		}
	}
}
