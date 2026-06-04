using System.Collections.Generic;

namespace RougeliteIdle.Combat.Effects;

public delegate void EffectHandler(
	CombatUnitData owner,
	CombatHitContext context,
	ActiveCombatEffect instance,
	EffectTriggerKind trigger,
	EffectHandlerServices services);

public sealed class EffectHandlerServices
{
	public BattlefieldController? Battlefield { get; init; }
	public IReadOnlyList<CombatUnitData>? Allies { get; init; }
	public IReadOnlyList<CombatUnitData>? Enemies { get; init; }
	public CombatActionExecutor? Executor { get; init; }
	public float MetaPercent { get; init; }
}
