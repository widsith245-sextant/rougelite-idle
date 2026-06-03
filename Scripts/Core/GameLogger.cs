using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RougeliteIdle.Core;

public partial class GameLogger : Node
{
	public enum LogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
	}

	[Signal]
	public delegate void LogLineAddedEventHandler(string line);

	private const int RingBufferSize = 200;
	private const string SettingsPath = "res://data/tables/meta/debug_settings.json";

	private readonly Queue<string> _ringBuffer = new();
	private bool _combatTraceEnabled = true;

	public bool CombatTraceEnabled => _combatTraceEnabled;

	public void SetCombatTraceEnabled(bool enabled)
	{
		_combatTraceEnabled = enabled;
	}

	public override void _Ready()
	{
		LoadSettings();
		var eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
		if (eventBus == null)
		{
			return;
		}

		eventBus.ItemIdentified += OnItemIdentified;

		if (!_combatTraceEnabled)
		{
			return;
		}

		eventBus.DamageDealt += OnDamageDealt;
		eventBus.WaveStarted += idx => Log("Combat", LogLevel.Info, $"WaveStarted index={idx}");
		eventBus.WaveCleared += idx => Log("Combat", LogLevel.Info, $"WaveCleared index={idx}");
		eventBus.SquadChanged += () => Log("Combat", LogLevel.Info, "SquadChanged");
		eventBus.RosterLevelChanged += id => Log("Meta", LogLevel.Info, $"RosterLevelChanged roster={id}");
	}

	public void Log(string category, LogLevel level, string message)
	{
		var line = $"[{category}] [{level}] {message}";
		_ringBuffer.Enqueue(line);
		while (_ringBuffer.Count > RingBufferSize)
		{
			_ringBuffer.Dequeue();
		}

		EmitSignal(SignalName.LogLineAdded, line);

		switch (level)
		{
			case LogLevel.Warn:
				GD.PushWarning(line);
				break;
			case LogLevel.Error:
				GD.PushError(line);
				break;
			default:
				GD.Print(line);
				break;
		}
	}

	public void LogCombat(string message) => Log("Combat", LogLevel.Info, message);

	public void LogCombatWarn(string message) => Log("Combat", LogLevel.Warn, message);

	public void LogLoot(string message) => Log("Loot", LogLevel.Info, message);

	public Godot.Collections.Array GetRecentLines(int maxLines = 50)
	{
		var result = new Godot.Collections.Array();
		var list = _ringBuffer.ToArray();
		var start = System.Math.Max(0, list.Length - maxLines);
		for (var i = start; i < list.Length; i++)
		{
			result.Add(list[i]);
		}

		return result;
	}

	private void OnItemIdentified(Godot.Collections.Dictionary itemData)
	{
		var quality = itemData.GetValueOrDefault("quality", "common").AsString();
		var displayName = itemData.GetValueOrDefault("display_name", "?").AsString();
		var itemLevel = itemData.GetValueOrDefault("item_level", 0).AsInt32();
		var slot = itemData.GetValueOrDefault("slot", "").AsString();
		LogLoot($"鉴定获得 [{quality}] {displayName} iLvl {itemLevel} · 部位 {slot}");
	}

	private void OnDamageDealt(string sourceId, string targetId, float amount, bool isCrit)
	{
		if (!_combatTraceEnabled)
		{
			return;
		}

		LogCombat($"{sourceId}->{targetId} dmg={amount:F1} crit={isCrit}");
	}

	private void LoadSettings()
	{
		if (!FileAccess.FileExists(SettingsPath))
		{
			return;
		}

		using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return;
		}

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var settings = JsonSerializer.Deserialize<DebugSettings>(file.GetAsText(), options);
		if (settings != null)
		{
			_combatTraceEnabled = settings.CombatTraceEnabled;
		}
	}

	private sealed class DebugSettings
	{
		public bool CombatTraceEnabled { get; set; } = true;
	}
}
