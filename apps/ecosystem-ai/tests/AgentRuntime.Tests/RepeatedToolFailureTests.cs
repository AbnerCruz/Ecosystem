using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

public class RepeatedToolFailureTests
{
    [Fact]
    public async Task Successful_mutation_invalidates_failed_read_so_create_then_read_can_recover()
    {
        var rig = new Rig();
        var exists = false;
        var read = rig.Probe("read", behavior: (_, _) => exists ? ToolOutcome.Ok("content") : ToolOutcome.Error("missing"));
        rig.Probe("create", RiskClass.Write, permissions: [Permissions.FsWrite], behavior: (_, _) =>
        {
            exists = true;
            return ToolOutcome.Ok("created");
        });
        rig.Provider.ThenToolUse("a", "read", "{}").ThenToolUse("b", "create", "{}")
            .ThenToolUse("c", "read", "{}").ThenText("done");
        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["read", "create"])));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(2, read.Invocations);
        Assert.DoesNotContain(rig.Events(), e => e.Kind == EventKind.ToolDenied);
    }

    [Fact]
    public async Task Failed_call_is_not_executed_again_after_intervening_success_and_new_call_id()
    {
        var rig = new Rig();
        var broken = rig.Probe("broken", behavior: (_, _) => ToolOutcome.Error("not found"));
        var search = rig.Probe("search");
        rig.Provider.ThenToolUse("a", "broken", "{\"path\":\"missing\"}")
            .ThenToolUse("b", "search", "{}")
            .ThenToolUse("c", "broken", "{\"path\":\"missing\"}").ThenText("explained the failure");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["broken", "search"])));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(1, broken.Invocations);
        Assert.Equal(1, search.Invocations);
        Assert.Contains(rig.Provider.Requests[^1].Messages.SelectMany(m => m.Content).OfType<ToolResultBlock>(),
            b => b.ToolUseId == "c" && b.IsError && b.Content.Contains("mude de estratégia"));
        Assert.Single(rig.Events(), e => e.Kind == EventKind.ToolDenied);
    }

    [Theory]
    [InlineData("{\"a\":1,\"nested\":{\"x\":2,\"y\":\"ok\"}}", "{ \"nested\": {\"y\":\"o\\u006b\",\"x\":2}, \"a\":1 }")]
    [InlineData("{\"items\":[{\"a\":1,\"b\":2}]}", "{\"items\":[{\"b\":2,\"a\":1}]}")]
    public async Task Equivalent_json_does_not_bypass_guard(string first, string second)
    {
        var rig = new Rig();
        var tool = rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("failed"));
        rig.Provider.ThenToolUse("a", "p", first).ThenToolUse("b", "p", second).ThenText("done");
        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(1, tool.Invocations);
    }

    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":2}")]
    [InlineData("{\"a\":1}", "{\"a\":\"1\"}")]
    [InlineData("{\"a\":\"Path\"}", "{\"a\":\"path\"}")]
    [InlineData("{\"a\":[1,2]}", "{\"a\":[2,1]}")]
    public async Task Changed_arguments_can_recover(string first, string second)
    {
        var rig = new Rig();
        var tool = rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("failed"));
        rig.Provider.ThenToolUse("a", "p", first).ThenToolUse("b", "p", second).ThenText("done");
        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(2, tool.Invocations);
    }

    [Fact]
    public async Task Failures_do_not_leak_between_capabilities_or_runs()
    {
        var rig = new Rig();
        var p = rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("failed"));
        var q = rig.Probe("q", behavior: (_, _) => ToolOutcome.Error("failed"));
        rig.Provider.ThenToolUse("a", "p", "{}").ThenToolUse("b", "q", "{}").ThenText("done")
            .ThenToolUse("a", "p", "{}").ThenText("done");
        var request = Rig.Request(grant: Rig.Grant(["p", "q"]));
        var runner = rig.Runner();
        await runner.RunAsync(request);
        await runner.RunAsync(request with { RunId = "run-2" });
        Assert.Equal(2, p.Invocations);
        Assert.Equal(1, q.Invocations);
    }

    [Fact]
    public async Task Successful_calls_remain_repeatable()
    {
        var rig = new Rig();
        var tool = rig.Probe("p");
        rig.Provider.ThenToolUse("a", "p", "{}").ThenToolUse("b", "p", "{}").ThenText("done");
        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(2, tool.Invocations);
    }

    [Fact]
    public async Task Repeats_in_one_batch_stop_at_policy_limit_without_extra_effects()
    {
        var rig = new Rig();
        var tool = rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("failed"));
        rig.Provider.Then(new ModelResponse(Enumerable.Range(0, 8)
            .Select(i => (ContentBlock)new ToolUseBlock($"c{i}", "p", ScriptedModelProvider.Json("{}"))).ToArray(),
            StopReason.ToolUse, ScriptedModelProvider.Cost(1)));
        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"]), policy: new RunPolicy(NoProgressRepeats: 2)));
        Assert.Equal(BlockReason.NoProgress, result.State.Block);
        Assert.Equal(1, tool.Invocations);
        Assert.Equal(1, rig.Provider.Calls);
        Assert.Equal(2, rig.Events().Count(e => e.Kind == EventKind.ToolDenied));
    }

    [Fact]
    public async Task Process_restart_rebuilds_failure_and_repeat_count_from_events()
    {
        var rig = new Rig();
        var tool = rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("failed"));
        var provider = new ReactiveProvider("reactive", request =>
        {
            var count = request.Messages.Count(m => m.Role == MessageRole.Assistant);
            return new ModelResponse([new ToolUseBlock($"c{count}", "p", ScriptedModelProvider.Json("{}"))],
                StopReason.ToolUse, ScriptedModelProvider.Cost(1));
        });
        var request = Rig.Request(grant: Rig.Grant(["p"]), providerId: "reactive");
        // RunCreated, ToolsResolved, ModelRequested, ModelResponded, ToolCalled,
        // ToolResult, ModelRequested, ModelResponded, ToolDenied survive; next append crashes.
        await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(new FaultInjectingEventLog(rig.Log, 9),
            extraProvider: provider).RunAsync(request));
        Assert.Equal(EventKind.ToolDenied, rig.Events()[^1].Kind);

        var result = await rig.Runner(extraProvider: provider).ResumeAsync(request);
        Assert.Equal(BlockReason.NoProgress, result.State.Block);
        Assert.Equal(1, tool.Invocations);
        Assert.Equal(3, rig.Events().Count(e => e.Kind == EventKind.ToolDenied));
    }

    [Fact]
    public async Task Process_restart_preserves_invalidation_after_successful_write()
    {
        var rig = new Rig();
        var exists = false;
        var read = rig.Probe("read", behavior: (_, _) => exists ? ToolOutcome.Ok("content") : ToolOutcome.Error("missing"));
        rig.Probe("create", RiskClass.Write, permissions: [Permissions.FsWrite], behavior: (_, _) =>
        {
            exists = true;
            return ToolOutcome.Ok("created");
        });
        var provider = new ReactiveProvider("reactive", request =>
        {
            var count = request.Messages.Count(m => m.Role == MessageRole.Assistant);
            return count >= 3
                ? new ModelResponse([new TextBlock("done")], StopReason.EndTurn, ScriptedModelProvider.Cost(1))
                : new ModelResponse([new ToolUseBlock($"c{count}", count == 1 ? "create" : "read", ScriptedModelProvider.Json("{}"))],
                    StopReason.ToolUse, ScriptedModelProvider.Cost(1));
        });
        var request = Rig.Request(grant: Rig.Grant(["read", "create"]), providerId: "reactive");
        await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(new FaultInjectingEventLog(rig.Log, 10),
            extraProvider: provider).RunAsync(request));
        var result = await rig.Runner(extraProvider: provider).ResumeAsync(request);
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Equal(2, read.Invocations);
    }

    [Fact]
    public async Task Legacy_events_without_new_metadata_remain_resumable()
    {
        var rig = new Rig();
        rig.Probe("p", behavior: (_, _) => ToolOutcome.Error("missing"));
        rig.Provider.ThenToolUse("a", "p", "{}");
        await Assert.ThrowsAsync<InjectedLogFailure>(() => rig.Runner(new FaultInjectingEventLog(rig.Log, 6))
            .RunAsync(Rig.Request(grant: Rig.Grant(["p"]))));
        var legacy = new InMemoryEventLog();
        foreach (var e in rig.Events())
            await legacy.AppendAsync(e with { Data = e.Data?.Where(pair => pair.Key != "failedInvocationSha256")
                .ToDictionary(pair => pair.Key, pair => pair.Value) }, CancellationToken.None);
        rig.Provider.ThenText("explained the failure");
        var result = await rig.Runner(legacy).ResumeAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
    }

    [Fact]
    public async Task Exceptions_are_memoized_without_logging_secret_arguments_again()
    {
        var rig = new Rig();
        const string secret = "private-argument-123";
        rig.Redactor.Register(secret);
        var tool = rig.Probe("p", behavior: (_, _) => throw new InvalidOperationException(secret));
        var input = "{\"value\":\"" + secret + "\"}";
        rig.Provider.ThenToolUse("a", "p", input).ThenToolUse("b", "p", input).ThenText("done");
        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(1, tool.Invocations);
        var events = rig.Events();
        Assert.DoesNotContain(secret, string.Join("\n", events.Select(EventSerializer.ToLine)));
        var error = Assert.Single(events, e => e.Kind == EventKind.ToolResult);
        Assert.Equal(64, error.Data!["failedInvocationSha256"].Length);
    }

    [Fact]
    public async Task Denied_approval_does_not_poison_later_explicit_approval()
    {
        var rig = new Rig();
        var tool = rig.Probe("p", RiskClass.Destructive, permissions: [Permissions.FsWrite]);
        rig.Approver = new SecondApproval();
        rig.Provider.ThenToolUse("a", "p", "{}").ThenToolUse("b", "p", "{}").ThenText("done");
        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(1, tool.Invocations);
        Assert.DoesNotContain(rig.Events(), e => e.Data?.ContainsKey("failedInvocationSha256") == true);
    }

    private sealed class SecondApproval : IApprover
    {
        private int _calls;
        public ValueTask<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(++_calls == 1 ? ApprovalDecision.Deny : new ApprovalDecision(true, "human:test"));
    }
}
