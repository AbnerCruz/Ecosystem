using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Personalização against the unmodified 1.8.4-beta customize.js run by Node
/// (tools/customize-oracle.mjs): normalize (configuration, errors and warnings), and the exact
/// tema.json text each set(patch) writes.
/// </summary>
public sealed class LegacyCustomizeOracleTests
{
    private static JsonArray Cases()
    {
        static JsonObject C(string? text, JsonArray? sets = null, JsonObject? files = null)
        {
            var o = new JsonObject { ["text"] = text };
            if (sets is not null) o["sets"] = sets;
            if (files is not null) o["files"] = files.DeepClone();
            return o;
        }
        static JsonNode J(string json) => JsonNode.Parse(json)!;
        var savedTheme = new JsonObject { ["Personalização/temas/Meu tema.json"] = "{\"nome\":\"Meu tema\",\"cores\":{}}", ["Personalização/temas/ruim.json"] = "{nope" };
        return
        [
            C(null), C(""), C("   "), C("[]"), C("42"), C("\"texto\""), C("null"),
            C("{\"tema\":\"oceano\",\"2\":1,\"1\":2,\"b\":3,\"a\":4,\"$schema\":\"urbe-tema-1\",\"descrição\":\"x\"}"),
            C("{\"tema\":\"\"}"), C("{\"tema\":5}"), C("{\"tema\":\" meia-noite \"}"), C("{\"tema\":\"naoexiste\"}"),
            C("{\"tema\":\"constructor\"}"), C("{\"tema\":\"Meu tema\"}", files: savedTheme), C("{\"tema\":\"ruim\"}", files: savedTheme),
            C("{\"tema\":\"Personalização/temas/Meu tema.json\"}", files: savedTheme),
            C("{\"cores\":{\"destaque\":12,\"fundo\":true,\"texto\":[1,2],\"linha\":{},\"perigo\":\"#abc\",\"sucesso\":\"#ABCDEF\",\"aviso\":\"red\",\"codigo\":\"\",\"texto2\":null,\"x\":\"#fff\"}}"),
            C("{\"cores\":[1]}"), C("{\"cores\":\"#fff\"}"),
            C("{\"texto\":{\"fonte\":\"Inter\",\"fonteEditor\":\"\",\"tamanho\":\"15\",\"tamanhoEditor\":30,\"alturaLinha\":1.25,\"z\":1}}"),
            C("{\"texto\":{\"fonte\":5,\"tamanho\":\"\"}}"), C("{\"texto\":{\"tamanho\":true}}"), C("{\"texto\":{\"tamanho\":[14]}}"),
            C("{\"texto\":{\"tamanho\":\"0x10\",\"alturaLinha\":2,\"tamanhoEditor\":\"1e1\"}}"), C("{\"texto\":{\"tamanho\":15.5,\"fonte\":\" mono \"}}"),
            C("{\"texto\":{\"tamanho\":\" 13 \",\"tamanhoEditor\":\"Infinity\"}}"), C("{\"texto\":\"x\"}"),
            C("{\"forma\":{\"cantos\":\"retos\",\"densidade\":\"x\",\"vidro\":\"true\"},\"animacoes\":\"ligadas\"}"), C("{\"animacoes\":3}"),
            C("{\"editor\":{\"largura\":\"total\",\"modoInicial\":\"texto\",\"ortografia\":false}}"),
            C("{\"cidade\":{\"moradores\":false,\"fauna\":false,\"clima\":false,\"eventos\":false,\"nomes\":false,\"bairros\":false,\"ambiente\":\"auto\",\"texturas\":\" p.json \"}}"),
            C("{\"cidade\":{\"ambiente\":\"meio-dia\",\"texturas\":3,\"fauna\":\"não\"}}"),
            C("{\"cidade\":{\"paleta\":{\"grama\":\"#123\",\"água\":[\"#111\",\"#222\",\"#333\",\"#444\",\"#555\"],\"foo\":\"#fff\",\"mar\":[],\"neve\":[\"#zzz\"],\"GRASS\":\"#abc\",\"constructor\":\"#fff\",\" Rio \":\"#fff\"}}}"),
            C("{\"cidade\":{\"paleta\":5}}"),
            C("{\"estilos\":{\"Personalização/estilos/a.css\":true,\"b\":\"x\"},\"css\":\"body{}\"}"), C("{\"estilos\":[],\"css\":5}"),
            C("{\"tema\":\"x\",\"tema\":\"vinho\",\"cidade\":{\"fauna\":false},\"cidade\":{\"clima\":false}}"),
            C("{\"texto\":{\"tamanho\":1e400}}"),
            C(null, new JsonArray(J("{\"cidade\":{\"fauna\":false}}"), J("{\"cidade\":{\"ambiente\":\"noite\"}}"), J("{\"tema\":\"oceano\"}"),
                J("{\"cidade\":{\"fauna\":true}}"), J("{\"cores\":{\"destaque\":\"#FFD400\"}}"), J("{\"texto\":{\"tamanho\":\"x\"}}"),
                J("{\"texto\":{\"alturaLinha\":2}}"), J("{\"cidade\":{\"paleta\":{\"grama\":[\"#111\",\"#222\"]}}}"), J("{\"novo\":1}"),
                J("{\"cidade\":{\"ambiente\":\"ciclo\"},\"tema\":\"escuro\"}"))),
            C("{\"tema\":\"claro\",\"cidade\":{\"nomes\":false}}", new JsonArray(J("{\"cidade\":{\"nomes\":true}}"), J("{\"tema\":\"escuro\"}")))
        ];
    }

