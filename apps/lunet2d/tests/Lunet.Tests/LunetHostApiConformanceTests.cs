using System.Text.Json;
using Lunet.Core.Capabilities;

namespace Lunet.Tests;

public class LunetHostApiConformanceTests
{
    private sealed record Outcome(string Layer, string? Code);

    static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ecosystem.json")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do Ecosystem não encontrada.");
    }

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
            LunetTextInspectionCapability.CapabilityId,
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

    static async Task<Outcome> Run(string scenario, CancellationToken testToken)
    {
        switch (scenario)
        {
            case "valid-invoke":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open(
                    "conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
            }
            case "invalid-context":
            {
                try
                {
                    _ = new LunetHostContext([new("project", "invalid")]);
                    return new("success", null);
                }
                catch (ArgumentException)
                {
                    return new("host", "INVALID_CONTEXT");
                }
            }
            case "identity-missing":
            {
                try
                {
                    using var _ = Host(LunetTextInspectionCapability.Definition()).Open("", Context(), []);
                    return new("success", null);
                }
                catch (LunetHostException ex)
                {
                    return new("host", ex.Code);
                }
            }
            case "unknown-capability":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open(
                    "conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    "unknown.capability", new Version(1, 0, 0), Input(), testToken));
            }
            case "incompatible-version":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open(
                    "conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(2, 0, 0),
                    Input(),
                    testToken));
            }
            case "missing-permission":
            {
                var protectedTool = Tool((_, _) => Task.FromResult(Input()), permissions: ["ui.display"]);
                using var session = Host(protectedTool, "ui.display").Open(
                    "conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
            }
            case "grant-escalation":
            {
                var grants = new List<string>();
                var protectedTool = Tool((_, _) => Task.FromResult(Input()), permissions: ["ui.display"]);
                using var session = Host(protectedTool, "ui.display").Open(
                    "conformance-caller", Context(), grants);
                grants.Add("ui.display");
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
            }
            case "invalid-input":
            {
                using var session = Host(LunetTextInspectionCapability.Definition()).Open(
                    "conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    JsonSerializer.SerializeToElement(new { text = 12 }),
                    testToken));
            }
            case "invalid-output":
            {
                var tool = Tool(
                    (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { wrong = true })),
                    validateOutput: _ => false);
                using var session = Host(tool).Open("conformance-caller", Context(), []);
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
            }
            case "provider-failure":
            {
                var tool = Tool((_, _) => throw new InvalidOperationException("SECRET provider detail"));
                using var session = Host(tool).Open("conformance-caller", Context(), []);
                var outcome = FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
                Assert.Equal("EXECUTION_FAILED", outcome.Code);
                return outcome;
            }
            case "session-closed":
            {
                var session = Host(LunetTextInspectionCapability.Definition()).Open(
                    "conformance-caller", Context(), []);
                session.Dispose();
                return FromResponse(await session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken));
            }
            case "cancellation":
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var tool = Tool(async (_, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return Input();
                });
                using var session = Host(tool).Open("conformance-caller", Context(), []);
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(testToken);
                var running = session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    cancellation.Token);
                await started.Task.WaitAsync(testToken);
                cancellation.Cancel();
                return FromResponse(await running);
            }
            case "revocation":
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var tool = Tool(async (_, token) =>
                {
                    started.SetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return Input();
                }, permissions: ["ui.display"]);
                using var session = Host(tool, "ui.display").Open(
                    "conformance-caller", Context(), ["ui.display"]);
                var running = session.InvokeAsync(
                    LunetTextInspectionCapability.CapabilityId,
                    new Version(1, 0, 0),
                    Input(),
                    testToken);
                await started.Task.WaitAsync(testToken);
                Assert.True(session.Revoke(["ui.display"]));
                var outcome = FromResponse(await running);
                Assert.Empty(session.Discover());
                return outcome;
            }
            default:
                throw new InvalidOperationException($"Fixture sem adapter de teste: {scenario}");
        }
    }

    [Fact]
    public async Task LunetRuntimeConformsToPublicHostApiV1Fixtures()
    {
        var path = Path.Combine(
            RepositoryRoot(), "docs", "contracts", "examples", "host-api", "conformance.v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("ecosystem-host-api/1.0.0", document.RootElement.GetProperty("api").GetString());

        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = test.GetProperty("id").GetString()!;
            var expectedLayer = test.GetProperty("expectedLayer").GetString();
            var expectedCodeNode = test.GetProperty("expectedCode");
            var expectedCode = expectedCodeNode.ValueKind == JsonValueKind.Null
                ? null
                : expectedCodeNode.GetString();

            var actual = await Run(
                test.GetProperty("scenario").GetString()!,
                TestContext.Current.CancellationToken);

            Assert.True(actual.Layer == expectedLayer,
                $"{id}: layer esperado {expectedLayer}, obtido {actual.Layer} ({actual.Code})");
            Assert.True(actual.Code == expectedCode,
                $"{id}: código esperado {expectedCode ?? "<null>"}, obtido {actual.Code ?? "<null>"}");
        }
    }
}
