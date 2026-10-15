using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Plugins;

/// <summary>
/// A plugin written in the vault (Personalização/plugins/&lt;nome&gt;.plugin.json) for the native plugin API (ADD-0021):
/// a description of its panel, buttons, commands, clock and events, with actions in the small language of
/// <see cref="PluginScript"/>. It declares its permissions; it runs only after the person approves this exact text
/// (SHA-256), and every action is checked against what it declared (deny-by-default, NN-016). No code is loaded.
/// </summary>
public sealed class VaultPluginSpec
{
    public const string Extension = ".plugin.json";
    public const int Format = 1;

    public static readonly string[] Permissions = ["avisos", "botoes", "comandos", "notas.ler", "notas.escrever", "eventos", "paineis", "dados"];
    public static readonly string[] Icons = ["clock", "star", "check", "edit", "search", "file", "folder", "list", "book", "pin", "sparkle", "plus", "layers", "flower", "house"];
    public static readonly string[] Events = ["nota:aberta", "nota:salva"];

    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "sparkle";
    public IReadOnlyList<string> Declared { get; init; } = [];
    public JsonObject State { get; init; } = [];
    public IReadOnlyList<string> Keep { get; init; } = [];
    public JsonArray OnStart { get; init; } = [];
    public JsonObject? Panel { get; init; }
    public JsonArray Buttons { get; init; } = [];
    public JsonArray Commands { get; init; } = [];
    public JsonObject? Clock { get; init; }
    public JsonObject Handlers { get; init; } = [];

