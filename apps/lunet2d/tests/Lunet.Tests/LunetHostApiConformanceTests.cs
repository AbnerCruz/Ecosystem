using System.Text.Json;
using Lunet.Core.Capabilities;

namespace Lunet.Tests;

public class LunetHostApiConformanceTests
{
    private sealed record Outcome(string Layer, string? Code);

    static LunetHostContext Root() => new([new("ecosystem", "ecosystem")]);
    static LunetHostContext Context() => new([
        new("ecosystem", "ecosystem"),
        new("product", "lunet2d"),
        new("project", "conformance")
    ]);
    static JsonElement Input(string text = "Olá mundo\n🙂") => JsonSerializer.SerializeToElement(new { text });

    static LunetHostCapability Tool(
        Func<LunetCapabilityInvocation, CancellationToken, Task<JsonElement>> handler,
        IEnumerable<string>? permissions = null,
        Func<JsonElement, bool>? validateInput = null,
        Func<JsonElement, bool>? validateOutput = null) =>
        new(
            "text.inspect",
            "conformance-provider",
            new Version(1, 0, 0),
            Root(),
            permissions ?? [],
            "stateless",
            validateInput ?? (_ => true),
            validateOutput ?? (_ => true),
            handler);

    static LunetCapabilityHost Host(LunetHostCapability capability, params string[] declared) =>
        new([capability], declared);

    static Outcome FromResponse(LunetHostResponse response)
    {
        if (response.Succeeded) return new("success", null);
        if (response.HostError is not null) return new("host", response.HostError);
        return new("capability", response.CapabilityError);
    }

    [Fact]
    public async Task HostImplementsApplicableHostApiV1Matrix()
    {
        var cases = new (string Scenario, string Layer, string? Code)[]
        {
            ("valid-invoke", "success", null),
            ("invalid-context", "host", "INVALID_CONTEXT"),
            ("unknown-capability", "host", "CAPABILITY_UNAVAILABLE"),
            ("incompatible-version", "host", "VERSION_UNSUPPORTED"),
            ("missing-permission", "host", "CAPABILITY_UNAVAILABLE"),
            ("grant-escalation", "host", "CAPABILITY_UNAVAILABLE"),
            ("invalid-input", "capability", "INVALID_INPUT"),
            ("invalid-output", "host", "PROVIDER_CONTRACT_VIOLATION"),
            ("cancellation", "host", "CANCELLED"),
            ("revocation", "host", "REVOKED"),
            ("session-closed", "host", "SESSION_CLOSED"),
            ("provider-failure", "capability", "EXECUTION_FAILED"),
            ("identity-missing", "host", "IDENTITY_REQUIRED")
        };

        foreach (var test in cases)
        {
            var actual = await Run(test.Scenario, TestContext.Current.CancellationToken);
            Assert.True(actual.Layer == test.Layer,
                $"{test.Scenario}: layer esperado {test.Layer}, obtido {actual.Layer} ({actual.Code})");
            Assert.True(actual.Code == test.Code,
                $"{test.Scenario}: código esperado {test.Code ?? "<null>"}, obtido {actual.Code ?? "<null>"}");
        }
    }

    static async Task<Outcome> Run(string scenario, CancellationToken testToken)
    {
        switch (scenario)
        {
            case "valid-invoke":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "invalid-context":
            {
                try
                {
                    _ = new LunetHostContext([new("project", "invalid")]);
                    return new("success", null);
                }
                catch (ArgumentException) { return new("host", "INVALID_CONTEXT"); }
            }
            case "identity-missing":
            {
                try
                {
                    using var _ = Host(LunetTextInspectionCapability.Definition()).Open("", Context(), []);
                    return new("success", null);
                }
                catch (LunetHostException ex) { return new("host", ex.Code); }
            }
            case "unknown-capability":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("unknown.capability", new Version(1, 0, 0), Input(), testToken));
            }
            case "incompatible-version":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(2, 0, 0), Input(), testToken));
            }
            case "missing-permission":
            {
                using var session = Host(Tool((_, _) => Task.FromResult(Input()), ["ui.display"]), "ui.display")
                    .Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "grant-escalation":
            {
                var grants = new List<string>();
                using var session = Host(Tool((_, _) => Task.FromResult(Input()), ["ui.display"]), "ui.display")
                    .Open("caller", Context(), grants);
                grants.Add("ui.display");
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "invalid-input":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0),
                    JsonSerializer.SerializeToElement(new { text = 12 }), testToken));
            }
            case "invalid-output":
            {
                using var session = Host(Tool((_, _) =>
                    Task.FromResult(JsonSerializer.SerializeToElement(new { wrong = true })),
                    validateOutput: _ => false)).Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "provider-failure":
            {
                using var session = Host(Tool((_, _) => throw new InvalidOperationException("SECRET")))
                    .Open("caller", Context(), []);
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "session-closed":
            {
                var session = Host(LunetTextInspectionCapability.Definition()).Open("caller", Context(), []);
                session.Dispose();
                return FromResponse(await session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken));
            }
            case "cancellation":
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var session = Host(Tool(async (_, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return Input();
                })).Open("caller", Context(), []);

                var running = session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken);
                await started.Task.WaitAsync(testToken);
                Assert.True(session.CancelActive());
                return FromResponse(await running);
            }
            case "revocation":
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var session = Host(Tool(async (_, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return Input();
                }, ["ui.display"]), "ui.display").Open("caller", Context(), ["ui.display"]);

                var running = session.InvokeAsync("text.inspect", new Version(1, 0, 0), Input(), testToken);
                await started.Task.WaitAsync(testToken);
                Assert.True(session.Revoke(["ui.display"]));
                var outcome = FromResponse(await running);
                Assert.Empty(session.Discover());
                return outcome;
            }
            default:
                throw new InvalidOperationException("Cenário não implementado: " + scenario);
        }
    }
}
