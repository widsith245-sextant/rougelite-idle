namespace RougeliteIdle.Combat.Effects;

public interface ICombatEffect
{
	string EffectId { get; }
	void Apply(CombatUnitData source, CombatUnitData? target, CombatActionExecutor executor);
}