    public static bool IsPluginPath(string path) =>
        path.StartsWith(LegacyPlugins.Folder, StringComparison.Ordinal) && path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads and checks a plugin file: the spec, or the problems (path: message) found, without running anything.</summary>
    public static (VaultPluginSpec? Spec, IReadOnlyList<string> Errors) Parse(string path, string text)
    {
        var errors = new List<string>();
        JsonObject o;
        try { o = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject ?? throw new JsonException("o arquivo precisa ser um objeto JSON"); }
        catch (JsonException e) { return (null, ["JSON inválido: " + e.Message]); }

        if (o["urbe-plugin"] is not JsonValue fv || LegacyCustomize.JsToNumber(fv) != Format) errors.Add("urbe-plugin: use 1.");
        var name = Str(o["nome"]).Trim();
        if (name.Length == 0) errors.Add("nome: obrigatório.");
        var declared = (o["permissoes"] as JsonArray ?? []).Select(x => Str(x)).ToList();
        foreach (var p in declared.Where(p => !Permissions.Contains(p))) errors.Add("permissoes: “" + p + "” não existe. Use: " + string.Join(", ", Permissions) + ".");
        var icon = Str(o["icone"]);
        if (icon.Length > 0 && !Icons.Contains(icon)) errors.Add("icone: use um de " + string.Join(", ", Icons) + ".");
        var state = o["estado"] as JsonObject ?? [];
        if (o["estado"] is not null and not JsonObject) errors.Add("estado: deve ser um objeto {variável: valor inicial}.");
        foreach (var (k, _) in state) if (!IsName(k)) errors.Add("estado." + k + ": nome de variável inválido (letras, números e _).");
        var keep = (o["guardar"] as JsonArray ?? []).Select(x => Str(x)).ToList();
        foreach (var k in keep.Where(k => !state.ContainsKey(k))) errors.Add("guardar: “" + k + "” não está em estado.");
        if (keep.Count > 0 && !declared.Contains("dados")) errors.Add("guardar: precisa da permissão “dados”.");

        void Need(string perm, string where) { if (!declared.Contains(perm)) errors.Add(where + ": precisa da permissão “" + perm + "”."); }

        var onStart = o["ao_ligar"] as JsonArray ?? [];
        CheckActions(onStart, "ao_ligar", declared, errors);
        var panel = o["painel"] as JsonObject;
        if (panel is not null)
        {
            Need("paineis", "painel");
            CheckWidgets(panel["itens"] as JsonArray ?? [], "painel.itens", declared, errors);
            if (panel["titulo"] is { } t) CheckTemplate(Str(t), "painel.titulo", errors);
        }
        var buttons = o["botoes"] as JsonArray ?? [];
        if (o["botao"] is JsonObject single) buttons = [single.DeepClone()];
        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i] as JsonObject;
            var where = "botoes[" + i + "]";
            if (b is null) { errors.Add(where + ": deve ser um objeto."); continue; }
            Need("botoes", where);
            if (Str(b["titulo"]).Length == 0) errors.Add(where + ".titulo: obrigatório.");
            var onde = Str(b["onde"]);
            if (onde.Length > 0 && onde is not ("cidade" or "editor")) errors.Add(where + ".onde: use \"cidade\" ou \"editor\".");
            var bi = Str(b["icone"]);
            if (bi.Length > 0 && !Icons.Contains(bi)) errors.Add(where + ".icone: use um de " + string.Join(", ", Icons) + ".");
            if (b["acoes"] is JsonArray ba) CheckActions(ba, where + ".acoes", declared, errors);
            else if (panel is null) errors.Add(where + ".acoes: sem painel, o botão precisa de ações.");
        }
        var commands = o["comandos"] as JsonArray ?? [];
        for (int i = 0; i < commands.Count; i++)
        {
            var where = "comandos[" + i + "]";
            if (commands[i] is not JsonObject c) { errors.Add(where + ": deve ser um objeto."); continue; }
            Need("comandos", where);
            if (Str(c["titulo"]).Length == 0) errors.Add(where + ".titulo: obrigatório.");
            CheckActions(c["acoes"] as JsonArray ?? [], where + ".acoes", declared, errors);
        }
        var clock = o["relogio"] as JsonObject;
        if (clock is not null)
        {
            var ms = clock["intervalo"] is { } iv ? LegacyCustomize.JsToNumber(iv) : 1000;
            if (!(ms >= 100 && ms <= 3_600_000)) errors.Add("relogio.intervalo: entre 100 e 3600000 ms.");
            CheckActions(clock["acoes"] as JsonArray ?? [], "relogio.acoes", declared, errors);
        }
        var handlers = o["eventos"] as JsonObject ?? [];
        foreach (var (k, v) in handlers)
        {
            if (!Events.Contains(k)) { errors.Add("eventos." + k + ": use " + string.Join(" ou ", Events) + "."); continue; }
            Need("eventos", "eventos." + k);
            CheckActions(v as JsonArray ?? [], "eventos." + k, declared, errors);
        }
        if (panel is null && buttons.Count == 0 && commands.Count == 0 && clock is null && handlers.Count == 0 && onStart.Count == 0)
            errors.Add("O plugin não faz nada: dê a ele um painel, um botão, um comando, um relógio ou eventos.");
        foreach (var (k, _) in o)
            if (k is not ("urbe-plugin" or "$schema" or "nome" or "versao" or "descricao" or "icone" or "permissoes" or "estado" or "guardar" or "ao_ligar" or "painel" or "botao" or "botoes" or "comandos" or "relogio" or "eventos"))
                errors.Add(k + ": campo desconhecido.");
        if (errors.Count > 0) return (null, errors);

        var file = path[(path.LastIndexOf('/') + 1)..];
        return (new VaultPluginSpec
        {
            Id = "vault:" + file[..^Extension.Length], Name = name, Version = Str(o["versao"]) is { Length: > 0 } ver ? ver : "1.0", Description = Str(o["descricao"]),
            Icon = icon.Length > 0 ? icon : "sparkle", Declared = declared, State = (JsonObject)state.DeepClone(), Keep = keep, OnStart = (JsonArray)onStart.DeepClone(),
            Panel = (JsonObject?)panel?.DeepClone(), Buttons = (JsonArray)buttons.DeepClone(), Commands = (JsonArray)commands.DeepClone(), Clock = (JsonObject?)clock?.DeepClone(),
            Handlers = (JsonObject)handlers.DeepClone()
        }, []);
    }

    public static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is JsonValue ? LegacyCustomize.JsToString(n) : "";

    private static bool IsName(string k) => k.Length > 0 && (char.IsLetter(k[0]) || k[0] == '_') && k.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static void CheckExpr(string src, string where, List<string> errors)
    {
        try { PluginScript.Parse(src); }
        catch (PluginScriptException e) { errors.Add(where + ": " + e.Message); }
    }

    private static void CheckTemplate(string src, string where, List<string> errors)
    {
        try { PluginScript.CheckTemplate(src); }
        catch (PluginScriptException e) { errors.Add(where + ": " + e.Message); }
    }

    /// <summary>The verbs of the actions and the permission each needs ("" = none).</summary>
    public static readonly IReadOnlyDictionary<string, string> Verbs = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["definir"] = "", ["se"] = "", ["repetir"] = "", ["painel"] = "paineis", ["aviso"] = "avisos", ["escreverNota"] = "notas.escrever",
        ["inserirTexto"] = "notas.escrever", ["abrirNota"] = "notas.ler", ["relogio"] = "", ["parar"] = ""
    };

    private static void CheckActions(JsonArray actions, string where, List<string> declared, List<string> errors)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            var w = where + "[" + i + "]";
            if (actions[i] is not JsonObject a) { errors.Add(w + ": cada ação é um objeto, ex.: {\"definir\": \"x\", \"valor\": \"x + 1\"}."); continue; }
            var verb = a.Select(p => p.Key).FirstOrDefault(k => Verbs.ContainsKey(k));
            if (verb is null) { errors.Add(w + ": ação desconhecida. Use uma de: " + string.Join(", ", Verbs.Keys) + "."); continue; }
            if (Verbs[verb] is { Length: > 0 } perm && !declared.Contains(perm)) errors.Add(w + "." + verb + ": precisa da permissão “" + perm + "”.");
            switch (verb)
            {
                case "definir":
                    if (!IsName(Str(a["definir"]))) errors.Add(w + ".definir: nome de variável.");
                    if (a["valor"] is JsonValue vv && vv.TryGetValue<string>(out var vs)) CheckExpr(vs, w + ".valor", errors);
                    else if (a["valor"] is null) errors.Add(w + ".valor: obrigatório (uma expressão entre aspas, ex.: \"x + 1\").");
                    break;
                case "se":
                    CheckExpr(Str(a["se"]), w + ".se", errors);
                    CheckActions(a["entao"] as JsonArray ?? [], w + ".entao", declared, errors);
                    CheckActions(a["senao"] as JsonArray ?? [], w + ".senao", declared, errors);
                    break;
                case "repetir":
                    CheckExpr(Str(a["repetir"]), w + ".repetir", errors);
                    CheckActions(a["acoes"] as JsonArray ?? [], w + ".acoes", declared, errors);
                    break;
                case "painel":
                    if (Str(a["painel"]) is not ("mostrar" or "ocultar" or "alternar")) errors.Add(w + ".painel: use \"mostrar\", \"ocultar\" ou \"alternar\".");
                    break;
                case "relogio":
                    if (Str(a["relogio"]) is not ("iniciar" or "parar")) errors.Add(w + ".relogio: use \"iniciar\" ou \"parar\".");
                    break;
                case "aviso" or "inserirTexto" or "abrirNota":
                    CheckTemplate(Str(a[verb]), w + "." + verb, errors);
                    break;
                case "escreverNota":
                    CheckTemplate(Str(a["escreverNota"]), w + ".escreverNota", errors);
                    CheckTemplate(Str(a["conteudo"]), w + ".conteudo", errors);
                    if (Str(a["modo"]) is { Length: > 0 } m && m is not ("substituir" or "acrescentar")) errors.Add(w + ".modo: use \"substituir\" ou \"acrescentar\".");
                    break;
            }
        }
    }

    /// <summary>The kinds of the panel's items.</summary>
    public static readonly string[] WidgetKinds = ["texto", "botao", "linha", "campo", "marcar", "lista", "se", "progresso", "separador"];

    private static void CheckWidgets(JsonArray items, string where, List<string> declared, List<string> errors)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var w = where + "[" + i + "]";
            if (items[i] is not JsonObject it) { errors.Add(w + ": cada item é um objeto, ex.: {\"texto\": \"Olá\"}."); continue; }
            var kind = it.Select(p => p.Key).FirstOrDefault(k => WidgetKinds.Contains(k));
            if (kind is null) { errors.Add(w + ": item desconhecido. Use um de: " + string.Join(", ", WidgetKinds) + "."); continue; }
            switch (kind)
            {
                case "texto": CheckTemplate(Str(it["texto"]), w + ".texto", errors); break;
                case "botao":
                    CheckTemplate(Str(it["botao"]), w + ".botao", errors);
                    CheckActions(it["acoes"] as JsonArray ?? [], w + ".acoes", declared, errors);
                    break;
                case "linha": CheckWidgets(it["linha"] as JsonArray ?? [], w + ".linha", declared, errors); break;
                case "campo" or "marcar":
                    if (!IsName(Str(it[kind]))) errors.Add(w + "." + kind + ": nome de uma variável do estado.");
                    break;
                case "lista":
                    CheckExpr(Str(it["lista"]), w + ".lista", errors);
                    CheckTemplate(Str(it["item"]), w + ".item", errors);
                    CheckActions(it["acoes"] as JsonArray ?? [], w + ".acoes", declared, errors);
                    break;
                case "se":
                    CheckExpr(Str(it["se"]), w + ".se", errors);
                    CheckWidgets(it["itens"] as JsonArray ?? [], w + ".itens", declared, errors);
                    CheckWidgets(it["senao"] as JsonArray ?? [], w + ".senao", declared, errors);
                    break;
                case "progresso": CheckExpr(Str(it["progresso"]), w + ".progresso", errors); break;
            }
        }
    }

    /// <summary>The format, the items, the actions and the functions, for the person and the Assistente (plugin_guide).</summary>
    public static string Guide() => string.Join("\n",
    [
        "PLUGINS DO URBE NATIVO (arquivo Personalização/plugins/<nome>.plugin.json)",
        "Um plugin descreve o que aparece (painel, botões, comandos) e o que acontece (ações). Ele só roda depois que a pessoa o liga e só faz o que declarou em \"permissoes\". Mudou o arquivo, precisa ligar de novo.",
        "",
        "{",
        "  \"urbe-plugin\": 1,",
        "  \"nome\": \"Cronômetro\", \"versao\": \"1.0\", \"descricao\": \"...\", \"icone\": \"clock\",",
        "  \"permissoes\": [" + string.Join(", ", Permissions.Select(p => "\"" + p + "\"")) + "],   // só as necessárias",
        "  \"estado\": { \"segundos\": 0, \"rodando\": false, \"voltas\": [] },   // variáveis e valores iniciais",
        "  \"guardar\": [\"voltas\"],                 // variáveis lembradas neste aparelho (precisa de \"dados\")",
        "  \"ao_ligar\": [ações],",
        "  \"painel\": { \"titulo\": \"Cronômetro\", \"aberto\": false, \"itens\": [itens] },   // painel flutuante sobre a cidade e o editor (\"paineis\")",
        "  \"botoes\": [ { \"titulo\": \"Cronômetro\", \"icone\": \"clock\", \"onde\": \"cidade\" | \"editor\", \"acoes\": [ações] } ],   // sem acoes: abre/fecha o painel (\"botoes\")",
        "  \"comandos\": [ { \"titulo\": \"Zerar cronômetro\", \"acoes\": [ações] } ],   // na paleta de comandos (\"comandos\")",
        "  \"relogio\": { \"intervalo\": 1000, \"ativo\": false, \"acoes\": [ações] },   // repete as ações a cada intervalo (ms) enquanto ativo",
        "  \"eventos\": { \"nota:aberta\": [ações], \"nota:salva\": [ações] }   // a variável nota tem o caminho (\"eventos\")",
        "}",
        "Ícones: " + string.Join(", ", Icons) + ".",
        "",
        "ITENS DO PAINEL",
        "  {\"texto\": \"Tempo: {formatarTempo(segundos)}\", \"tamanho\": \"pequeno\"|\"normal\"|\"grande\"|\"enorme\", \"negrito\": true, \"mono\": true}",
        "  {\"botao\": \"Iniciar\", \"acoes\": [...], \"destaque\": true}",
        "  {\"linha\": [itens]}                        itens lado a lado",
        "  {\"campo\": \"variavel\", \"rotulo\": \"Minutos\", \"tipo\": \"texto\"|\"numero\", \"dica\": \"...\"}   a pessoa digita e a variável muda",
        "  {\"marcar\": \"variavel\", \"rotulo\": \"Som ligado\"}         caixa de marcar ligada a uma variável",
        "  {\"lista\": \"voltas\", \"item\": \"{indice + 1}. {item}\", \"acoes\": [...]}   um texto por item (variáveis item e indice); acoes ao tocar",
        "  {\"se\": \"rodando\", \"itens\": [itens], \"senao\": [itens]}",
        "  {\"progresso\": \"segundos / 60\"}            barra de 0 a 1",
        "  {\"separador\": true}",
        "Textos aceitam {expressão} no meio; use {{ e }} para chaves literais.",
        "",
        "AÇÕES (cada uma é um objeto; rodam em ordem)",
        "  {\"definir\": \"segundos\", \"valor\": \"segundos + 1\"}      valor é uma expressão (texto entre aspas dentro dela: \"'olá'\")",
        "  {\"se\": \"segundos >= 60\", \"entao\": [ações], \"senao\": [ações]}",
        "  {\"repetir\": \"3\", \"acoes\": [ações]}                     até 1000 vezes; variável i",
        "  {\"relogio\": \"iniciar\" | \"parar\"}",
        "  {\"painel\": \"mostrar\" | \"ocultar\" | \"alternar\"}         (\"paineis\")",
        "  {\"aviso\": \"Tempo esgotado: {formatarTempo(segundos)}\"}     aviso sobre a cidade (\"avisos\")",
        "  {\"escreverNota\": \"Diário/{data()}.md\", \"conteudo\": \"- {hora()} pausa\", \"modo\": \"acrescentar\"|\"substituir\"}   (\"notas.escrever\"; nunca em .urbe nem em plugins)",
        "  {\"inserirTexto\": \"⏱ {formatarTempo(segundos)}\"}          insere no cursor da nota aberta no editor (\"notas.escrever\")",
        "  {\"abrirNota\": \"Diário/{data()}.md\"}                      abre no editor (\"notas.ler\")",
        "  {\"parar\": true}                                          para as ações restantes",
        "",
        "EXPRESSÕES: números, \"textos\" ou 'textos', verdadeiro/falso, nulo, [listas], variáveis do estado, + - * / %, == != < <= > >=, e/&&, ou/||, nao/!, condição ? a : b, lista[i].",
        "Variáveis prontas: notaAberta (caminho da nota no editor ou \"\"), nota (nos eventos), item/indice (em lista), i (em repetir).",
        "FUNÇÕES:",
        string.Join("\n", PluginScript.Functions.Select(f => "  " + f.Value.Signature + (f.Value.Help.Length > 0 ? " — " + f.Value.Help : ""))),
        "",
        "EXEMPLO (cronômetro com botão no editor):",
        ExampleTimer
    ]);

    public const string ExampleTimer = """
{
  "urbe-plugin": 1,
  "nome": "Cronômetro",
  "descricao": "Cronômetro com voltas, aberto por um botão no editor.",
  "icone": "clock",
  "permissoes": ["paineis", "botoes", "notas.escrever", "dados"],
  "estado": { "segundos": 0, "rodando": false, "voltas": [] },
  "guardar": ["voltas"],
  "relogio": { "intervalo": 1000, "acoes": [ { "definir": "segundos", "valor": "segundos + 1" } ] },
  "painel": {
    "titulo": "Cronômetro",
    "itens": [
      { "texto": "{formatarTempo(segundos)}", "tamanho": "enorme", "mono": true },
      { "linha": [
        { "botao": "{rodando ? 'Pausar' : 'Iniciar'}", "destaque": true, "acoes": [
          { "definir": "rodando", "valor": "!rodando" },
          { "se": "rodando", "entao": [ { "relogio": "iniciar" } ], "senao": [ { "relogio": "parar" } ] } ] },
        { "botao": "Volta", "acoes": [ { "definir": "voltas", "valor": "adicionar(voltas, formatarTempo(segundos))" } ] },
        { "botao": "Zerar", "acoes": [ { "relogio": "parar" }, { "definir": "rodando", "valor": "falso" }, { "definir": "segundos", "valor": "0" }, { "definir": "voltas", "valor": "[]" } ] }
      ] },
      { "lista": "voltas", "item": "Volta {indice + 1}: {item}" },
      { "botao": "Inserir na nota", "acoes": [ { "inserirTexto": "⏱ {formatarTempo(segundos)}" } ] }
    ]
  },
  "botoes": [ { "titulo": "Cronômetro", "icone": "clock", "onde": "editor" } ]
}
""";
}

