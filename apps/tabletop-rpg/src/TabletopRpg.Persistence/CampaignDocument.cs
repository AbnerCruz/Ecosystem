using System.Text.Json;
using TabletopRpg.Core;

namespace TabletopRpg.Persistence;

internal sealed record CampaignEnvelope(
    string? Format,
    int SchemaVersion,
    DateTimeOffset SavedAtUtc,
    string? PayloadSha256,
    JsonElement Payload);

internal sealed record CharacterDto(
    Guid Id,
    string? Name,
    Dictionary<string, int>? Attributes);

internal sealed record ParticipantDto(
    Guid Id,
    string? Name,
    ControllerKind Controller,
    SeatRole Role,
    Guid[]? ControlledCharacterIds);

internal sealed record WorldFactDto(
    Guid Id,
    string? Statement);

internal sealed record KnowledgeClaimDto(
    Guid? FactId,
    string? Statement,
    string? Source,
    double Confidence,
    long LearnedAt);

internal sealed record MemoryEntryDto(
    string? Summary,
    long RememberedAt);

internal sealed record PerspectiveDto(
    Guid CharacterId,
    KnowledgeClaimDto[]? Knowledge,
    MemoryEntryDto[]? Memories);

internal sealed record GameEventDto(
    long Sequence,
    string? Kind,
    Guid? ParticipantId,
    Guid? CharacterId,
    string? Text);

internal sealed record SessionDto(
    Guid Id,
    string? Name,
    long StartedAt,
    long? EndedAt);

internal sealed record CampaignDto(
    Guid Id,
    string? Name,
    long Clock,
    CharacterDto[]? Characters,
    ParticipantDto[]? Participants,
    WorldFactDto[]? WorldFacts,
    PerspectiveDto[]? Perspectives,
    GameEventDto[]? Events,
    SessionDto[]? Sessions,
    Guid? ActiveSessionId);
