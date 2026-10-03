namespace TabletopRpg.Core;

public readonly record struct CampaignId(Guid Value)
{
    public static CampaignId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct ParticipantId(Guid Value)
{
    public static ParticipantId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct CharacterId(Guid Value)
{
    public static CharacterId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct FactId(Guid Value)
{
    public static FactId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