/// <summary>What a running vault plugin asks of the app (each call is behind a permission the runtime already checked).</summary>
public interface IVaultPluginHost : IPluginScriptHost
{
    void Notice(string text);
    void Panel(string how);
    Task WriteNoteAsync(string path, string content, bool append);
    void InsertText(string text);
    void OpenNote(string path);
    string? OpenNotePath { get; }
    string? LoadData(string key);
    void SaveData(string key, string json);
}

/// <summary>
/// The running state of a vault plugin: its variables, its clock, and the actions run against the host with the
/// permissions it declared. Every run has a step budget; a failing action stops its list and is reported.
/// </summary>
public sealed class VaultPluginRuntime
{
    private readonly VaultPluginSpec _spec;
    private readonly IVaultPluginHost _host;
    private readonly Dictionary<string, object?> _vars = new(StringComparer.Ordinal);

    public VaultPluginRuntime(VaultPluginSpec spec, IVaultPluginHost host)
    {
        _spec = spec;
        _host = host;
        foreach (var (k, v) in spec.State) _vars[k] = PluginScript.FromJson(v);
        foreach (var k in spec.Keep)
        {
            if (host.LoadData(k) is not { } saved) continue;
            try { _vars[k] = PluginScript.FromJson(JsonNode.Parse(saved)); }
            catch (JsonException) { }
        }
        ClockOn = spec.Clock is { } c && PluginScript.Truthy(PluginScript.FromJson(c["ativo"]));
        PanelOpen = spec.Panel is { } p && PluginScript.Truthy(PluginScript.FromJson(p["aberto"]));
    }

