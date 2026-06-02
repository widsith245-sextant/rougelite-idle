namespace RougeliteIdle.Combat;

public readonly struct PendingDamage
{
	public CombatUnitData Source { get; init; }
	public CombatUnitData Target { get; init; }
	public float Amount { get; init; }

	public PendingDamage(CombatUnitData source, CombatUnitData target, float amount)
	{
		Source = source;
		Target = target;
		Amount = amount;
	}
}
