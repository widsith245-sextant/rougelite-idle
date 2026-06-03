using System.Text.Json;
using Godot;

namespace RougeliteIdle.Core;

public partial class GameSettingsManager : Node
{
	private const string SettingsPath = "user://settings.json";
	private const float SaveDebounceSeconds = 0.35f;

	private float _saveDebounce;
	private GameSettingsData _settings = new();

	public bool RememberWindowPosition => _settings.RememberWindowPosition;
	public bool IdentifyRevealEnabled => _settings.IdentifyRevealEnabled;
	public float IdentifyIntervalMultiplier => _settings.IdentifyIntervalMultiplier;
	public bool CombatTraceEnabled => _settings.CombatTraceEnabled;

	public override void _Ready()
	{
		LoadSettings();
		ApplyCombatTraceSetting();
	}

	public override void _Process(double delta)
	{
		if (_saveDebounce <= 0f)
		{
			return;
		}

		_saveDebounce -= (float)delta;
		if (_saveDebounce <= 0f)
		{
			WriteSettingsFile();
		}
	}

	public void SetRememberWindowPosition(bool enabled)
	{
		_settings.RememberWindowPosition = enabled;
		RequestSave();
	}

	public void SetIdentifyRevealEnabled(bool enabled)
	{
		_settings.IdentifyRevealEnabled = enabled;
		RequestSave();
	}

	public void SetIdentifyIntervalMultiplier(float multiplier)
	{
		_settings.IdentifyIntervalMultiplier = Mathf.Clamp(multiplier, 0.5f, 1.5f);
		RequestSave();
	}

	public void SetCombatTraceEnabled(bool enabled)
	{
		_settings.CombatTraceEnabled = enabled;
		ApplyCombatTraceSetting();
		RequestSave();
	}

	public void SaveWindowPosition(int x, int y)
	{
		if (!_settings.RememberWindowPosition)
		{
			return;
		}

		_settings.WindowX = x;
		_settings.WindowY = y;
		_settings.HasSavedWindowPosition = true;
		RequestSave();
	}

	public bool TryRestoreWindowPosition()
	{
		if (!_settings.RememberWindowPosition || !_settings.HasSavedWindowPosition)
		{
			return false;
		}

		var mainWinId = (int)DisplayServer.MainWindowId;
		var screen = DisplayServer.WindowGetCurrentScreen(mainWinId);
		var usable = DisplayServer.ScreenGetUsableRect(screen);
		var size = DisplayServer.WindowGetSize(mainWinId);
		var pos = new Vector2I(_settings.WindowX, _settings.WindowY);
		if (pos.X + size.X < usable.Position.X + 16
			|| pos.Y + size.Y < usable.Position.Y + 16
			|| pos.X > usable.Position.X + usable.Size.X - 16
			|| pos.Y > usable.Position.Y + usable.Size.Y - 16)
		{
			return false;
		}

		DisplayServer.WindowSetPosition(pos, mainWinId);
		return true;
	}

	public void ClearSavedWindowPosition()
	{
		_settings.HasSavedWindowPosition = false;
		RequestSave();
	}

	public Godot.Collections.Dictionary GetSnapshot() => new()
	{
		{ "remember_window_position", _settings.RememberWindowPosition },
		{ "identify_reveal_enabled", _settings.IdentifyRevealEnabled },
		{ "identify_interval_multiplier", _settings.IdentifyIntervalMultiplier },
		{ "combat_trace_enabled", _settings.CombatTraceEnabled },
	};

	private void LoadSettings()
	{
		_settings = new GameSettingsData();
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
		var loaded = JsonSerializer.Deserialize<GameSettingsData>(file.GetAsText(), options);
		if (loaded != null)
		{
			_settings = loaded;
		}
	}

	private void RequestSave() => _saveDebounce = SaveDebounceSeconds;

	private void WriteSettingsFile()
	{
		var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
		using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
		file?.StoreString(json);
	}

	private void ApplyCombatTraceSetting()
	{
		GetNodeOrNull<GameLogger>("/root/GameLogger")?.SetCombatTraceEnabled(_settings.CombatTraceEnabled);
	}

	private sealed class GameSettingsData
	{
		public bool RememberWindowPosition { get; set; } = true;
		public bool HasSavedWindowPosition { get; set; }
		public int WindowX { get; set; }
		public int WindowY { get; set; }
		public bool IdentifyRevealEnabled { get; set; } = true;
		public float IdentifyIntervalMultiplier { get; set; } = 1f;
		public bool CombatTraceEnabled { get; set; } = true;
	}
}
