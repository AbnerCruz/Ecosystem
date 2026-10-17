using System.Globalization;
using AgentRuntime.Testing;

namespace AgentRuntime.Tests;

public sealed class RunTokenUsageTests
{
    private static RuntimeEvent Response(long sequence, string? input, string? output) => new("r", sequence,
        DateTimeOffset.UnixEpoch, EventKind.ModelResponded, Rig.Agent, "t", "context", Model: "m", Cost: new Money(1, "USD"),
        Data: input is null ? null : output is null ? new Dictionary<string, string> { ["inputTokens"] = input }
            : new Dictionary<string, string> { ["inputTokens"] = input, ["outputTokens"] = output });

    [Fact]
    public async Task Runner_preserves_counts_in_each_canonical_response_event_and_replay()
    {
        var rig = new Rig(); rig.Probe("p");
        rig.Provider.Then(new ModelResponse([new ToolUseBlock("c", "p", ScriptedModelProvider.Json("{}"))], StopReason.ToolUse,
            new Usage(12, 4, new Money(1, "BRL"))));
        rig.Provider.Then(new ModelResponse([new TextBlock("done")], StopReason.EndTurn, new Usage(20, 6, new Money(1, "BRL"))));
        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["p"])));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        var events = rig.Events().Select(e => EventSerializer.FromLine(EventSerializer.ToLine(e))).ToArray();
        Assert.Equal(new RunTokenUsage(32, 10), RunTokenUsage.Read(events));
        Assert.Equal("12", events.First(e => e.Kind == EventKind.ModelResponded).Data!["inputTokens"]);
        Assert.Equal(result.State.Cost, RunState.Replay(events).Cost);
    }

    [Fact]
    public async Task Unknown_counts_survive_recording_and_are_not_logged_as_zero()
    {
        var response = new ModelResponse([new TextBlock("done")], StopReason.EndTurn,
            new Usage(0, 0, new Money(1, "BRL")) { TokensKnown = false });
        var provider = new RecordedModelProvider("recorded", [RecordedModelProvider.Record(response)]);
        var rig = new Rig();
        var result = await rig.Runner(extraProvider: provider).RunAsync(Rig.Request(providerId: "recorded"));
        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.Null(Assert.Single(rig.Events(), e => e.Kind == EventKind.ModelResponded).Data);
        Assert.Null(RunTokenUsage.Read(rig.Events()));
    }

    [Fact]
    public async Task Old_recording_without_flag_keeps_its_explicit_counts()
    {
        // Use the serializer's content format, then remove only the new optional flag.
        var current = RecordedModelProvider.Record(new ModelResponse([new TextBlock("done")], StopReason.EndTurn,
            new Usage(7, 2, new Money(1, "BRL"))));
        var provider = new RecordedModelProvider("recorded", [current.Replace(",\"tokensKnown\":true", "", StringComparison.Ordinal)]);
        var rig = new Rig(); await rig.Runner(extraProvider: provider).RunAsync(Rig.Request(providerId: "recorded"));
        Assert.Equal(new RunTokenUsage(7, 2), RunTokenUsage.Read(rig.Events()));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("1", null)]
    [InlineData("-1", "2")]
    [InlineData("1", "-2")]
    [InlineData("not-a-count", "2")]
    [InlineData(" 1", "2")]
    [InlineData("9223372036854775808", "2")]
    public void Missing_or_invalid_counts_never_become_partial_totals(string? input, string? output)
    {
        Assert.Null(RunTokenUsage.Read([Response(1, "10", "2"), Response(2, input, output)]));
    }
    [Fact]
    public void Overflow_does_not_wrap_and_actual_zero_is_distinct_from_no_measurement()
    {
        Assert.Null(RunTokenUsage.Read([Response(1, long.MaxValue.ToString(CultureInfo.InvariantCulture), "0"), Response(2, "1", "0")]));
        Assert.Equal(new RunTokenUsage(0, 0), RunTokenUsage.Read([Response(1, "0", "0")]));
        Assert.Null(RunTokenUsage.Read([]));
    }
}
