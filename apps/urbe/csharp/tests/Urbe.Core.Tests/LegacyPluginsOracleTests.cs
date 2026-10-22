using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Plugin listing data, templates and approval fingerprint against the unmodified plugins.js (tools/plugins-oracle.mjs).</summary>
public sealed class LegacyPluginsOracleTests
{
    [Fact]
    public async Task TemplatesMetadataAndFingerprintMatchTheOriginal()
    {
        var files = new Dictionary<string, string>
        {
            ["Personalização/plugins/a.js"] = "urbe.plugin({ nome: 'Contador', versao: '2.1', descricao: \"Conta tudo\", ligar(api){} });",
            ["Personalização/plugins/Meu plugin.js"] = "// nada\nurbe.plugin({ ligar(){} })",
            ["Personalização/plugins/b.js"] = "urbe.plugin({\n nome: `Longo ção`,\n descricao: 'x'\n});",
            ["Personalização/plugins/c.txt"] = "nao e plugin",
            ["Outra/d.js"] = "nome: 'fora'"
        };
        var creates = new (string? Name, string Template)[]
        {
            ("Meu plugin", "basico"), ("  Diário d'água  ", "diario"), ("a/b:c", "assistente"), ("", "assistente"), (null, "inexistente"), ("a", "basico")
        };
        var input = new JsonObject
        {
            ["files"] = JsonNode.Parse(JsonSerializer.Serialize(files)),
            ["creates"] = new JsonArray(creates.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["template"] = c.Template }).ToArray())
        };
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "plugins-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        await process.StandardInput.WriteAsync(input.ToJsonString());
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var json = JsonDocument.Parse(await stdout);
        var e = json.RootElement;

        Assert.Equal(e.GetProperty("templates").EnumerateArray().Where(t => t.GetProperty("id").GetString() != WithoutTheCity.MapTemplate)
                .Select(t => WithoutTheCity.Native(t.GetProperty("id").GetString() + "|" + t.GetProperty("nome").GetString() + "|" + t.GetProperty("descricao").GetString())),
            LegacyPlugins.Templates.Select(t => t.Id + "|" + t.Name + "|" + t.Description));

        var list = files.Where(f => f.Key.StartsWith(LegacyPlugins.Folder, StringComparison.Ordinal) && f.Key.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Key, StringComparer.InvariantCulture)
            .Select(f => LegacyPlugins.Read(f.Key, f.Key.Split('/').Last(), f.Value)).ToList();
        var listed = e.GetProperty("list").EnumerateArray().ToList();
        Assert.Equal(listed.Count, list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            Assert.Equal(listed[i].GetProperty("path").GetString(), list[i].Path);
            Assert.Equal(listed[i].GetProperty("nome").GetString(), list[i].Name);
            Assert.Equal(listed[i].GetProperty("versao").GetString(), list[i].Version);
            Assert.Equal(listed[i].GetProperty("descricao").GetString(), list[i].Description);
            Assert.Equal(listed[i].GetProperty("tamanho").GetInt32(), list[i].Size);
        }
        foreach (var p in e.GetProperty("fingerprints").EnumerateObject())
            Assert.Equal(p.Value.GetString(), LegacyPlugins.Fingerprint(files[p.Name]));

        var made = new Dictionary<string, string>(files);
        int k = 0;
        foreach (var c in e.GetProperty("creates").EnumerateArray())
        {
            var (path, content) = LegacyPlugins.Create(creates[k].Name, creates[k].Template, made.ContainsKey);
            made[path] = content;
            Assert.Equal(c.GetProperty("path").GetString(), path);
            Assert.Equal(c.GetProperty("content").GetString(), content);
            k++;
        }
    }
}
