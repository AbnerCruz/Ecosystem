using System.Text.Json.Nodes;
using Urbe.Core.Plugins;

namespace Urbe.Core.Tests;

/// <summary>The vault plugins of the native API: the expression language, the checks of a plugin file and its runtime.</summary>
public sealed class VaultPluginTests
{
    private sealed class Host : IVaultPluginHost
    {
        public Dictionary<string, string> Files { get; } = new() { ["Diário/2026-10-15.md"] = "# Hoje\n" };
        public Dictionary<string, string> Data { get; } = [];
        public List<string> Log { get; } = [];
        public DateTime Now { get; set; } = new(2026, 10, 15, 9, 30, 5);
        public string? OpenNotePath { get; set; } = "Comece.md";
        public IReadOnlyList<string> Notes(string? folder) => Files.Keys.Where(k => folder is null || k.StartsWith(folder + "/", StringComparison.Ordinal)).Order().ToList();
        public string? ReadNote(string path) => Files.GetValueOrDefault(path);
        public void Notice(string text) => Log.Add("aviso " + text);
        public void Panel(string how) => Log.Add("painel " + how);
        public Task WriteNoteAsync(string path, string content, bool append)
        {
            Files[path] = append && Files.TryGetValue(path, out var old) ? old + (old.EndsWith('\n') ? "" : "\n") + content : content;
            Log.Add("escrever " + path);
            return Task.CompletedTask;
        }
        public void InsertText(string text) => Log.Add("inserir " + text);
        public void OpenNote(string path) => Log.Add("abrir " + path);
        public string? LoadData(string key) => Data.GetValueOrDefault(key);
        public void SaveData(string key, string json) => Data[key] = json;
    }

    private static object? Eval(string src, Dictionary<string, object?>? vars = null, Func<string, bool>? allowed = null) =>
        PluginScript.Eval(src, new PluginScript.Scope(vars ?? [], new Host(), allowed ?? (_ => true)));

    [Fact]
    public void TheExpressionsCompute()
    {
        Assert.Equal(7d, Eval("1 + 2 * 3"));
        Assert.Equal(9d, Eval("(1 + 2) * 3"));
        Assert.Equal("a1", Eval("'a' + 1"));
        Assert.Equal(true, Eval("2 > 1 e nao (3 < 1)"));
        Assert.Equal("sim", Eval("x >= 10 ? 'sim' : 'não'", new() { ["x"] = 12d }));
        Assert.Equal("01:05", Eval("formatarTempo(65)"));
        Assert.Equal("1:01:01", Eval("formatarTempo(3661)"));
        Assert.Equal("09:30:05", Eval("hora()"));
        Assert.Equal("2026-10-15", Eval("data()"));
        Assert.Equal(2d, Eval("tamanho(adicionar(l, 'b'))", new() { ["l"] = new List<object?> { "a" } }));
        Assert.Equal("b", Eval("[\"a\", \"b\"][1]"));
        Assert.Equal("a, b", Eval("juntar(['a', 'b'], ', ')"));
        Assert.Equal(3d, Eval("palavras('um dois três')"));
        Assert.Equal(1d, Eval("7 % 3"));
        Assert.Equal(0d, Eval("1 / 0"));
        Assert.Equal(new List<object?> { "Diário/2026-10-15.md" }, Eval("notas('Diário')"));
        var e = Assert.Throws<PluginScriptException>(() => Eval("notas()", allowed: p => p != "notas.ler"));
        Assert.Contains("notas.ler", e.Message);
        Assert.Contains("Variável desconhecida", Assert.Throws<PluginScriptException>(() => Eval("y + 1")).Message);
        Assert.Contains("Função desconhecida", Assert.Throws<PluginScriptException>(() => PluginScript.Parse("apagarTudo()")).Message);
        Assert.Throws<PluginScriptException>(() => PluginScript.Parse("1 +"));
        Assert.Equal("Tempo 01:00 {ok}", PluginScript.Template("Tempo {formatarTempo(s)} {{ok}}", new PluginScript.Scope(new Dictionary<string, object?> { ["s"] = 60d }, new Host(), _ => true)));
    }

    [Fact]
    public async Task TheGuideExampleIsAValidPluginAndRuns()
    {
        var (spec, errors) = VaultPluginSpec.Parse("Personalização/plugins/Cronômetro.plugin.json", VaultPluginSpec.ExampleTimer);
        Assert.Empty(errors);
        Assert.Equal(("vault:Cronômetro", "Cronômetro"), (spec!.Id, spec.Name));
        var host = new Host();
        var rt = new VaultPluginRuntime(spec, host);
        Assert.False(rt.ClockOn);
        var items = spec.Panel!["itens"]!.AsArray();
        var row = items[1]!["linha"]!.AsArray();
        Assert.Equal("Iniciar", rt.Text((string)row[0]!["botao"]!));
        await rt.RunAsync(row[0]!["acoes"]!.AsArray());
        Assert.True(rt.ClockOn);
        Assert.Equal("Pausar", rt.Text((string)row[0]!["botao"]!));
        for (int i = 0; i < 65; i++) await rt.TickAsync();
        Assert.Equal("01:05", rt.Text((string)items[0]!["texto"]!));
        await rt.RunAsync(row[1]!["acoes"]!.AsArray());
        Assert.Equal("[\"01:05\"]", host.Data["voltas"]);   // kept on the device
        await rt.RunAsync(items[3]!["acoes"]!.AsArray());
        Assert.Contains("inserir ⏱ 01:05", host.Log);
        // a new start remembers the laps
        var again = new VaultPluginRuntime(spec, host);
        Assert.Equal(new List<object?> { "01:05" }, again.Vars["voltas"]);
        Assert.Equal(0d, again.Vars["segundos"]);
    }

