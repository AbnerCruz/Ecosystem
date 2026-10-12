using TabletopRpg.Core;

namespace TabletopRpg.Director;

public enum DirectorThreadKind
{
    Threat,
    Clue,
    Promise
}

public enum DirectorBeatKind
{
    Foreshadow,
    Escalation,
    Discovery,
    Consequence
}

/// <summary>
/// Private story pressure, not a campaign fact, a quest or a script step.
/// Its premise may contain secrets and must never be projected into PlayerView.
/// </summary>
public sealed record DirectorThread(
    string Key,
    DirectorThreadKind Kind,
    string Premise,
    int Pressure = 0,
    bool Resolved = false);

/// <summary>
/// Private suggestion for the Game Master, never a published game event or
/// an automatic change to campaign state.
/// </summary>
public sealed record DirectorCue(
    long SourceEventSequence,
    string ThreadKey,
    DirectorBeatKind Beat,
    int Tension,
    string Guidance);

public sealed record DirectorFrame(
    GameMasterRevision Revision,
    long LastProcessedEventSequence,
    int Tension,
    IReadOnlyList<DirectorThread> Threads,
    IReadOnlyList<DirectorCue> Cues);
