using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Buscar notas, paleta de comandos and atalhos against the unmodified 1.8.4-beta quick-open.js,
/// command-palette.js and keymap.js run by Node (tools/search-oracle.mjs).</summary>
public sealed class LegacySearchOracleTests
{
    private static readonly LegacySearchDoc[] Docs =
    [
        new("1", "Café", "Culinária/Café.md", "# Café\nmuito café aqui e mais café, cafe", "2026-01-02"),
        new("2", "Ideias", "Ideias.md", "# Outra coisa\nideias soltas sobre café com leite e pão de queijo " + new string('x', 200) + " café", "2026-03-01"),
        new("3", "Ação rápida", "Ação rápida.md", "Texto sem cabeçalho.\n\n  várias   linhas\ncom   espaços e AÇÃO", "2026-03-01"),
        new("4", "Plano de viagem", "Viagens/Plano de viagem.md", "## Plano\n- passagem\n- hotel café da manhã", "2025-12-31"),
        new("5", "Ça va", "Ça va.md", "# Ça va\nCA VA?\nİstanbul ΑΣ ΣΑΣ", "2026-02-02"),
        new("6", "ideias", "Pasta/ideias.md", "", "2026-03-01"),
        new("7", "Notas & <tags>", "A/B/Notas & <tags>.md", "# Notas & <tags>\numa \"nota\" com <b>html</b> & mais", null),
        new("8", "Zebra", "Zebra.md", "  #   Zebra  \nlistras", "2024-05-05"),
        new("9", "Árvore", "Árvore.md", "frutas e ÁRVORES; árvore árvore árvore", "2026-03-01"),
        new("10", "Straße", "Straße.md", "Strasse ß ẞ", "2026-03-02")
    ];

    private static readonly string[] Queries =
    [
        "café", "CAFE", "cafe ideias", "ideias", "Ideias", "ação", "acao rapida", "ca va", "ça", "plano viagem", "viagens", "pasta",
        "zebra", "arvore", "árvores", "nota", "<b>", "&", "strasse", "straße", "ΣΑΣ", "istanbul", "x", "  ", "", "inexistente",
        "a/b", "café aqui", "queijo", "linhas espaços", "ideias.md", "Nova.MD", "Nova.markdown", "com: dois"
    ];

