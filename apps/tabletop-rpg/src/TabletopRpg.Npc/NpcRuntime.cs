using TabletopRpg.Core;

namespace TabletopRpg.Npc;

/// <summary>
/// Event-driven, bounded NPC reactions. No AI provider, AgentTurnRunner,
/// background polling, world-state writes or gameplay authority.
/// </summary>
public sealed class NpcRuntime
{
    private const int MaxNpcs = 128;
    private readonly Campaign _campaign;
    private readonly SessionEngine _session;
    private readonly Dictionary<CharacterId, NpcEntry> _npcs = [];

    private sealed class NpcEntry
    {
        public NpcEntry(NpcProfile profile, long start, SceneId? observedScene)
        {
            Profile = profile;
            LastEventSequence = start;
            ObservedScene = observedScene;
            Trust = profile.Disposition switch
            {
                NpcDisposition.Friendly => 7,
                NpcDisposition.Cautious => 4,
                _ => 1
            };
        }

        public NpcProfile Profile { get; }
        public int Trust { get; set; }
        public int Alertness { get; set; }
        public long LastEventSequence { get; set; }
        public SceneId? ObservedScene { get; set; }
        public long Version { get; set; }
        public NpcReaction? Pending { get; set; }
    }

    public NpcRuntime(Campaign campaign, SessionEngine session)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Register(NpcProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_npcs.ContainsKey(profile.CharacterId))
            throw new InvalidOperationException("NPC has already been registered.");
        if (_npcs.Count >= MaxNpcs)
            throw new InvalidOperationException("NPC capacity reached.");
        if (!_campaign.TryGetCharacter(profile.CharacterId, out _))
            throw new KeyNotFoundException("NPC character does not exist in campaign.");
        if (!_campaign.Scenes.Any(s => s.Id == profile.SceneId))
            throw new KeyNotFoundException("NPC scene does not exist in campaign.");
        if (!_campaign.TryGetParticipant(profile.GameMasterId, out var master) ||
            master.Role != SeatRole.GameMaster || !master.Controls(profile.CharacterId))
            throw new InvalidOperationException("NPC must be controlled explicitly by a GameMaster seat.");

