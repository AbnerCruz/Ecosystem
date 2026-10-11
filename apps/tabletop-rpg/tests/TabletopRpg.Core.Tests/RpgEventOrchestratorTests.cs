using TabletopRpg.Core;
using TabletopRpg.GameMaster;
using TabletopRpg.ModelGateway;
using TabletopRpg.Orchestration;

namespace TabletopRpg.Core.Tests;

public sealed class RpgEventOrchestratorTests
{
    [Fact]
    public async Task An_explicit_event_runs_once_and_duplicate_does_not_call_agent()
    {
        var t = Table.Create();
        var source = new SpeakingAgentSource();
        var coordinator = t.Coordinator();
        coordinator.RegisterPlayer(t.Agent.Id, source);
        Assert.Empty(coordinator.Receipts);
        Assert.Equal(0, source.Calls);

        var result = await coordinator.DispatchAsync("event-1", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);
        var duplicate = await coordinator.DispatchAsync("event-1", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);

        Assert.Equal(OrchestratedTurnStatus.Applied, result.Status);
        Assert.Equal(OrchestratedTurnStatus.Duplicate, duplicate.Status);
        Assert.Equal(1, source.Calls);
        Assert.Single(t.Campaign.Events);
        Assert.All(coordinator.Receipts, x =>
        {
            Assert.Null(x.Action);
            Assert.Null(x.Proposal);
        });
    }

    [Fact]
    public async Task Busy_seat_refuses_concurrent_wake_without_spending()
    {
        var t = Table.Create();
        var pending = new TaskCompletionSource<GameIntent?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new WaitingAgentSource(pending.Task);
        var coordinator = t.Coordinator();
        coordinator.RegisterPlayer(t.Agent.Id, source);

        var firstTask = coordinator.DispatchAsync("event-a", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);
        Assert.Equal(1, source.Calls);
        var second = await coordinator.DispatchAsync("event-b", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);

        Assert.Equal(OrchestratedTurnStatus.Busy, second.Status);
        Assert.Equal(1, source.Calls);
        pending.SetResult(new SpeakIntent(t.Agent.Id, t.Companion.Id, "Ready."));
        Assert.Equal(OrchestratedTurnStatus.Applied, (await firstTask).Status);
        Assert.Single(t.Campaign.Events);
    }

    [Fact]
    public async Task Only_a_confirmed_no_charge_failure_is_retried_once()
    {
        var t = Table.Create();
        var agent = new NoChargeThenSpeakAgent(t.Agent.Id, t.Companion.Id);
        var coordinator = t.Coordinator();
        coordinator.RegisterPlayer(t.Agent.Id, agent);

        var response = await coordinator.DispatchAsync("retry-safe", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);

        Assert.Equal(OrchestratedTurnStatus.Applied, response.Status);
        Assert.Equal(2, response.Attempts);
        Assert.Equal(2, agent.Calls);
        Assert.Single(t.Campaign.Events);
    }

    [Fact]
    public async Task Unknown_remote_failure_is_never_retried_or_replayed()
    {
        var t = Table.Create();
        var source = new FailingAgent();
        var coordinator = t.Coordinator();
        coordinator.RegisterPlayer(t.Agent.Id, source);

        var first = await coordinator.DispatchAsync("uncertain-remote", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);
        var second = await coordinator.DispatchAsync("uncertain-remote", t.Agent.Id,
            new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);

        Assert.Equal(OrchestratedTurnStatus.Failed, first.Status);
        Assert.Equal(1, first.Attempts);
        Assert.Equal("provider-or-agent-failure", first.Reason);
        Assert.Equal(OrchestratedTurnStatus.Duplicate, second.Status);
        Assert.Equal(1, source.Calls);
        Assert.Empty(t.Campaign.Events);
    }

