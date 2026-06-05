using System;
using Godot;
using RougeliteIdle.Save;

namespace RougeliteIdle.Meta;

public partial class OfflineIdleManager : Node
{
	private const long MaxOfflineElapsedSeconds = 7L * 24 * 3600;

	public void StampSessionTime()
	{
		var save = GetNode<SaveManager>("/root/SaveManager");
		save.SaveGame();
	}

	public OfflineIdleResult CalculateOfflineRewards(long lastSessionUnix, int stageLevel)
	{
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var elapsedSeconds = Math.Max(0, now - lastSessionUnix);
		if (elapsedSeconds > MaxOfflineElapsedSeconds)
		{
			elapsedSeconds = MaxOfflineElapsedSeconds;
		}

		return new OfflineIdleResult
		{
			ElapsedSeconds = elapsedSeconds,
			Gold = elapsedSeconds * 0.5f * stageLevel,
			Experience = elapsedSeconds * 0.2f * stageLevel,
			Chests = (int)(elapsedSeconds / 300),
		};
	}

	public void ApplyRewards(
		OfflineIdleResult result,
		ProgressionManager progression,
		RosterProgressionManager rosterProgression)
	{
		var goldGrant = Mathf.RoundToInt(result.Gold);
		if (goldGrant > 0)
		{
			progression.AddGold(goldGrant);
		}

		if (result.Experience > 0.01f)
		{
			rosterProgression.GrantExpToActiveSquad(result.Experience);
		}
	}

	public string FormatElapsed(long seconds)
	{
		if (seconds <= 0)
		{
			return "0秒";
		}

		var hours = seconds / 3600;
		var minutes = (seconds % 3600) / 60;
		var secs = seconds % 60;
		if (hours > 0)
		{
			return minutes > 0 ? $"{hours}小时{minutes}分" : $"{hours}小时";
		}

		if (minutes > 0)
		{
			return secs > 0 ? $"{minutes}分{secs}秒" : $"{minutes}分";
		}

		return $"{secs}秒";
	}

	public Godot.Collections.Dictionary ResultToDictionary(OfflineIdleResult result)
	{
		return new Godot.Collections.Dictionary
		{
			{ "elapsed_seconds", result.ElapsedSeconds },
			{ "elapsed_label", FormatElapsed(result.ElapsedSeconds) },
			{ "gold", Mathf.RoundToInt(result.Gold) },
			{ "experience", result.Experience },
			{ "chests", result.Chests },
		};
	}
}

public class OfflineIdleResult
{
	public long ElapsedSeconds { get; init; }
	public float Gold { get; init; }
	public float Experience { get; init; }
	public int Chests { get; init; }
}
