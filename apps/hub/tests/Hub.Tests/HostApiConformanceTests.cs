using System.Text.Json;
using Hub.Core.Capabilities;

namespace Hub.Tests;

public class HostApiConformanceTests
{
    private sealed record Outcome(string Layer, string? Code);

    static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ecosystem.json")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do Ecosystem não encontrada.");
    }

    static LocalContext Root() => new([new("ecosystem", "ecosystem")]);
    static LocalContext Context() => new([new("ecosystem", "ecosystem"), new("product", "hub"), new("project", "conformance")]);
    static JsonElement Input(string text = "Olá mundo\n🙂") => JsonSerializer.SerializeToElement(new { text });

    static LocalEnvelope Request(string id = "one", string capability = TextInspectionTool.CapabilityId,
        Version? version = null, JsonElement? input = null) =>
        new(LocalProtocol.Id, id, "conformance-caller", "request", capability, "inspect",
            version ?? new Version(1, 0, 0), input ?? Input());

    static LocalCapability Tool(Func<LocalInvocation, CancellationToken, Task<JsonElement>> handler,
        IEnumerable<string>? permissions = null, Func<JsonElement, bool>? validateInput = null,
        Func<JsonElement, bool>? validateOutput = null) =>
        new(TextInspectionTool.CapabilityId, "conformance-provider", new Version(1, 0, 0), "inspect",
            Root(), permissions ?? [], "stateless", validateInput ?? (_ => true), validateOutput ?? (_ => true), handler);

    static LocalCapabilityHost Host(LocalCapability capability, params string[] declared) =>
        new([capability], declared.Length == 0 ? ["ui.display"] : declared);

    static Outcome FromResponse(LocalResponse response)
    {
        if (response.Succeeded) return new("success", null);
        var hostCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "INVALID_CONTEXT", "IDENTITY_REQUIRED", "SESSION_CLOSED", "CAPABILITY_UNAVAILABLE",
            "VERSION_UNSUPPORTED", "CANCELLED", "REVOKED", "PROVIDER_CONTRACT_VIOLATION"
        };
        return hostCodes.Contains(response.Error!) ? new("host", response.Error) : new("capability", response.Error);
    }

    static async Task<Outcome> Run(string scenario, CancellationToken testToken)
    {
        switch (scenario)
        {
            case "valid-invoke":
            {
                using var session = Host(TextInspectionTool.Definition(Root())).Open("conformance-caller", Context(), []);
                return FromResponse(await session.DispatchAsync(Request(), testToken));
            }
            case "invalid-context":
            {
                try
                {
                    _ = new LocalContext([new("project", "invalid")]);
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
                    using var _ = Host(TextInspectionTool.Definition(Root())).Open("", Context(), []);
                    return new("success", null);
                }
                catch (ArgumentException)
                {
                    return new("host", "IDENTITY_REQUIRED");
                }
            }
            case "unknown-capability":
            {
                using var session = Host(TextInspectionTool.Definition(Root())).Open("conformance-caller", Context(), []);
                return FromResponse(await session.DispatchAsync(Request(capability: "unknown.capability"), testToken));
            }
            case "incompatible-version":
            {
                using var session = Host(TextInspectionTool.Definition(Root())).Open("conformance-caller", Context(), []);
                return FromResponse(await session.DispatchAsync(Request(version: new Version(2, 0, 0)), testToken));
            }
            case "missing-permission":
            {
                var protectedTool = Tool((_, _) => Task.FromResult(Input()), permissions: ["ui.display"]);
                using var session = Host(protectedTool).Open("conformance-caller", Context(), []);
                return FromResponse(await session.DispatchAsync(Request(), testToken));
            }
            case "grant-escalation":
            {
                var grants = new List<string>();
                var protectedTool = Tool((_, _) => Task.FromResult(Input()), permissions: ["ui.display"]);
                using var session = Host(protectedTool).Open("conformance-caller", Context(), grants);
                grants.Add("ui.display");
                return FromResponse(await session.DispatchAsync(Request(), testToken));
            }
            case "invalid-input":
            {
                using var session = Host(TextInspectionTool.Definition(Root())).Open("conformance-caller", Context(), []);
                var invalid = JsonSerializer.SerializeToElement(new { text = 12 });
                return FromResponse(await session.DispatchAsync(Request(input: invalid), testToken));
            }
            case "invalid-output":
            {
                var tool = Tool((_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { wrong = true })),
                    validateOutput: _ => false);
                using var session = Host(tool).Open("conformance-caller", Context(), []);
                return FromResponse(await session.DispatchAsync(Request(), testToken));
            }
            case "provider-failure":
            {
                var tool = Tool((_, _) => throw new InvalidOperationException("SECRET provider detail"));
                using var session = Host(tool).Open("conformance-caller", Context(), []);
                var outcome = FromResponse(await session.DispatchAsync(Request(), testToken));
                Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(session.Frames));
                return outcome;
            }
            case "session-closed":
            {
                var session = Host(TextInspectionTool.Definition(Root())).Open("conformance-caller", Context(), []);
                session.Dispose();
                return FromResponse(await session.DispatchAsync(Request(), testToken));
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
                var running = session.DispatchAsync(Request(), cancellation.Token);
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
                using var session = Host(tool).Open("conformance-caller", Context(), ["ui.display"]);
                var running = session.DispatchAsync(Request(), testToken);
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
    public async Task LocalHubRuntimeConformsToPublicHostApiV1Fixtures()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "contracts", "examples", "host-api", "conformance.v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("ecosystem-host-api/1.0.0", document.RootElement.GetProperty("api").GetString());

        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = test.GetProperty("id").GetString()!;
            var expectedLayer = test.GetProperty("expectedLayer").GetString();
            var expectedCodeNode = test.GetProperty("expectedCode");
            var expectedCode = expectedCodeNode.ValueKind == JsonValueKind.Null ? null : expectedCodeNode.GetString();
            var actual = await Run(test.GetProperty("scenario").GetString()!, TestContext.Current.CancellationToken);

            Assert.True(actual.Layer == expectedLayer,
                $"{id}: layer esperado {expectedLayer}, obtido {actual.Layer} ({actual.Code})");
            Assert.True(actual.Code == expectedCode,
                $"{id}: código esperado {expectedCode ?? "<null>"}, obtido {actual.Code ?? "<null>"}");
        }
    }
}
