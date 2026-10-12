using TabletopRpg.Core;

namespace TabletopRpg.Director;

/// <summary>
/// Pure, synchronous director: only a trusted host call to Observe reacts to
/// an event snapshot. No timers, background agents, model calls or Campaign writes.
/// </summary>
public sealed class DirectorRuntime
{
    private const int MaxThreads = 32;
    private const int MaxCuesPerObservation = 8;
    private readonly CampaignId _campaignId;
    private readonly ParticipantId _gameMasterId;
    private readonly SortedDictionary<string, DirectorThread> _threads = new(StringComparer.Ordinal);
    private GameMasterRevision? _lastRevision;
    private long _lastEventSequence;
    private string? _lastFocusedThread;
    private int _tension;

    public DirectorRuntime(CampaignId campaignId, ParticipantId gameMasterId)
    {
        if (campaignId.Value == Guid.Empty || gameMasterId.Value == Guid.Empty)
            throw new ArgumentException("Director requires valid campaign and Game Master identities.");
        _campaignId = campaignId;
        _gameMasterId = gameMasterId;
    }

    /// <summary>
    /// Adds a GM-authored unresolved situation, without changing the campaign.
    /// A thread is a possibility, never a fixed mandatory scene.
    /// </summary>
    public void Track(DirectorThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        if (string.IsNullOrWhiteSpace(thread.Key) || thread.Key.Length > 64 ||
            !thread.Key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new ArgumentException("Thread key must be a lowercase ASCII slug.", nameof(thread));
        if (string.IsNullOrWhiteSpace(thread.Premise) || thread.Premise.Length > 500 ||
            !Enum.IsDefined(thread.Kind) ||
            thread.Pressure is < 0 or > 10 || thread.Resolved)
            throw new ArgumentException("Invalid unresolved director thread.", nameof(thread));
        if (_threads.Count >= MaxThreads)
            throw new InvalidOperationException("Director thread capacity reached.");
        if (!_threads.TryAdd(thread.Key, thread with { Premise = thread.Premise.Trim() }))
            throw new InvalidOperationException("Duplicate director thread key.");
    }

    /// <summary>Only the trusted host can mark a private narrative thread resolved.</summary>
    public void Resolve(string key)
    {
        if (!_threads.TryGetValue(key, out var thread))
            throw new KeyNotFoundException("Unknown director thread.");
        if (thread.Resolved) return;
        _threads[key] = thread with { Resolved = true };
    }

    public DirectorFrame Observe(GameMasterView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.CampaignId != _campaignId || view.ParticipantId != _gameMasterId)
            throw new InvalidOperationException("Director snapshot belongs to another campaign or GM.");

        var events = view.RecentEvents.OrderBy(e => e.Sequence).ToArray();
        if (events.Any(e => e.Sequence < 1 || e.Sequence > view.Revision.Clock) ||
            events.Select(e => e.Sequence).Distinct().Count() != events.Length)
            throw new InvalidOperationException("Invalid GM event snapshot.");

        if (_lastRevision is { } previous)
        {
            if (view.Revision.Clock < previous.Clock ||
                view.Revision.FactCount < previous.FactCount ||
                view.Revision.CharacterCount < previous.CharacterCount ||
                view.Revision.ParticipantCount < previous.ParticipantCount)
                throw new InvalidOperationException("Director cannot consume regressed campaign state.");

            // The GM projection contains at most the most recent 24 events.
            // Fail closed rather than silently skip story-affecting actions.
            if (_lastEventSequence > 0 && events.All(e => e.Sequence != _lastEventSequence))
                throw new InvalidOperationException("Director event history has a gap; resynchronize from a checkpoint.");
        }

        var newEvents = events.Where(e => e.Sequence > _lastEventSequence).ToArray();
        var cues = new List<DirectorCue>();
        foreach (var item in newEvents)
        {
            var impact = Impact(item);
            _tension = Math.Clamp(_tension + impact, 0, 10);
            if (TryBeat(item.Kind, out var beat, out var favored))
            {
                var focus = ChooseThread(favored);
                if (focus is not null)
                {
                    var change = impact > 0 ? 1 : impact < 0 ? -1 : 0;
                    var next = focus with { Pressure = Math.Clamp(focus.Pressure + change, 0, 10) };
                    _threads[focus.Key] = next;
                    _lastFocusedThread = focus.Key;
                    if (cues.Count < MaxCuesPerObservation)
                        cues.Add(new DirectorCue(item.Sequence, focus.Key, beat, _tension,
                            Guidance(beat, next)));
                }
            }
            _lastEventSequence = item.Sequence;
        }

        _lastRevision = view.Revision;
        return new DirectorFrame(view.Revision, _lastEventSequence, _tension,
            Array.AsReadOnly(_threads.Values.ToArray()),
            Array.AsReadOnly(cues.ToArray()));
    }

    private DirectorThread? ChooseThread(DirectorThreadKind favored)
    {
        var open = _threads.Values.Where(t => !t.Resolved).ToArray();
        if (open.Length == 0) return null;
        var relevant = open.Where(t => t.Kind == favored).ToArray();
        var candidates = relevant.Length > 0 ? relevant : open;
        return candidates.FirstOrDefault(t => t.Key != _lastFocusedThread) ?? candidates[0];
    }

    private static bool TryBeat(string kind, out DirectorBeatKind beat, out DirectorThreadKind favored)
    {
        switch (kind)
        {
            case "scene-entered":
                beat = DirectorBeatKind.Foreshadow;
                favored = DirectorThreadKind.Clue;
                return true;
            case "combat-start":
            case "combat-attack":
                beat = DirectorBeatKind.Escalation;
                favored = DirectorThreadKind.Threat;
                return true;
            case "skill-check":
                beat = DirectorBeatKind.Discovery;
                favored = DirectorThreadKind.Clue;
                return true;
            case "quest-resolved":
            case "combat-finished":
                beat = DirectorBeatKind.Consequence;
                favored = DirectorThreadKind.Promise;
                return true;
            default:
                beat = default;
                favored = default;
                return false;
        }
    }

    private static int Impact(GameEvent item) => item.Kind switch
    {
        "scene-entered" => 1,
        "combat-start" => 3,
        "combat-attack" => 2,
        "skill-check" => 1,
        "quest-resolved" when item.Text.EndsWith(":Failed", StringComparison.Ordinal) => 3,
        "quest-resolved" when item.Text.EndsWith(":Completed", StringComparison.Ordinal) => -3,
        "quest-resolved" => 0,
        "combat-finished" => -2,
        _ => 0
    };

    private static string Guidance(DirectorBeatKind beat, DirectorThread thread) => beat switch
    {
        DirectorBeatKind.Foreshadow =>
            "Foreshadow without revealing private truth or prescribing the next scene: " + thread.Premise,
        DirectorBeatKind.Escalation =>
            "Raise the stakes while preserving player agency and rule authority: " + thread.Premise,
        DirectorBeatKind.Discovery =>
            "Offer a possible lead, not an automatic fact revelation or predetermined outcome: " + thread.Premise,
        DirectorBeatKind.Consequence =>
            "Acknowledge the outcome and consider a consequence without forcing a script: " + thread.Premise,
        _ => throw new ArgumentOutOfRangeException(nameof(beat))
    };
}
