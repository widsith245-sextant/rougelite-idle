namespace RougeliteIdle.Core.Enums;

/// <summary>
/// Positional skill movement tags for the combat matrix.
/// </summary>
public enum MoveTagKind
{
	Charge,
	Retreat,
	ForceSwap,
}

public readonly struct MoveTag
{
	public MoveTagKind Kind { get; init; }
	public int Distance { get; init; }

	public MoveTag(MoveTagKind kind, int distance = 1)
	{
		Kind = kind;
		Distance = distance;
	}
}