    [Fact]
    public void ABadPluginIsExplainedAndNothingRuns()
    {
        var (spec, errors) = VaultPluginSpec.Parse("Personalização/plugins/X.plugin.json", """
            { "urbe-plugin": 1, "nome": "X", "permissoes": ["internet"],
              "estado": { "n": 0 },
              "painel": { "itens": [ { "texto": "{n +}" }, { "botao": "Apagar", "acoes": [ { "escreverNota": "a.md", "conteudo": "x" } ] } ] },
              "eventos": { "app:fechar": [] }, "extra": 1 }
            """);
        Assert.Null(spec);
        Assert.Contains(errors, e => e.StartsWith("permissoes: “internet” não existe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("painel: precisa da permissão “paineis”", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("painel.itens[0].texto:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("painel.itens[1].acoes[0].escreverNota: precisa da permissão “notas.escrever”", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("eventos.app:fechar", StringComparison.Ordinal));
        Assert.Contains("extra: campo desconhecido.", errors);
        Assert.StartsWith("JSON inválido", VaultPluginSpec.Parse("Personalização/plugins/Y.plugin.json", "{").Errors[0]);
    }

    [Fact]
    public async Task ActionsWriteNotesReportErrorsAndStopRunaways()
    {
        var (spec, errors) = VaultPluginSpec.Parse("Personalização/plugins/Diario.plugin.json", """
            { "urbe-plugin": 1, "nome": "Diário", "permissoes": ["comandos", "notas.escrever", "avisos", "eventos"],
              "estado": { "n": 0 },
              "comandos": [ { "titulo": "Anotar", "acoes": [
                 { "escreverNota": "Diário/{data()}.md", "conteudo": "- {hora()} anotado", "modo": "acrescentar" },
                 { "repetir": "5", "acoes": [ { "definir": "n", "valor": "n + i" } ] },
                 { "se": "n == 10", "entao": [ { "aviso": "n = {n}" }, { "parar": true } ] },
                 { "aviso": "nunca" } ] },
                 { "titulo": "Errar", "acoes": [ { "definir": "n", "valor": "n / zzz" } ] } ],
              "eventos": { "nota:salva": [ { "aviso": "salva {nota}" } ] } }
            """);
        Assert.Empty(errors);
        var host = new Host();
        var rt = new VaultPluginRuntime(spec!, host);
        await rt.RunAsync(spec!.Commands[0]!["acoes"]!.AsArray());
        Assert.Equal("# Hoje\n- 09:30:05 anotado", host.Files["Diário/2026-10-15.md"]);
        Assert.Equal(10d, rt.Vars["n"]);
        Assert.Equal(["escrever Diário/2026-10-15.md", "aviso n = 10"], host.Log);
        await rt.RunAsync(spec.Commands[1]!["acoes"]!.AsArray());
        Assert.StartsWith("Variável desconhecida: zzz", rt.LastError);
        Assert.Equal("aviso Diário: Variável desconhecida: zzz. Declare-a em \"estado\".", host.Log[^1]);
        Assert.Equal(10d, rt.Vars["n"]);   // the failed action changed nothing
        await rt.EventAsync("nota:salva", "Comece.md");
        Assert.Equal("aviso salva Comece.md", host.Log[^1]);

        // a runaway: 1000 × 1000 is cut by the step budget, reported, and the app goes on
        var (loop, _) = VaultPluginSpec.Parse("Personalização/plugins/L.plugin.json", """
            { "urbe-plugin": 1, "nome": "L", "permissoes": ["comandos", "avisos"], "estado": { "n": 0 },
              "comandos": [ { "titulo": "Laço", "acoes": [ { "repetir": "1000", "acoes": [ { "repetir": "1000", "acoes": [ { "definir": "n", "valor": "n + 1" } ] } ] } ] } ] }
            """);
        var lrt = new VaultPluginRuntime(loop!, host);
        await lrt.RunAsync(loop!.Commands[0]!["acoes"]!.AsArray());
        Assert.Contains("limite de operações", lrt.LastError);
    }

    [Fact]
    public void PluginPathsAreOnlyThoseInThePluginsFolder()
    {
        Assert.True(VaultPluginSpec.IsPluginPath("Personalização/plugins/Relógio.plugin.json"));
        Assert.False(VaultPluginSpec.IsPluginPath("Personalização/plugins/velho.js"));
        Assert.False(VaultPluginSpec.IsPluginPath("Notas/a.plugin.json"));
        Assert.Contains("\"urbe-plugin\": 1", VaultPluginSpec.Guide());
        Assert.NotNull(JsonNode.Parse(VaultPluginSpec.ExampleTimer));
    }
}
