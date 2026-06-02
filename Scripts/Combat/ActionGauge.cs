namespace RougeliteIdle.Combat;

/// <summary>
/// Turn-based action gauge tick logic (non-real-time).
/// </summary>
public static class ActionGauge
{
	public const float GaugeMax = 100f;

	public static float Tick(CombatUnitData unit, float delta)
	{
		if (unit.Speed <= 0f)
		{
			return unit.ActionGauge;
		}

		unit.ActionGauge += unit.Speed * delta;
		if (unit.ActionGauge > GaugeMax)
		{
			unit.ActionGauge = GaugeMax;
		}

		return unit.ActionGauge;
	}

	public static bool IsReady(CombatUnitData unit) => unit.ActionGauge >= GaugeMax;

	public static void Reset(CombatUnitData unit) => unit.ActionGauge = 0f;
}
