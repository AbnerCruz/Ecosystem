using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TabletopRpg.Core;

namespace TabletopRpg.Persistence;

public sealed class CampaignFormatException : Exception
{
    public CampaignFormatException(string message) : base(message) { }
    public CampaignFormatException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class CampaignCodec
{
    public const string FormatId = "tabletop-rpg-campaign";
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions CanonicalOptions = CreateOptions(writeIndented: false);
    private static readonly JsonSerializerOptions EnvelopeOptions = CreateOptions(writeIndented: true);

    private readonly TimeProvider _timeProvider;

    public CampaignCodec(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public byte[] Encode(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var payload = ToDto(campaign.CaptureState());
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, CanonicalOptions);
        var checksum = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        using var document = JsonDocument.Parse(payloadBytes);
        var envelope = new CampaignEnvelope(
            FormatId,
            CurrentSchemaVersion,
            _timeProvider.GetUtcNow(),
            checksum,
            document.RootElement.Clone());

        return JsonSerializer.SerializeToUtf8Bytes(envelope, EnvelopeOptions);
    }

    public Campaign Decode(ReadOnlySpan<byte> bytes)
    {
        CampaignEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CampaignEnvelope>(bytes, EnvelopeOptions)
                ?? throw new CampaignFormatException("Campaign document is empty.");
        }
        catch (CampaignFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new CampaignFormatException("Campaign document is not valid JSON.", exception);
        }

        if (!string.Equals(envelope.Format, FormatId, StringComparison.Ordinal))
            throw new CampaignFormatException($"Unsupported campaign format '{envelope.Format}'.");

        if (envelope.SchemaVersion != CurrentSchemaVersion)
            throw new CampaignFormatException(
                $"Unsupported campaign schema version {envelope.SchemaVersion}; expected {CurrentSchemaVersion}.");

        if (string.IsNullOrWhiteSpace(envelope.PayloadSha256))
            throw new CampaignFormatException("Campaign checksum is missing.");
        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new CampaignFormatException("Campaign payload is missing.");

        var canonicalPayload = JsonSerializer.SerializeToUtf8Bytes(envelope.Payload, CanonicalOptions);
        var actualChecksum = Convert.ToHexString(SHA256.HashData(canonicalPayload)).ToLowerInvariant();

        if (!string.Equals(actualChecksum, envelope.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new CampaignFormatException("Campaign checksum does not match the payload.");

        CampaignDto dto;
        try
        {
            dto = envelope.Payload.Deserialize<CampaignDto>(CanonicalOptions)
                ?? throw new CampaignFormatException("Campaign payload is empty.");
        }
        catch (CampaignFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new CampaignFormatException("Campaign payload cannot be decoded.", exception);
        }

        try
        {
            return Campaign.Restore(ToState(dto));
        }
        catch (CampaignFormatException)
        {
            throw;
        }
        catch (CampaignStateException exception)
        {
            throw new CampaignFormatException($"Campaign state is invalid: {exception.Message}", exception);
        }
    }

    public async Task ExportAsync(
        Campaign campaign,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));

        var bytes = Encode(campaign);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Campaign> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Source stream must be readable.", nameof(source));

        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Decode(buffer.ToArray());
    }