    private static async Task<JsonElement> RunAsync(object cases)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "search-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(cases));
        process.StandardInput.Close();
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        return JsonDocument.Parse(await stdout).RootElement.Clone();
    }

    private static object DocJson(LegacySearchDoc d) => new { id = d.Id, title = d.Title, path = d.Path, content = d.Content, modified = d.Modified };

    [Fact]
    public async Task RankingAndTheListMatchTheOriginal()
    {
        var docs = Docs.Select(DocJson).ToArray();
        var recent = new[] { "9", "nope", "4", "9" };
        var cases = new
        {
            rank = Queries.Select(q => new { docs, query = q, limit = (int?)null }).Concat(new[] { new { docs, query = "a", limit = (int?)3 } }).ToArray(),
            view = Queries.Select(q => new { docs, recent, query = q }).ToArray(),
            create = Queries
        };
        var result = await RunAsync(cases);

        var ranks = result.GetProperty("rank").EnumerateArray().ToArray();
        for (var i = 0; i < ranks.Length; i++)
        {
            var (query, limit) = i < Queries.Length ? (Queries[i], 0) : ("a", 3);
            var hits = LegacyQuickOpen.Rank(Docs, query, limit);
            var expected = ranks[i].EnumerateArray().ToArray();
            Assert.True(expected.Length == hits.Count, $"'{query}': {expected.Length} esperados, {hits.Count}");
            for (var k = 0; k < hits.Count; k++)
            {
                Assert.Equal(expected[k].GetProperty("id").GetString(), hits[k].Doc.Id);
                Assert.Equal(expected[k].GetProperty("score").GetInt32(), hits[k].Score);
                var sn = expected[k].GetProperty("snippet");
                if (sn.ValueKind == JsonValueKind.Null) Assert.Null(hits[k].Snippet);
                else
                {
                    Assert.Equal(sn.GetProperty("before").GetString(), hits[k].Snippet!.Before);
                    Assert.Equal(sn.GetProperty("match").GetString(), hits[k].Snippet!.Match);
                    Assert.Equal(sn.GetProperty("after").GetString(), hits[k].Snippet!.After);
                }
            }
        }

        var views = result.GetProperty("view").EnumerateArray().ToArray();
        for (var i = 0; i < Queries.Length; i++)
        {
            var view = LegacyQuickOpen.View(Docs, recent, Queries[i]);
            var expected = views[i];
            Assert.Equal(expected.GetProperty("group").GetBoolean(), view.RecentGroup);
            Assert.Equal(expected.GetProperty("empty").GetBoolean(), view.Empty);
            var rows = expected.GetProperty("rows").EnumerateArray().ToArray();
            Assert.True(rows.Length == view.Rows.Count, $"'{Queries[i]}': {rows.Length} linhas esperadas, {view.Rows.Count}");
            for (var k = 0; k < rows.Length; k++)
            {
                var row = view.Rows[k];
                if (rows[k].TryGetProperty("create", out var create))
                {
                    Assert.Equal(create.GetString(), row.Create);
                    continue;
                }
                Assert.Equal(rows[k].GetProperty("title").GetString(), row.Doc!.Title);
                var sn = rows[k].GetProperty("snippet");
                if (sn.ValueKind == JsonValueKind.Null) Assert.Null(row.Snippet);
                else Assert.Equal((sn.GetProperty("before").GetString(), sn.GetProperty("match").GetString(), sn.GetProperty("after").GetString()),
                    (row.Snippet!.Before, row.Snippet.Match, row.Snippet.After));
                var folder = rows[k].GetProperty("folder");
                if (folder.ValueKind != JsonValueKind.Null) Assert.Equal(folder.GetString(), row.Folder);
            }
        }

        var created = result.GetProperty("create").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(created, Queries.Select(LegacyQuickOpen.CreatePath).ToArray());
    }

    private static readonly LegacyCommand[] Commands =
    [
        new("ui.quickOpen.open", "Buscar notas", "Navegação"), new("ui.commandPalette.open", "Abrir comandos", "Interface"),
        new("ui.explorer.open", "Abrir notas", "Navegação"), new("editor.back", "Voltar", "Navegação", () => false),
        new("a.dup", "Tutorial", "Ajuda"), new("b.dup", "Tutorial", "Ajuda"), new("c.other", "Tutorial", "Outra"),
        new("city.fit", "Enquadrar todas as notas", "Cidade"), new("ui.customize", "Personalização", "Aplicativo"),
        new("workspace.navigate.world", "Abrir cidade", "Navegação"), new("x.upper", "ÁRVORE Nova", "Cidade"),
        new("y.zeta", "Zeta", "Z"), new("y.alfa", "Alfa", "Z"), new("y.alfa2", "alfa", "Z2")
    ];

    [Fact]
    public async Task CommandPaletteMatchesTheOriginal()
    {
        string[] typed = ["", "  ", "abrir", "ABRIR", "nav", "tutorial", "ajuda", "ui.", "city", "árvore", "arvore", "voltar", "zz", "a", "notas", "interface", "z", "alfa", " cidade "];
        var commands = Commands.Select(c => new { id = c.Id, title = c.Title, category = c.Category, enabled = c.IsEnabled }).ToArray();
        var result = await RunAsync(new { palette = typed.Select(q => new { commands, query = q }).ToArray() });
        var expected = result.GetProperty("palette").EnumerateArray().ToArray();
        for (var i = 0; i < typed.Length; i++)
        {
            var ids = LegacyCommandPalette.Filter(Commands, typed[i]).Select(c => c.Id).ToArray();
            Assert.Equal(expected[i].GetProperty("ids").EnumerateArray().Select(e => e.GetString()).ToArray(), ids);
            Assert.Equal(expected[i].GetProperty("empty").GetBoolean(), ids.Length == 0);
        }
    }

    [Fact]
    public async Task KeymapMatchesTheOriginal()
    {
        string[] keys = ["k", "K", "p", "s", "w", "f", "n", "m", "ArrowLeft", "ArrowRight", "\\", "ß", "Enter", "F5", "ç", "Escape"];
        var inputs = new List<(LegacyKeyInput Key, string? Editable)>();
        foreach (var key in keys)
            foreach (var (ctrl, meta, alt, shift) in new[] { (false, false, false, false), (true, false, false, false), (false, true, false, false), (true, false, false, true), (false, false, true, false), (true, false, true, true) })
                foreach (var editable in new string?[] { null, "INPUT", "TEXTAREA", "CE" })
                    inputs.Add((new LegacyKeyInput(key, ctrl, meta, alt, shift), editable));
        var all = LegacyKeymap.List().Select(b => b.CommandId).ToArray();
        var cases = new
        {
            bindings = true,
            keys = inputs.SelectMany(i => new[] { all, [] }.Select(av => new
            {
                @event = new { key = i.Key.Key, ctrlKey = i.Key.Ctrl, metaKey = i.Key.Meta, altKey = i.Key.Alt, shiftKey = i.Key.Shift },
                editable = i.Editable,
                available = av
            })).ToArray()
        };
        var result = await RunAsync(cases);
        var bindings = result.GetProperty("bindings").EnumerateArray().Select(b => (b.GetProperty("shortcut").GetString()!, b.GetProperty("commandId").GetString()!)).ToArray();
        Assert.Equal(bindings, LegacyKeymap.List().Select(b => (b.Shortcut, b.CommandId)).ToArray());
        var expected = result.GetProperty("keys").EnumerateArray().ToArray();
        var n = 0;
        foreach (var (key, editable) in inputs)
            foreach (var available in new[] { true, false })
            {
                var got = LegacyKeymap.Resolve(key, editable is not null, _ => available);
                Assert.Equal(expected[n].GetProperty("executed").GetString(), got.CommandId);
                Assert.Equal(expected[n].GetProperty("prevented").GetBoolean(), got.PreventDefault);
                n++;
            }
        Assert.Equal(expected.Length, n);
    }

    [Fact]
    public void NavigationKeepsTheActiveRowInRange()
    {
        Assert.Equal(1, LegacyQuickOpen.Move(0, 1, 3));
        Assert.Equal(2, LegacyQuickOpen.Move(2, 1, 3));
        Assert.Equal(0, LegacyQuickOpen.Move(0, -1, 3));
        Assert.Equal(0, LegacyQuickOpen.Clamp(5, 0));
        Assert.Equal(1, LegacyQuickOpen.Clamp(5, 2));
    }
}