    public VaultPluginSpec Spec => _spec;
    public IReadOnlyDictionary<string, object?> Vars => _vars;
    public bool ClockOn { get; private set; }
    public bool PanelOpen { get; set; }
    public int ClockInterval => _spec.Clock is { } c && c["intervalo"] is { } iv ? (int)LegacyCustomize.JsToNumber(iv) : 1000;

    /// <summary>Something changed the state (the interface redraws).</summary>
    public event Action? Changed;

    /// <summary>The last error of a run (also told to the host as a notice when the plugin may show notices).</summary>
    public string? LastError { get; private set; }

    private bool Allowed(string permission) => _spec.Declared.Contains(permission);

    public PluginScript.Scope Scope(IDictionary<string, object?>? extra = null)
    {
        var vars = new Dictionary<string, object?>(_vars, StringComparer.Ordinal) { ["notaAberta"] = _host.OpenNotePath ?? "" };
        if (extra is not null) foreach (var (k, v) in extra) vars[k] = v;
        return new PluginScript.Scope(vars, _host, Allowed);
    }

    /// <summary>A text of the interface with its {expressions}; an error shows in the text instead of breaking the panel.</summary>
    public string Text(string template, IDictionary<string, object?>? extra = null)
    {
        try { return PluginScript.Template(template, Scope(extra)); }
        catch (PluginScriptException e) { return "⚠ " + e.Message; }
    }

