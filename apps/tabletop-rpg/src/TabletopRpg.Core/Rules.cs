namespace TabletopRpg.Core;

public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}

public sealed class SystemRandomSource : IRandomSource
{
    public int Next(int minInclusive, int maxExclusive) =>
        Random.Shared.Next(minInclusive, maxExclusive);
}

public interface IDiceRoller
{
    int Roll(int sides);
}

public sealed class DiceRoller : IDiceRoller
{
    private readonly IRandomSource _random;

    public DiceRoller(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public int Roll(int sides)
    {
        if (sides < 2) throw new ArgumentOutOfRangeException(nameof(sides), "A die needs at least two sides.");
        return _random.Next(1, checked(sides + 1));
    }
}

public sealed record SkillCheckResult(
    int NaturalRoll,
    int Modifier,
    int Total,
    int Difficulty,
    bool Success);

public interface IRuleSystem
{
    SkillCheckResult ResolveSkillCheck(Character character, SkillCheckIntent intent, IDiceRoller dice);
}

public sealed class BasicD20RuleSystem : IRuleSystem
{
    public SkillCheckResult ResolveSkillCheck(Character character, SkillCheckIntent intent, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(dice);

        var natural = dice.Roll(20);
        var modifier = character.AttributeModifier(intent.Attribute);
        var total = checked(natural + modifier);

        return new SkillCheckResult(
            natural,
            modifier,
            total,
            intent.Difficulty,
            total >= intent.Difficulty);
    }
}
