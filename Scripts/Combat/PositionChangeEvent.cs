namespace RougeliteIdle.Combat;

public readonly struct PositionChangeEvent
{
	public string EntityId { get; init; }
	public float OldX { get; init; }
	public float NewX { get; init; }

	public PositionChangeEvent(string entityId, float oldX, float newX)
	{
		EntityId = entityId;
		OldX = oldX;
		NewX = newX;
	}

	public float Delta => NewX - OldX;
}