    public object? Value(string expr, IDictionary<string, object?>? extra = null)
    {
        try { return PluginScript.Eval(expr, Scope(extra)); }
        catch (PluginScriptException) { return null; }
    }

    /// <summary>A field of the panel changed a variable.</summary>
    public void Set(string name, object? value)
    {
        if (!_vars.ContainsKey(name)) return;
        _vars[name] = value;
        Persist(name);
        Changed?.Invoke();
    }

    public Task StartAsync() => RunAsync(_spec.OnStart);
    public Task TickAsync() => RunAsync(_spec.Clock?["acoes"] as JsonArray ?? []);
    public Task EventAsync(string evt, string path) =>
        _spec.Handlers[evt] is JsonArray a ? RunAsync(a, new Dictionary<string, object?> { ["nota"] = path }) : Task.CompletedTask;

    /// <summary>Runs a list of actions (a button, a command, the clock...). Errors are reported, never thrown.</summary>
    public async Task RunAsync(JsonArray actions, IDictionary<string, object?>? extra = null)
    {
        var scope = Scope(extra);
        try
        {
            await RunList(actions, scope);
            LastError = null;
        }
        catch (PluginScriptException e) { Report(e.Message); }
        catch (InvalidOperationException e) { Report(e.Message); }
        catch (UnauthorizedAccessException e) { Report(e.Message); }
        finally
        {
            // the variables of the plugin's state that the actions changed
            foreach (var k in _vars.Keys.ToList())
                if (scope.Vars.TryGetValue(k, out var v) && Json(v) != Json(_vars[k]))
                {
                    _vars[k] = v;
                    Persist(k);
                }
            Changed?.Invoke();
        }
    }

