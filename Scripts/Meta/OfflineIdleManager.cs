using System;
using Godot;
using RougeliteIdle.Save;

namespace RougeliteIdle.Meta;

public partial class OfflineIdleManager : Node
{
	public void StampSessionTime()
	{
		var save = GetNode<SaveManager>("/root/SaveManager");
		save.SaveGame();
	}

	public OfflineIdleResult CalculateOfflineRewards(long lastSessionUnix, int stageLevel)
	{
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var elapsedSeconds = Math.Max(0, now - lastSessionUnix);

		return new OfflineIdleResult
		{
			ElapsedSeconds = elapsedSeconds,
			Gold = elapsedSeconds * 0.5f * stageLevel,
			Experience = elapsedSeconds * 0.2f * stageLevel,
			Chests = (int)(elapsedSeconds / 300),
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
