using System.Text.Json;
using Hub.Core.Capabilities;

namespace Hub.Tests;

public class TextInspectionContractTests
{
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ecosystem.json")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz do Ecosystem não encontrada.");
    }

    private static JsonElement StableVersion(JsonDocument document)
        => document.RootElement.GetProperty("versions").EnumerateArray()
            .Single(version => version.GetProperty("version").GetString() == "1.0.0");

    [Fact]
    public void LocalDefinitionMatchesStableContract()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", "contracts", "capabilities", "text.inspect.json")));
        var root = document.RootElement;
        var version = StableVersion(document);
        var scope = new LocalContext([new("ecosystem", "local")]);
        var definition = TextInspectionTool.Definition(scope);

        Assert.Equal(TextInspectionTool.CapabilityId, root.GetProperty("capability").GetString());
        Assert.Equal("semver", root.GetProperty("compatibility").GetString());
        Assert.Equal(definition.Version.ToString(3), version.GetProperty("version").GetString());
        Assert.Equal("stable", version.GetProperty("status").GetString());
        Assert.Equal(definition.Lifecycle, version.GetProperty("lifecycle").GetString());

        var permissions = version.GetProperty("requiredPermissions").EnumerateArray()
            .Select(value => value.GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Empty(permissions);
        Assert.Empty(definition.RequiredPermissions);

        var input = Assert.Single(version.GetProperty("inputs").EnumerateArray());
        Assert.Equal("text", input.GetProperty("name").GetString());
        Assert.Equal("string", input.GetProperty("type").GetString());
        Assert.True(input.GetProperty("required").GetBoolean());

        var outputs = version.GetProperty("outputs").EnumerateArray().ToArray();
        Assert.Equal(new[] { "characters", "lines", "words" },
            outputs.Select(field => field.GetProperty("name").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(outputs, field =>
        {
            Assert.Equal("integer", field.GetProperty("type").GetString());
            Assert.True(field.GetProperty("required").GetBoolean());
        });

        var errors = version.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "EXECUTION_FAILED", "INVALID_INPUT" }, errors);
    }

    [Fact]
    public async Task RuntimeResultSatisfiesStableOutputShape()
    {
        var context = new LocalContext([new("ecosystem", "local"), new("product", "hub")]);
        var definition = TextInspectionTool.Definition(new LocalContext([new("ecosystem", "local")]));
        var host = new LocalCapabilityHost([definition], ["ui.display"]);
        using var session = host.Open("contract-test", context, ["ui.display"]);
        var input = JsonSerializer.SerializeToElement(new { text = "Olá mundo\n🙂" });
        var request = new LocalEnvelope(LocalProtocol.Id, "contract-1", "contract-test", "request",
            TextInspectionTool.CapabilityId, "inspect", new Version(1, 0, 0), input);

        var response = await session.DispatchAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);
        var output = response.Output!.Value;
        Assert.Equal(JsonValueKind.Object, output.ValueKind);
        Assert.Equal(new[] { "characters", "lines", "words" },
            output.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.All(output.EnumerateObject(), property =>
        {
            Assert.Equal(JsonValueKind.Number, property.Value.ValueKind);
            Assert.True(property.Value.TryGetInt32(out var value));
            Assert.True(value >= 0);
        });
    }
}