        // New NPC does not replay historic actions it never witnessed.
        _npcs.Add(profile.CharacterId, new NpcEntry(
            profile, _campaign.Events.LastOrDefault()?.Sequence ?? 0, _campaign.ActiveSceneId));
    }

    public NpcSnapshot Snapshot(CharacterId npcCharacterId)
    {
        var npc = GetNpc(npcCharacterId);
        return new NpcSnapshot(
            npc.Profile.CharacterId, npc.Profile.SceneId, npc.Profile.Disposition,
            npc.Profile.Role, npc.Profile.Motivation, npc.Trust, npc.Alertness,
            npc.LastEventSequence, npc.Version);
    }

    /// <summary>
    /// Explicitly observe since the last consumed event. Scene transitions are
    /// replayed in sequence so off-scene battles do not affect an absent NPC.
    /// Only private mood changes; Campaign is never modified here.
    /// </summary>
    public NpcSnapshot Observe(CharacterId npcCharacterId)
    {
        var npc = GetNpc(npcCharacterId);
        var view = _campaign.CreateGameMasterView(npc.Profile.GameMasterId);
        var events = view.RecentEvents.OrderBy(e => e.Sequence).ToArray();

        if (npc.LastEventSequence > 0 && events.Length > 0 &&
            events[0].Sequence > npc.LastEventSequence &&
            events.All(e => e.Sequence != npc.LastEventSequence))
            throw new InvalidOperationException("NPC event history was truncated; checkpoint resync required.");

        foreach (var item in events.Where(e => e.Sequence > npc.LastEventSequence))
        {
            if (item.Kind == "scene-entered" &&
                Guid.TryParseExact(item.Text, "N", out var scene) &&
                scene != Guid.Empty)
                npc.ObservedScene = new SceneId(scene);

            if (npc.ObservedScene == npc.Profile.SceneId)
            {
                var delta = item.Kind switch
                {
                    "combat-start" => 3,
                    "combat-attack" => 1,
                    "combat-finished" => -3,
                    _ => 0
                };
                if (delta != 0)
                {
                    npc.Alertness = Math.Clamp(npc.Alertness + delta, 0, 10);
                    npc.Version++;
                }

                if (item.Kind == "quest-resolved" &&
                    item.Text.EndsWith(":Completed", StringComparison.Ordinal))
                {
                    npc.Trust = Math.Min(10, npc.Trust + 1);
                    npc.Version++;
                }
            }
            npc.LastEventSequence = item.Sequence;
        }

        return Snapshot(npcCharacterId);
    }

    /// <summary>
    /// Calculate a dialogue candidate without a call to an LLM or to
    /// SessionEngine. The candidate is not a GameIntent until explicitly approved.
    /// </summary>
    public NpcReaction Propose(NpcInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        var npc = GetNpc(interaction.NpcCharacterId);
        if (!Enum.IsDefined(interaction.Kind))
            throw new ArgumentOutOfRangeException(nameof(interaction));
        if (_campaign.ActiveSceneId != npc.Profile.SceneId)
            throw new InvalidOperationException("NPC is not in the active scene.");
        if (!_campaign.TryGetParticipant(interaction.ActorParticipantId, out var actor) ||
            actor.Role != SeatRole.Player || !actor.Controls(interaction.ActorCharacterId) ||
            interaction.ActorCharacterId == interaction.NpcCharacterId)
            throw new InvalidOperationException("An authorized player character must interact with the NPC.");

        // Old dialogue must be invalidated when a new one is proposed.
        var answer = SelectDialogue(npc, interaction.Kind);
        var view = _campaign.CreateGameMasterView(npc.Profile.GameMasterId);
        var reaction = new NpcReaction(
            Guid.NewGuid(), npc.Profile.CharacterId, npc.Profile.GameMasterId,
            view.Revision, npc.Version, answer.Kind, answer.Text,
            Math.Clamp(npc.Trust + answer.Trust, 0, 10),
            Math.Clamp(npc.Alertness + answer.Alertness, 0, 10));
        npc.Pending = reaction;
        return reaction;
    }

    /// <summary>
    /// Trusted host approves a specific live proposal. Core retains the final
    /// authority through SpeakIntent and SessionEngine; a denied/stale proposal
    /// cannot modify campaign state or NPC mood.
    /// </summary>
    public ActionResolution ApplyApproved(NpcReaction reaction)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        if (!_npcs.TryGetValue(reaction.NpcCharacterId, out var npc) ||
            !ReferenceEquals(npc.Pending, reaction))
            return ActionResolution.Reject("Unrecognized or superseded NPC proposal.");

        npc.Pending = null; // single-use, including rejected proposals

        if (npc.Version != reaction.StateVersion ||
            _campaign.ActiveSceneId != npc.Profile.SceneId ||
            _campaign.CreateGameMasterView(npc.Profile.GameMasterId).Revision != reaction.CampaignRevision)
            return ActionResolution.Reject("Stale NPC proposal.");

        var result = _session.Execute(
            new SpeakIntent(npc.Profile.GameMasterId, npc.Profile.CharacterId, reaction.Text));
        if (result.Accepted)
        {
            npc.Trust = reaction.NextTrust;
            npc.Alertness = reaction.NextAlertness;
            npc.Version++;
        }
        return result;
    }

    private NpcEntry GetNpc(CharacterId id) =>
        _npcs.TryGetValue(id, out var entry) ? entry :
            throw new KeyNotFoundException("Unknown NPC.");

    private static (NpcReactionKind Kind, string Text, int Trust, int Alertness) SelectDialogue(
        NpcEntry npc, NpcInteractionKind kind)
    {
        return kind switch
        {
            NpcInteractionKind.Greet when npc.Alertness >= 7 =>
                (NpcReactionKind.Warn, "Keep your distance. Things are dangerous here.", 0, 0),
            NpcInteractionKind.Greet when npc.Trust >= 6 =>
                (NpcReactionKind.Welcome, "Welcome. What brings you here?", 1, -1),
            NpcInteractionKind.Greet =>
                (NpcReactionKind.Listen, "State your business.", 0, 0),

            NpcInteractionKind.Ask when npc.Trust >= 5 =>
                (NpcReactionKind.Listen, "I am listening. What do you need to know?", 0, 0),
            NpcInteractionKind.Ask =>
                (NpcReactionKind.Refuse, "I do not know you well enough to speak freely.", 0, 0),

            NpcInteractionKind.RequestHelp when npc.Trust >= 6 && npc.Alertness <= 6 =>
                (NpcReactionKind.Help, "I will help if I can.", 1, 0),
            NpcInteractionKind.RequestHelp =>
                (NpcReactionKind.Refuse, "I cannot help you right now.", 0, 0),

            NpcInteractionKind.Threaten when npc.Alertness >= 7 =>
                (NpcReactionKind.Warn, "Back away. Do not make this worse.", -2, 2),
            NpcInteractionKind.Threaten =>
                (NpcReactionKind.Warn, "Threats will not persuade me.", -2, 3),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