    private static CampaignDto ToDto(CampaignState state) =>
        new(
            state.Id.Value,
            state.Name,
            state.Clock,
            state.Characters.Select(character => new CharacterDto(
                character.Id.Value,
                character.Name,
                character.Attributes.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase))).ToArray(),
            state.Participants.Select(participant => new ParticipantDto(
                participant.Id.Value,
                participant.Name,
                participant.Controller,
                participant.Role,
                participant.ControlledCharacterIds.Select(id => id.Value).ToArray())).ToArray(),
            state.WorldFacts.Select(fact => new WorldFactDto(
                fact.Id.Value,
                fact.Statement)).ToArray(),
            state.Perspectives.Select(perspective => new PerspectiveDto(
                perspective.CharacterId.Value,
                perspective.Knowledge.Select(claim => new KnowledgeClaimDto(
                    claim.FactId?.Value,
                    claim.Statement,
                    claim.Source,
                    claim.Confidence,
                    claim.LearnedAt)).ToArray(),
                perspective.Memories.Select(memory => new MemoryEntryDto(
                    memory.Summary,
                    memory.RememberedAt)).ToArray())).ToArray(),
            state.Events.Select(gameEvent => new GameEventDto(
                gameEvent.Sequence,
                gameEvent.Kind,
                gameEvent.ParticipantId?.Value,
                gameEvent.CharacterId?.Value,
                gameEvent.Text)).ToArray(),
            state.Sessions.Select(session => new SessionDto(
                session.Id.Value,
                session.Name,
                session.StartedAt,
                session.EndedAt)).ToArray(),
            state.ActiveSessionId?.Value);

    private static CampaignState ToState(CampaignDto dto)
    {
        var characters = RequiredArray(dto.Characters, "characters")
            .Select(character => new CharacterState(
                new CharacterId(character.Id),
                RequiredText(character.Name, "character.name"),
                ReadAttributes(character.Attributes)))
            .ToArray();

        var participants = RequiredArray(dto.Participants, "participants")
            .Select(participant => new ParticipantState(
                new ParticipantId(participant.Id),
                RequiredText(participant.Name, "participant.name"),
                participant.Controller,
                participant.Role,
                RequiredArray(participant.ControlledCharacterIds, "participant.controlledCharacterIds")
                    .Select(id => new CharacterId(id))
                    .ToArray()))
            .ToArray();

        var worldFacts = RequiredArray(dto.WorldFacts, "worldFacts")
            .Select(fact => new WorldFact(
                new FactId(fact.Id),
                RequiredText(fact.Statement, "worldFact.statement")))
            .ToArray();

        var perspectives = RequiredArray(dto.Perspectives, "perspectives")
            .Select(perspective => new PerspectiveState(
                new CharacterId(perspective.CharacterId),
                RequiredArray(perspective.Knowledge, "perspective.knowledge")
                    .Select(claim => new KnowledgeClaim(
                        claim.FactId is { } factId ? new FactId(factId) : null,
                        RequiredText(claim.Statement, "knowledge.statement"),
                        RequiredText(claim.Source, "knowledge.source"),
                        claim.Confidence,
                        claim.LearnedAt))
                    .ToArray(),
                RequiredArray(perspective.Memories, "perspective.memories")
                    .Select(memory => new MemoryEntry(
                        RequiredText(memory.Summary, "memory.summary"),
                        memory.RememberedAt))
                    .ToArray()))
            .ToArray();

        var events = RequiredArray(dto.Events, "events")
            .Select(gameEvent => new GameEvent(
                gameEvent.Sequence,
                RequiredText(gameEvent.Kind, "event.kind"),
                gameEvent.ParticipantId is { } participantId ? new ParticipantId(participantId) : null,
                gameEvent.CharacterId is { } characterId ? new CharacterId(characterId) : null,
                RequiredText(gameEvent.Text, "event.text")))
            .ToArray();

        var sessions = RequiredArray(dto.Sessions, "sessions")
            .Select(session => new CampaignSession(
                new SessionId(session.Id),
                RequiredText(session.Name, "session.name"),
                session.StartedAt,
                session.EndedAt))
            .ToArray();

        return new CampaignState(
            new CampaignId(dto.Id),
            RequiredText(dto.Name, "name"),
            dto.Clock,
            characters,
            participants,
            worldFacts,
            perspectives,
            events,
            sessions,
            dto.ActiveSessionId is { } activeSessionId ? new SessionId(activeSessionId) : null);
    }

    private static IReadOnlyDictionary<string, int> ReadAttributes(
        Dictionary<string, int>? attributes)
    {
        if (attributes is null)
            throw new CampaignFormatException("Campaign field 'character.attributes' is required.");

        try
        {
            return CampaignState.ReadOnlyAttributes(attributes);
        }
        catch (ArgumentException exception)
        {
            throw new CampaignFormatException(
                "Campaign character attributes contain duplicate or invalid names.",
                exception);
        }
    }

    private static T[] RequiredArray<T>(T[]? value, string field) =>
        value ?? throw new CampaignFormatException($"Campaign field '{field}' is required.");

    private static string RequiredText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CampaignFormatException($"Campaign field '{field}' is required.");

        return value;
    }

    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = writeIndented
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
