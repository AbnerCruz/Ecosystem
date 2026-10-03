using System.Collections.ObjectModel;

namespace TabletopRpg.Core;

public enum ControllerKind
{
    Human,
    Agent
}

public enum SeatRole
{
    Player,
    GameMaster
}

public sealed class Character
{
    private readonly Dictionary<string, int> _attributes;
    private readonly IReadOnlyDictionary<string, int> _attributesView;

    public Character(CharacterId id, string name, IReadOnlyDictionary<string, int>? attributes = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Character id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Character name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        _attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (attributes is not null)
        {
            foreach (var pair in attributes)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Attribute names cannot be empty.", nameof(attributes));

                _attributes[pair.Key.Trim()] = pair.Value;
            }
        }

        _attributesView = new ReadOnlyDictionary<string, int>(_attributes);
    }

    public CharacterId Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, int> Attributes => _attributesView;

    public int AttributeModifier(string attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute)) return 0;
        return _attributes.TryGetValue(attribute.Trim(), out var value) ? value : 0;
    }
}

public sealed class Participant
{
    private readonly HashSet<CharacterId> _controlledCharacters;

    public Participant(
        ParticipantId id,
        string name,
        ControllerKind controller,
        SeatRole role,
        IEnumerable<CharacterId>? controlledCharacters = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("Participant id cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Participant name is required.", nameof(name));

        Id = id;
        Name = name.Trim();
        Controller = controller;
        Role = role;
        _controlledCharacters = controlledCharacters is null
            ? []
            : new HashSet<CharacterId>(controlledCharacters);
    }

    public ParticipantId Id { get; }
    public string Name { get; }
    public ControllerKind Controller { get; }
    public SeatRole Role { get; }
    public IReadOnlyList<CharacterId> ControlledCharacterIds => _controlledCharacters.ToArray();

    public bool Controls(CharacterId characterId) => _controlledCharacters.Contains(characterId);
}
