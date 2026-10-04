using System.Text.Json;
using Hub.Core.Capabilities;

namespace Hub.Tests;

public class AuthenticatedHostGatewayTests
{
    static LocalContext Root() => new([new("ecosystem", "ecosystem")]);
    static LocalContext Context() => new([
        new("ecosystem", "ecosystem"), new("product", "consumer"), new("workspace", "ide"),
        new("tool", "text-inspect")]);

    static AuthenticatedHostGateway Gateway(LocalCapability? tool = null, TimeProvider? clock = null) =>
        new(new LocalCapabilityHost([tool ?? TextInspectionTool.Definition(Root())], []), clock);

    sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan amount) => _now += amount;
    }

    static JsonElement Input(string text = "um dois\ntrês") => JsonSerializer.SerializeToElement(new { text });

    [Fact]
    public async Task AuthenticatedPeerCanOpenDiscoverInvokeAndClose()
    {
        using var gateway = Gateway();
        var opened = gateway.Open("install-key-a", "android:io.lunet.studio:key-a", Context(), []);
        Assert.True(opened.Succeeded);

        var discovered = gateway.Discover("install-key-a", opened.SessionId!);
        Assert.True(discovered.Succeeded);
        var capability = Assert.Single(discovered.Capabilities);
        Assert.Equal("text.inspect", capability.Id);
        Assert.Equal("1.0.0", capability.Version);

        var response = await gateway.InvokeAsync("install-key-a", opened.SessionId!, "r1",
            "text.inspect", "inspect", new Version(1, 0, 0), Input(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(response.Succeeded);
        Assert.Equal(3, response.Output!.Value.GetProperty("words").GetInt32());

        Assert.True(gateway.Close("install-key-a", opened.SessionId!));
        Assert.Equal("SESSION_CLOSED", gateway.Discover("install-key-a", opened.SessionId!).Error);
    }

    [Fact]
    public async Task SessionIdFromAnotherPeerNeverCrossesIdentityBoundary()
    {
        using var gateway = Gateway();
        var opened = gateway.Open("peer-a", "actor-a", Context(), []);
        Assert.True(opened.Succeeded);

        Assert.Equal("SESSION_CLOSED", gateway.Discover("peer-b", opened.SessionId!).Error);
        var response = await gateway.InvokeAsync("peer-b", opened.SessionId!, "stolen",
            "text.inspect", "inspect", new Version(1, 0, 0), Input(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("SESSION_CLOSED", response.HostError);
        Assert.False(gateway.Cancel("peer-b", opened.SessionId!, "stolen"));
        Assert.False(gateway.Close("peer-b", opened.SessionId!));

        Assert.True((await gateway.InvokeAsync("peer-a", opened.SessionId!, "owner",
            "text.inspect", "inspect", new Version(1, 0, 0), Input(),
            cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public void SessionBudgetIsFiniteAndDisconnectInvalidatesPeerSessions()
    {
        using var gateway = Gateway();
        var ids = Enumerable.Range(0, AuthenticatedHostGateway.MaxSessions)
            .Select(i => gateway.Open("peer-a", "actor-a", Context(), []).SessionId!)
            .ToArray();
        Assert.Equal("SESSION_LIMIT", gateway.Open("peer-a", "actor-a", Context(), []).Error);
        Assert.Equal(ids.Length, gateway.Disconnect("peer-a"));
        Assert.All(ids, id => Assert.Equal("SESSION_CLOSED", gateway.Discover("peer-a", id).Error));
        Assert.True(gateway.Open("peer-a", "actor-a", Context(), []).Succeeded);
    }

    [Fact]
    public async Task DuplicateRequestIsRejectedWithoutSecondProviderEffect()
    {
        var calls = 0;
        var tool = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, (_, _) =>
            {
                calls++;
                return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = true }));
            });
        using var gateway = Gateway(tool);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;

        Assert.True((await gateway.InvokeAsync("peer", session, "same", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        var duplicate = await gateway.InvokeAsync("peer", session, "same", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("DUPLICATE_REQUEST", duplicate.HostError);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancelUsesControlPathAndLateSuccessIsDiscarded()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return Input();
            });
        using var gateway = Gateway(tool);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;
        var running = gateway.InvokeAsync("peer", session, "r1", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(gateway.Cancel("peer", session, "r1"));
        var response = await running;
        Assert.Equal("CANCELLED", response.HostError);
        Assert.Null(response.Output);
    }

    [Fact]
    public async Task DeadlineIsTransportFailureAndSessionRemainsUsable()
    {
        var calls = 0;
        var tool = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, async (_, token) =>
            {
                if (++calls == 1) await Task.Delay(Timeout.Infinite, token);
                return JsonSerializer.SerializeToElement(new { ok = true });
            });
        using var gateway = Gateway(tool);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;

        var timedOut = await gateway.InvokeAsync("peer", session, "r1", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        Assert.Equal("DEADLINE_EXCEEDED", timedOut.TransportError);

        var retry = await gateway.InvokeAsync("peer", session, "r2", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.True(retry.Succeeded);
    }

    [Fact]
    public async Task InvalidDeadlineNeverStartsProvider()
    {
        var calls = 0;
        var tool = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, (_, _) =>
            {
                calls++;
                return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = true }));
            });
        using var gateway = Gateway(tool);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;
        var response = await gateway.InvokeAsync("peer", session, "r1", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        Assert.Equal("DEADLINE_INVALID", response.TransportError);
        Assert.Equal(0, calls);
    }
    [Fact]
    public async Task ConcurrentOpenNeverExceedsSessionBudget()
    {
        using var gateway = Gateway();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => gateway.Open("peer", "actor", Context(), []))));
        Assert.Equal(AuthenticatedHostGateway.MaxSessions, attempts.Count(x => x.Succeeded));
        Assert.All(attempts.Where(x => !x.Succeeded), x => Assert.Equal("SESSION_LIMIT", x.Error));
    }

    [Fact]
    public void IdleSessionExpiresAndReleasesBudget()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        using var gateway = Gateway(clock: clock);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;
        clock.Advance(AuthenticatedHostGateway.SessionIdleTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal("SESSION_CLOSED", gateway.Discover("peer", session).Error);
        Assert.True(gateway.Open("peer", "actor", Context(), []).Succeeded);
    }

    [Fact]
    public async Task NonCooperativeProviderCannotHoldCallerPastDeadline()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var tool = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, async (_, _) =>
            {
                calls++;
                started.TrySetResult();
                await release.Task;
                return JsonSerializer.SerializeToElement(new { ok = true });
            });
        using var gateway = Gateway(tool);
        var session = gateway.Open("peer", "actor", Context(), []).SessionId!;

        var running = gateway.InvokeAsync("peer", session, "slow", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        var timedOut = await running.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal("DEADLINE_EXCEEDED", timedOut.TransportError);

        var whileLate = await gateway.InvokeAsync("peer", session, "blocked", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal("SESSION_BUSY", whileLate.TransportError);

        release.TrySetResult();
        GatewayInvokeResult? recovered = null;
        for (var i = 0; i < 50 && recovered is null; i++)
        {
            var candidate = await gateway.InvokeAsync("peer", session, "after", "text.inspect", "inspect",
                new Version(1, 0, 0), Input(), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            if (candidate.TransportError == "SESSION_BUSY")
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                continue;
            }
            recovered = candidate;
        }

        Assert.NotNull(recovered);
        Assert.True(recovered!.Succeeded);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidInputVersionAndProviderFailureRemainSanitized()
    {
        using (var gateway = Gateway())
        {
            var session = gateway.Open("peer", "actor", Context(), []).SessionId!;
            var badVersion = await gateway.InvokeAsync("peer", session, "v", "text.inspect", "inspect",
                new Version(2, 0, 0), Input(), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("VERSION_UNSUPPORTED", badVersion.HostError);

            var badInput = await gateway.InvokeAsync("peer", session, "i", "text.inspect", "inspect",
                new Version(1, 0, 0), JsonSerializer.SerializeToElement(new { text = 12 }),
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("INVALID_INPUT", badInput.HostError);
        }

        var faulting = new LocalCapability("text.inspect", "test", new Version(1, 0, 0), "inspect", Root(), [],
            "stateless", _ => true, _ => true, (_, _) => throw new InvalidOperationException("SECRET-provider-detail"));
        using var faultGateway = Gateway(faulting);
        var faultSession = faultGateway.Open("peer", "actor", Context(), []).SessionId!;
        var failed = await faultGateway.InvokeAsync("peer", faultSession, "f", "text.inspect", "inspect",
            new Version(1, 0, 0), Input(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("EXECUTION_FAILED", failed.HostError);
        Assert.Null(failed.Output);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(failed));
    }

}
