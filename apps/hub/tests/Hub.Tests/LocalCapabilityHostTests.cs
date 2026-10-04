using System.Text.Json;
using Hub.Core.Capabilities;

namespace Hub.Tests;

public class LocalCapabilityHostTests
{
    static LocalContext Context(string project = "one") => new([
        new("ecosystem", "local"), new("product", "alpha"), new("project", project)]);
    static LocalContext Root() => new([new("ecosystem", "local")]);
    static JsonElement Input(string text = "Olá mundo\n🙂") => JsonSerializer.SerializeToElement(new { text });
    static LocalEnvelope Request(string id = "one", string source = "user") => new(
        LocalProtocol.Id, id, source, "request", TextInspectionTool.CapabilityId, "inspect", new(1, 0, 0), Input());
    static LocalCapabilityHost Host(LocalCapability? tool = null, params string[] declared) =>
        new([tool ?? TextInspectionTool.Definition(Root())], declared.Length == 0 ? ["ui.display"] : declared);
    static LocalCapability Tool(Func<LocalInvocation, CancellationToken, Task<JsonElement>> handler,
        LocalContext? scope = null, Version? version = null, Func<JsonElement, bool>? validateOutput = null,
        IEnumerable<string>? permissions = null)
        => new(TextInspectionTool.CapabilityId, "test-provider", version ?? new(1, 0, 0), "inspect",
            scope ?? Root(), permissions ?? ["ui.display"], "stateless", _ => true, validateOutput ?? (_ => true), handler);

    [Fact]
    public async Task OneToolRunsInTwoContextsWithoutConcreteHostNames()
    {
        var host = Host();
        foreach (var project in new[] { "one", "two" })
        {
            using var session = host.Open("user", Context(project), ["ui.display"]);
            Assert.Single(session.Discover());
            var response = await session.DispatchAsync(Request(), TestContext.Current.CancellationToken);
            Assert.True(response.Succeeded);
            Assert.Equal(3, response.Output!.Value.GetProperty("words").GetInt32());
            Assert.Equal(11, response.Output.Value.GetProperty("characters").GetInt32());
            Assert.Equal(2, response.Output.Value.GetProperty("lines").GetInt32());
            Assert.All(session.Frames, f => Assert.Equal(project, f.Context.Path.Last().Id));
            Assert.Equal(new[] { "event", "request", "progress", "progress", "response" }, session.Frames.Select(f => f.Kind));
            Assert.All(session.Frames.Where(f => f.Kind != "event"), f => Assert.Equal("one", f.RequestId));
        }
    }

