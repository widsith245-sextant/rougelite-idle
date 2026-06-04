using Godot;
using RougeliteIdle.Core;
using RougeliteIdle.Loot;
using RougeliteIdle.Save;

namespace RougeliteIdle.Run;

public partial class RunSettlementWindowManager : Node
{
	private Window? _window;
	private RunSettlementResult? _pending;

	public override void _Ready()
	{
		var scene = GD.Load<PackedScene>("res://scenes/ui/run/run_settlement_window.tscn");
		if (scene == null)
		{
			GD.PushWarning("Run settlement window scene not found.");
			return;
		}

		_window = scene.Instantiate<Window>();
		_window.Visible = false;
		GetTree().Root.CallDeferred(Node.MethodName.AddChild, _window);
	}

	public void ShowSettlement(RunSettlementResult result, int goldGrant, float expGrant, string chestQuality)
	{
		_pending = result;
		if (_window == null)
		{
			return;
		}

		var stats = GetNode<RunStatsAggregator>("/root/RunStatsAggregator");
		var payload = stats.ResultToDictionary(result);
		payload["gold_grant"] = goldGrant;
		payload["exp_grant"] = expGrant;
		payload["chest_quality"] = chestQuality;

		if (_window.HasMethod("show_settlement"))
		{
			_window.Call("show_settlement", payload);
		}
	}

	public void ClaimSettlement()
	{
		if (_pending == null)
		{
			return;
		}

		var loot = GetNode<LootManager>("/root/LootManager");
		loot.GrantRunSettlementChest(_pending.Value.ChestQuality);
		_pending = null;
		GetNodeOrNull<SaveBootstrap>("/root/SaveBootstrap")?.RequestSave();
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.Log(
			"Run",
			GameLogger.LogLevel.Info,
			"Run settlement chest claimed");
	}

	public void Dismiss()
	{
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
}
