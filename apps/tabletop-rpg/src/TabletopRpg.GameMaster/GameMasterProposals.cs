using System.Text.Json;
using TabletopRpg.Core;

namespace TabletopRpg.GameMaster;

/// <summary>Non-authoritative draft proposed by an untrusted model.</summary>
public abstract record GameMasterProposal(ParticipantId GameMasterId, long ExpectedRevision);
public sealed record NarrationDraft(
    ParticipantId GameMasterId, long ExpectedRevision, string Text)
    : GameMasterProposal(GameMasterId, ExpectedRevision);
public sealed record RevealFactDraft(
    ParticipantId GameMasterId, long ExpectedRevision, CharacterId TargetCharacterId, FactId FactId)
    : GameMasterProposal(GameMasterId, ExpectedRevision);

public static class GameMasterProposalDecoder
{
    public static GameMasterProposal? TryDecode(
        string? text, ParticipantId gmId, long revision)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096 ||
            gmId.Value == Guid.Empty || revision < 0)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                MaxDepth = 4,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length is < 1 or > 3 ||
                properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                return null;

            if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
                return null;

            switch (kind.GetString())
            {
                case "narrate":
                    if (properties.Length != 2 ||
                        properties.Any(p => p.Name is not ("kind" or "text")) ||
                        !root.TryGetProperty("text", out var narrative) ||
                        narrative.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(narrative.GetString()) ||
                        narrative.GetString()!.Length > 1800) return null;
                    return new NarrationDraft(gmId, revision, narrative.GetString()!.Trim());

                case "reveal_fact":
                    if (properties.Length != 3 ||
                        properties.Any(p => p.Name is not ("kind" or "factId" or "targetCharacterId")) ||
                        !root.TryGetProperty("factId", out var fact) ||
                        !root.TryGetProperty("targetCharacterId", out var character) ||
                        !ParseId(fact, out var factGuid) || !ParseId(character, out var characterGuid))
                        return null;
                    return new RevealFactDraft(gmId, revision,
                        new CharacterId(characterGuid), new FactId(factGuid));

                case "pass":
                    return properties.Length == 1 ? null : null;
                default:
                    return null;
            }
        }
        catch (JsonException) { return null; }
    }

    private static bool ParseId(JsonElement value, out Guid guid)
    {
        guid = Guid.Empty;
        return value.ValueKind == JsonValueKind.String &&
            Guid.TryParseExact(value.GetString(), "N", out guid) &&
            guid != Guid.Empty;
    }
}

/// <summary>
/// This separate host-facing entrypoint must be invoked by trusted application
/// code after reviewing and approving the draft. A gateway response alone never
/// mutates the campaign.
/// </summary>
public sealed class ApprovedGameMasterTools
{
    private readonly Campaign _campaign;
    public ApprovedGameMasterTools(Campaign campaign) =>
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));

    public void ApplyApproved(GameMasterProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        switch (proposal)
        {
            case NarrationDraft narration:
                _campaign.PublishGameMasterNarration(
                    narration.GameMasterId, narration.ExpectedRevision, narration.Text);
                break;
            case RevealFactDraft reveal:
                _campaign.RevealGameMasterFact(
                    reveal.GameMasterId, reveal.ExpectedRevision,
                    reveal.TargetCharacterId, reveal.FactId);
                break;
            default:
                throw new InvalidOperationException("Unsupported GameMaster tool.");
        }
    }
}