    [Fact]
    public async Task UndeclaredOrUngrantedPermissionIsNeverInherited()
    {
        using var noGrant = Host(Tool((_, _) => Task.FromResult(Input()))).Open("user", Context(), []);
        using var notDeclared = Host(Tool((_, _) => Task.FromResult(Input())), "fs.read").Open("user", Context(), ["ui.display"]);
        foreach (var session in new[] { noGrant, notDeclared })
        {
            Assert.Empty(session.Discover());
            Assert.Equal("CAPABILITY_UNAVAILABLE", (await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
        }
    }

    [Fact]
    public async Task ContextIsCapturedAndScopeCannotCrossProjects()
    {
        var steps = new[] { new ContextStep("ecosystem", "local"), new("project", "one") };
        var context = new LocalContext(steps);
        steps[1] = new("project", "two");
        var host = Host(Tool((_, _) => Task.FromResult(Input()), new LocalContext([
            new("ecosystem", "local"), new("project", "one")])));
        using var allowed = host.Open("user", context, ["ui.display"]);
        using var denied = host.Open("user", new LocalContext([new("ecosystem", "local"), new("project", "two")]), ["ui.display"]);
        Assert.True((await allowed.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal("CAPABILITY_UNAVAILABLE", (await denied.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
    }

    [Theory]
    [InlineData("protocol", "PROTOCOL_UNSUPPORTED")]
    [InlineData("actor", "INVALID_ENVELOPE")]
    [InlineData("kind", "INVALID_ENVELOPE")]
    [InlineData("id", "INVALID_ENVELOPE")]
    [InlineData("operation", "OPERATION_UNSUPPORTED")]
    [InlineData("capability", "CAPABILITY_UNAVAILABLE")]
    [InlineData("version", "VERSION_UNSUPPORTED")]
    public async Task InvalidEnvelopesNeverReachTheTool(string field, string error)
    {
        var calls = 0;
        using var session = Host(Tool((_, _) => { calls++; return Task.FromResult(Input()); })).Open("user", Context(), ["ui.display"]);
        var request = field switch
        {
            "protocol" => Request() with { Protocol = "ecosystem-local/999" },
            "actor" => Request(source: "impersonator"),
            "kind" => Request() with { Kind = "event" },
            "id" => Request(id: ""),
            "operation" => Request() with { Operation = "delete" },
            "capability" => Request() with { Capability = "file.write" },
            _ => Request() with { MinimumVersion = new(2, 0, 0) }
        };
        Assert.Equal(error, (await session.DispatchAsync(request, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(0, calls);
        Assert.All(session.Frames, f => Assert.Equal("user", f.Source));
    }

    [Fact]
    public async Task SameMajorCompatibilityAndExperimentalZeroAreExplicit()
    {
        using var newer = Host(Tool((_, _) => Task.FromResult(Input()), version: new(1, 2, 0))).Open("user", Context(), ["ui.display"]);
        Assert.True((await newer.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal("VERSION_UNSUPPORTED", (await newer.DispatchAsync(Request("two") with { MinimumVersion = new(1, 3, 0) }, TestContext.Current.CancellationToken)).Error);
        using var zero = Host(Tool((_, _) => Task.FromResult(Input()), version: new(0, 2, 0))).Open("user", Context(), ["ui.display"]);
        Assert.Equal("VERSION_UNSUPPORTED", (await zero.DispatchAsync(Request() with { MinimumVersion = new(0, 1, 0) }, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task MutatingGrantCollectionCannotEscalateTheSession()
    {
        var grants = new List<string>();
        using var session = Host(Tool((_, _) => Task.FromResult(Input()))).Open("user", Context(), grants);
        grants.Add("ui.display");
        Assert.Empty(session.Discover());
        Assert.Equal("CAPABILITY_UNAVAILABLE", (await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task InputRejectsUnknownFieldsWrongTypeAndOversizedText()
    {
        using var session = Host().Open("user", Context(), ["ui.display"]);
        var inputs = new[] { JsonSerializer.SerializeToElement(new { text = 12 }),
            JsonSerializer.SerializeToElement(new { text = "ok", permission = "fs.write" }),
            Input(new string('x', TextInspectionTool.MaximumLength + 1)) };
        foreach (var input in inputs)
            Assert.Equal("INVALID_INPUT", (await session.DispatchAsync(Request() with { Input = input }, TestContext.Current.CancellationToken)).Error);
        Assert.True((await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task DuplicateRequestDoesNotExecuteAgainAndJournalIsBounded()
    {
        using var session = Host().Open("user", Context(), ["ui.display"]);
        Assert.True((await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal("DUPLICATE_REQUEST", (await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
        for (var i = 0; i < 30; i++) Assert.True((await session.DispatchAsync(Request("next-" + i), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(LocalHostSession.JournalLimit, session.Frames.Count);
        Assert.Equal(session.Frames.Select(f => f.Sequence).Order(), session.Frames.Select(f => f.Sequence));
    }

    [Fact]
    public async Task AcceptedRequestIdentityHasAFiniteBudget()
    {
        using var session = Host().Open("user", Context(), ["ui.display"]);
        for (var i = 0; i < LocalHostSession.RequestLimit; i++)
            Assert.True((await session.DispatchAsync(Request("next-" + i), TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal("SESSION_LIMIT", (await session.DispatchAsync(Request("overflow"), TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task PreCancelledRequestHasNoToolEffectAndCanRetry()
    {
        using var session = Host().Open("user", Context(), ["ui.display"]);
        Assert.Equal("CANCELLED", (await session.DispatchAsync(Request(), new CancellationToken(true))).Error);
        Assert.True((await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task BusyAndClosedSessionsDoNotStartAnotherCall()
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = Host(Tool(async (_, token) => { start.SetResult(); await Task.Delay(Timeout.Infinite, token); return Input(); }))
            .Open("user", Context(), ["ui.display"]);
        var running = session.DispatchAsync(Request(), TestContext.Current.CancellationToken);
        await start.Task;
        Assert.Equal("SESSION_BUSY", (await session.DispatchAsync(Request("two"), TestContext.Current.CancellationToken)).Error);
        session.Dispose();
        session.Dispose();
        Assert.Equal(new[] { "session.opened", "session.closed" }, session.Frames.Where(f => f.Kind == "event").Select(f => f.Event));
        Assert.Equal("CANCELLED", (await running).Error);
        Assert.Empty(session.Discover());
        Assert.Equal("SESSION_CLOSED", (await session.DispatchAsync(Request("three"), TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task IgnoringCancellationDoesNotReportSuccessAfterRevocation()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = Host(Tool(async (_, _) => { await gate.Task; return Input(); })).Open("user", Context(), ["ui.display"]);
        var running = session.DispatchAsync(Request(), TestContext.Current.CancellationToken);
        session.Dispose(); gate.SetResult();
        Assert.Equal("CANCELLED", (await running).Error);
        Assert.DoesNotContain(session.Frames, f => f.Kind == "response");
    }

    [Fact]
    public async Task InvalidOutputDoesNotBecomeSuccessAndSessionRecovers()
    {
        var calls = 0;
        static bool Valid(JsonElement value) => value.ValueKind == JsonValueKind.Object
            && value.EnumerateObject().Count() == 1
            && value.TryGetProperty("value", out var number) && number.TryGetInt32(out _);
        using var session = Host(Tool((_, _) => Task.FromResult(++calls == 1
                ? JsonSerializer.SerializeToElement(new { wrong = true })
                : JsonSerializer.SerializeToElement(new { value = 7 })),
            validateOutput: Valid)).Open("user", Context(), ["ui.display"]);

        Assert.Equal("PROVIDER_CONTRACT_VIOLATION", (await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
        Assert.DoesNotContain(session.Frames, frame => frame.Kind == "response");
        var recovered = await session.DispatchAsync(Request("two"), TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded);
        Assert.Equal(7, recovered.Output!.Value.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task RevokingGrantCancelsActiveCallAndCannotRestorePrivilege()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var protectedTool = Tool(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Input();
        });
        using var session = Host(protectedTool).Open("user", Context(), ["ui.display"]);
        Assert.Single(session.Discover());
        var running = session.DispatchAsync(Request(), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(session.Revoke(["ui.display"]));
        Assert.Equal("REVOKED", (await running).Error);
        Assert.Empty(session.Discover());
        Assert.Equal("CAPABILITY_UNAVAILABLE",
            (await session.DispatchAsync(Request("after-revoke"), TestContext.Current.CancellationToken)).Error);
        Assert.Contains(session.Frames, frame => frame.Event == "session.grants-revoked");
        Assert.False(session.Revoke(["ui.display"]));
    }

    [Fact]
    public async Task HandlerErrorDoesNotLeakItsMessageAndSessionRecovers()
    {
        var calls = 0;
        using var session = Host(Tool((_, _) => { if (++calls == 1) throw new Exception("SECRET-should-never-be-logged"); return Task.FromResult(Input()); }))
            .Open("user", Context(), ["ui.display"]);
        Assert.Equal("EXECUTION_FAILED", (await session.DispatchAsync(Request(), TestContext.Current.CancellationToken)).Error);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(session.Frames));
        Assert.True((await session.DispatchAsync(Request("two") with { Kind = "command" }, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task LateProgressIsIgnoredAndInputIsNotStoredInJournal()
    {
        LocalInvocation? call = null;
        using var session = Host(Tool((input, _) => { call = input; return Task.FromResult(Input()); })).Open("user", Context(), ["ui.display"]);
        Assert.True((await session.DispatchAsync(Request() with { Input = Input("private-user-content") }, TestContext.Current.CancellationToken)).Succeeded);
        var before = session.Frames.Count;
        call!.Progress(50);
        Assert.Equal(before, session.Frames.Count);
        Assert.DoesNotContain("private-user-content", JsonSerializer.Serialize(session.Frames));
    }

    [Fact]
    public async Task InvalidIdentifiersCannotMakeJournalUnbounded()
    {
        using var session = Host().Open("user", Context(), ["ui.display"]);
        Assert.Equal("INVALID_ENVELOPE", (await session.DispatchAsync(Request(new string('x', 100_000))
            with { Capability = new string('y', 100_000) }, TestContext.Current.CancellationToken)).Error);
        Assert.All(session.Frames, f => { Assert.True(f.RequestId.Length <= 128); Assert.True(f.Capability.Length <= 128); });
    }

    [Fact]
    public async Task CallerCancellationStopsActiveCallAndSessionCanRunAgain()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var session = Host(Tool(async (_, token) =>
        {
            if (++calls == 1) { started.SetResult(); await Task.Delay(Timeout.Infinite, token); }
            return Input();
        })).Open("user", Context(), ["ui.display"]);
        var running = session.DispatchAsync(Request(), cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        Assert.Equal("CANCELLED", (await running).Error);
        Assert.True((await session.DispatchAsync(Request("two"), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public void InvalidCapabilityLifecycleIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new LocalCapability(TextInspectionTool.CapabilityId, "test-provider",
            new Version(1, 0, 0), "inspect", Root(), ["ui.display"], "forever", _ => true, _ => true,
            (_, _) => Task.FromResult(Input())));
    }

    [Fact]
    public void InvalidContextAndAmbiguousRegistryAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new LocalContext([new("project", "one")]));
        Assert.Throws<ArgumentException>(() => new LocalContext([new("ecosystem", "local"), new("product", "one"), new("ecosystem", "other")]));
        Assert.Throws<ArgumentException>(() => new LocalCapabilityHost([TextInspectionTool.Definition(Root()), TextInspectionTool.Definition(Root())], ["ui.display"]));
    }
}