    private void Report(string message)
    {
        LastError = message;
        if (Allowed("avisos")) _host.Notice(_spec.Name + ": " + message);
    }

    private void Persist(string name)
    {
        if (_spec.Keep.Contains(name) && Allowed("dados")) _host.SaveData(name, PluginScript.ToJson(_vars[name])?.ToJsonString() ?? "null");
    }

    private static string Json(object? v) => PluginScript.ToJson(v)?.ToJsonString() ?? "null";

    private async Task<bool> RunList(JsonArray actions, PluginScript.Scope s)
    {
        foreach (var node in actions)
        {
            if (node is not JsonObject a) continue;
            s.Step();
            var verb = a.Select(p => p.Key).FirstOrDefault(k => VaultPluginSpec.Verbs.ContainsKey(k));
            if (verb is null) continue;
            if (VaultPluginSpec.Verbs[verb] is { Length: > 0 } perm && !Allowed(perm))
                throw new PluginScriptException("não declarou a permissão “" + perm + "”.");
            switch (verb)
            {
                case "definir":
                {
                    var name = VaultPluginSpec.Str(a["definir"]);
                    var value = a["valor"] is JsonValue vv && vv.TryGetValue<string>(out var src) ? PluginScript.Eval(src, s) : PluginScript.FromJson(a["valor"]);
                    if (!s.Vars.ContainsKey(name)) throw new PluginScriptException("Variável desconhecida: " + name + ". Declare-a em \"estado\".");
                    s.Vars[name] = value;
                    break;
                }
                case "se":
                    if (!await RunList(PluginScript.Truthy(PluginScript.Eval(VaultPluginSpec.Str(a["se"]), s)) ? a["entao"] as JsonArray ?? [] : a["senao"] as JsonArray ?? [], s)) return false;
                    break;
                case "repetir":
                {
                    var n = (int)Math.Clamp(PluginScript.Num(PluginScript.Eval(VaultPluginSpec.Str(a["repetir"]), s)), 0, 1000);
                    var body = a["acoes"] as JsonArray ?? [];
                    var had = s.Vars.TryGetValue("i", out var before);
                    for (int i = 0; i < n; i++)
                    {
                        s.Vars["i"] = (double)i;
                        if (!await RunList(body, s)) return false;
                    }
                    if (had) s.Vars["i"] = before; else s.Vars.Remove("i");
                    break;
                }
                case "relogio":
                    ClockOn = VaultPluginSpec.Str(a["relogio"]) == "iniciar";
                    break;
                case "parar":
                    return false;
                case "painel":
                {
                    var how = VaultPluginSpec.Str(a["painel"]);
                    PanelOpen = how switch { "mostrar" => true, "ocultar" => false, _ => !PanelOpen };
                    _host.Panel(how);
                    break;
                }
                case "aviso":
                    _host.Notice(PluginScript.Template(VaultPluginSpec.Str(a["aviso"]), s));
                    break;
                case "escreverNota":
                {
                    var path = PluginScript.Template(VaultPluginSpec.Str(a["escreverNota"]), s).Trim();
                    var content = PluginScript.Template(VaultPluginSpec.Str(a["conteudo"]), s);
                    await _host.WriteNoteAsync(path, content, VaultPluginSpec.Str(a["modo"]) == "acrescentar");
                    break;
                }
                case "inserirTexto":
                    _host.InsertText(PluginScript.Template(VaultPluginSpec.Str(a["inserirTexto"]), s));
                    break;
                case "abrirNota":
                    _host.OpenNote(PluginScript.Template(VaultPluginSpec.Str(a["abrirNota"]), s).Trim());
                    break;
            }
        }
        return true;
    }
}