    [Fact]
    public async Task Gm_proposal_is_not_approved_automatically()
    {
        var t = Table.Create();
        var gateway = new ReplyGateway(new ModelTurnResponse(
            "{\"kind\":\"narrate\",\"text\":\"A shadow moves through the archway.\"}"));
        var coordinator = t.Coordinator();
        coordinator.RegisterGameMaster(t.Master.Id, new GameMasterAgent(t.Campaign, gateway));

        var result = await coordinator.DispatchAsync("scene-1", t.Master.Id,
            new AgentWakeSignal("scene-changed"), TestContext.Current.CancellationToken);

        Assert.Equal(OrchestratedTurnStatus.Proposal, result.Status);
        var draft = Assert.IsType<NarrationDraft>(result.Proposal);
        Assert.Contains("shadow", draft.Text);
        Assert.Empty(t.Campaign.Events);
        Assert.Null(Assert.Single(coordinator.Receipts).Proposal);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Missing_financial_limits_do_not_block_repeated_distinct_events()
    {
        var t = Table.Create();
        var source = new SpeakingAgentSource();
        var coordinator = t.Coordinator();
        coordinator.RegisterPlayer(t.Agent.Id, source);

        for (var i = 0; i < 25; i++)
        {
            var result = await coordinator.DispatchAsync($"speech-{i}", t.Agent.Id,
                new AgentWakeSignal("turn-started"), TestContext.Current.CancellationToken);
            Assert.Equal(OrchestratedTurnStatus.Applied, result.Status);
        }
        Assert.Equal(25, source.Calls);
        Assert.Equal(25, t.Campaign.Events.Count);
    }

    [Fact]
    public void Player_and_master_sources_require_their_own_agent_seats()
    {
        var t = Table.Create();
        var coordinator = t.Coordinator();
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.RegisterPlayer(t.Human.Id, new SpeakingAgentSource()));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.RegisterPlayer(t.Master.Id, new SpeakingAgentSource()));
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.RegisterGameMaster(t.Agent.Id, new GameMasterAgent(
                t.Campaign, new ReplyGateway(new ModelTurnResponse("{}")))));
    }

    [Fact]
    public async Task Metering_captures_only_authoritative_usage_without_a_quota()
    {
        var response = new ModelTurnResponse("{\"kind\":\"pass\"}",
            new ModelTurnUsage(inputTokens: 320, outputTokens: 24, billedUsd: 0.0012m));
        var inner = new ReplyGateway(response);
        var meter = new ObservableModelGateway(inner, "provider-a", "model-b");
        var request = new ModelTurnRequest("turn-1", "game-intent.v1",
            [new(ModelMessageRole.User, "Information explicitly projected by caller.")]);

        await meter.GenerateAsync(request, TestContext.Current.CancellationToken);
        await meter.GenerateAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Calls);
        Assert.Equal(2, meter.Receipts.Count);
        Assert.All(meter.Receipts, receipt =>
        {
            Assert.Equal("provider-a", receipt.ProviderId);
            Assert.Equal("model-b", receipt.ModelId);
            Assert.Equal(320, receipt.InputTokens);
            Assert.Equal(24, receipt.OutputTokens);
            Assert.Equal(0.0012m, receipt.BilledUsd);
            Assert.True(receipt.Completed);
            Assert.False(receipt.IsEstimate);
        });
    }

    [Fact]
    public async Task Missing_usage_or_failed_provider_must_not_be_recorded_as_free()
    {
        var meter = new ObservableModelGateway(
            new ReplyGateway(new ModelTurnResponse("{\"kind\":\"pass\"}")),
            "other-provider", "local-model");
        var request = new ModelTurnRequest("turn-no-usage", "game-intent.v1",
            [new(ModelMessageRole.User, "context")]);

        await meter.GenerateAsync(request, TestContext.Current.CancellationToken);
        var unknown = Assert.Single(meter.Receipts);
        Assert.Null(unknown.BilledUsd);
        Assert.Null(unknown.InputTokens);

        var failing = new ObservableModelGateway(new FailingGateway(), "unknown", "model");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failing.GenerateAsync(request, TestContext.Current.CancellationToken));

        var failed = Assert.Single(failing.Receipts);
        Assert.False(failed.Completed);
        Assert.Null(failed.BilledUsd);
        Assert.Null(failed.InputTokens);
    }

    [Fact]
    public void Invalid_usage_is_rejected_and_receipts_are_read_only()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModelTurnUsage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ModelTurnUsage(billedUsd: -0.01m));

        var meter = new ObservableModelGateway(new FailingGateway(), "p", "m");
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ModelUsageReceipt>)meter.Receipts).Add(
                new ModelUsageReceipt("t", "p", "m", null, null, null, false, false)));
    }

    private sealed class ReplyGateway : IModelGateway
    {
        private readonly ModelTurnResponse _response;
        public ReplyGateway(ModelTurnResponse response) => _response = response;
        public int Calls { get; private set; }
        public Task<ModelTurnResponse> GenerateAsync(ModelTurnRequest turn,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_response);
        }
    }

    private sealed class FailingGateway : IModelGateway
    {
        public Task<ModelTurnResponse> GenerateAsync(ModelTurnRequest turn,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ModelTurnResponse>(new InvalidOperationException("Do not log secret payload."));
    }

    private sealed class SpeakingAgentSource : IAgentDecisionSource
    {
        public int Calls { get; private set; }
        public Task<GameIntent?> DecideAsync(PlayerView view, AgentWakeSignal signal,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<GameIntent?>(new SpeakIntent(
                view.ParticipantId, view.Characters.Single().CharacterId, "Ready."));
        }
    }

    private sealed class WaitingAgentSource : IAgentDecisionSource
    {
        private readonly Task<GameIntent?> _task;
        public WaitingAgentSource(Task<GameIntent?> task) => _task = task;
        public int Calls { get; private set; }
        public Task<GameIntent?> DecideAsync(PlayerView view, AgentWakeSignal signal,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return _task;
        }
    }

    private sealed class NoChargeThenSpeakAgent : IAgentDecisionSource
    {
        private readonly ParticipantId _participant;
        private readonly CharacterId _character;
        public NoChargeThenSpeakAgent(ParticipantId participant, CharacterId character)
        {
            _participant = participant; _character = character;
        }
        public int Calls { get; private set; }
        public Task<GameIntent?> DecideAsync(PlayerView view, AgentWakeSignal signal,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls == 1) throw new NoChargeTransientModelException();
            return Task.FromResult<GameIntent?>(new SpeakIntent(_participant, _character, "Ready."));
        }
    }

    private sealed class FailingAgent : IAgentDecisionSource
    {
        public int Calls { get; private set; }
        public Task<GameIntent?> DecideAsync(PlayerView view, AgentWakeSignal signal,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Remote model may already have charged.");
        }
    }

    private sealed class Table
    {
        private Table(Campaign campaign, Participant human, Participant agent,
            Participant master, Character companion)
        {
            Campaign = campaign; Human = human; Agent = agent;
            Master = master; Companion = companion;
        }

        public Campaign Campaign { get; }
        public Participant Human { get; }
        public Participant Agent { get; }
        public Participant Master { get; }
        public Character Companion { get; }

        public RpgEventOrchestrator Coordinator() => new(
            Campaign,
            new SessionEngine(Campaign, new BasicD20RuleSystem(),
                new DiceRoller(new SystemRandomSource())));

        public static Table Create()
        {
            var campaign = new Campaign(CampaignId.New(), "RPG-015 Test");
            var hero = new Character(CharacterId.New(), "Hero");
            var companion = new Character(CharacterId.New(), "Companion");
            campaign.AddCharacter(hero);
            campaign.AddCharacter(companion);
            var human = new Participant(ParticipantId.New(), "Human", ControllerKind.Human,
                SeatRole.Player, [hero.Id]);
            var agent = new Participant(ParticipantId.New(), "Companion agent",
                ControllerKind.Agent, SeatRole.Player, [companion.Id]);
            var master = new Participant(ParticipantId.New(), "GM",
                ControllerKind.Agent, SeatRole.GameMaster);
            campaign.AddParticipant(human);
            campaign.AddParticipant(agent);
            campaign.AddParticipant(master);
            return new Table(campaign, human, agent, master, companion);
        }
    }
}