    [Fact]
    public async Task NormalizeAndTheFileTextMatchTheOriginal()
    {
        var cases = Cases();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "customize-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        await process.StandardInput.WriteAsync(cases.ToJsonString());
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var json = JsonDocument.Parse(await stdout);
        Assert.Equal(cases.Count, json.RootElement.GetArrayLength());

        int i = 0;
        foreach (var expected in json.RootElement.EnumerateArray())
        {
            var c = (JsonObject)cases[i]!;
            var at = $"caso {i++}: {c["text"]?.ToJsonString()}";
            var files = c["files"] as JsonObject;
            bool Saved(string p) => files is not null && files[p] is JsonValue v &&
                v.TryGetValue<string>(out var t) && (Try(() => LegacyCustomize.ParseJs(t)) is JsonObject);
            var text = c["text"] is JsonValue tv ? tv.GetValue<string>() : null;
            var n = LegacyCustomize.Normalize(text, Saved);
            Assert.True(expected.GetProperty("config").GetString() == LegacyCustomize.Stringify(n.Config), at + " config\n" + LegacyCustomize.Stringify(n.Config));
            var errors = expected.GetProperty("errors").EnumerateArray().ToList();
            Assert.True(errors.Count == n.Errors.Count, at + " errors: " + string.Join("; ", n.Errors));
            for (int k = 0; k < errors.Count; k++)
            {
                Assert.Equal(errors[k].GetProperty("path").GetString(), n.Errors[k].Path);
                // V8's JSON.parse messages are engine text; only their prefix is Urbe's
                if (n.Errors[k].Path.Length > 0 || !n.Errors[k].Message.StartsWith("JSON inválido: ", StringComparison.Ordinal))
                    Assert.Equal(errors[k].GetProperty("message").GetString(), n.Errors[k].Message);
            }
            var warnings = expected.GetProperty("warnings").EnumerateArray().ToList();
            Assert.True(warnings.Count == n.Warnings.Count, at + " warnings: " + string.Join("; ", n.Warnings));
            for (int k = 0; k < warnings.Count; k++)
            {
                Assert.Equal(warnings[k].GetProperty("path").GetString(), n.Warnings[k].Path);
                Assert.Equal(warnings[k].GetProperty("message").GetString(), n.Warnings[k].Message);
            }
            var writes = expected.GetProperty("writes").EnumerateArray().ToList();
            if (writes.Count == 0) continue;
            // replace(config), then each set(patch)
            var current = n.Config;
            Assert.True(writes[0].GetProperty("file").GetString() == LegacyCustomize.FileText(current), at + " replace file: " + writes[0].GetProperty("file"));
            var lastFile = LegacyCustomize.FileText(current);
            var sets = (c["sets"] as JsonArray) ?? [];
            for (int k = 0; k < sets.Count; k++)
            {
                var r = LegacyCustomize.Normalize(LegacyCustomize.Merge(current, (JsonObject)sets[k]!), Saved);
                var w = writes[k + 1];
                Assert.Equal(w.GetProperty("errors").GetInt32(), r.Errors.Count);
                if (r.Errors.Count == 0)
                {
                    current = r.Config;
                    var file = LegacyCustomize.FileText(current);
                    // gravar only upserts when the text changed (d.content !== txt)
                    if (w.GetProperty("file").ValueKind == JsonValueKind.Null) Assert.Equal(lastFile, file);
                    else Assert.Equal(w.GetProperty("file").GetString(), file);
                    lastFile = file;
                }
                else Assert.Equal(JsonValueKind.Null, w.GetProperty("file").ValueKind);
                Assert.Equal(w.GetProperty("config").GetString(), LegacyCustomize.Stringify(current));
            }
        }
    }

    private static JsonNode? Try(Func<JsonNode?> f)
    {
        try { return f(); } catch (JsonException) { return null; }
    }
}
