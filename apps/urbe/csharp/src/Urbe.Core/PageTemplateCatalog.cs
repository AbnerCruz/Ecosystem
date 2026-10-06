using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed record PageTemplateDescriptor(
    string Id,
    string Name,
    string Icon,
    string? Needs = null);

public sealed class PageTemplateContext
{
    public string? Title { get; init; }
    public string? Folder { get; init; }
    public string? NoteTitle { get; init; }
    public string? NotePath { get; init; }
    public string? Excerpt { get; init; }
    public int? Year { get; init; }
}

/// <summary>
/// Built-in page templates and pure variants. This is UI-free and mirrors the
/// semantic catalog of pages/templates.js; Studio concerns remain in UC-18.
/// </summary>
public static class PageTemplateCatalog
{
    private static readonly PageTemplateDescriptor[] Items =
    [
        new("empty", "Vazio", "○"),
        new("canvas", "Tela livre", "⊞"),
        new("blank", "Simples", "＋"),
        new("landing", "Landing de produto", "🚀"),
        new("portfolio", "Portfólio", "🎨"),
        new("article", "Artigo de uma nota", "📄", "note"),
        new("vault-site", "Site de uma pasta", "🗂️", "folder"),
        new("resume", "Currículo", "💼"),
        new("event", "Evento", "🎟️"),
        new("docs", "Documentação", "📚", "folder"),
        new("book", "Livro", "📖"),
        new("book-folder", "Livro de uma pasta", "📚", "folder"),
        new("links", "Links (bio)", "🔗")
    ];

    public static IReadOnlyList<PageTemplateDescriptor> List() =>
        Array.AsReadOnly(Items);

    public static PageTemplateDescriptor? Get(string id) =>
        Items.FirstOrDefault(item => item.Id == id);

    public static PageDocument Build(
        string id,
        PageTemplateContext? context = null)
    {
        context ??= new PageTemplateContext();
        var year = context.Year ?? DateTime.UtcNow.Year;
        JsonObject raw = id switch
        {
            "empty" => Page(
                context.Title ?? "Nova página",
                "✦",
                Theme("grafite", ("animations", false), ("shadow", "none")),
                Layout(("nav", false), ("footer", ""), ("themeToggle", false), ("backToTop", false), ("progress", false)),
                []),
            "canvas" => Page(
                context.Title ?? "Minha página",
                "⊞",
                Theme("aurora"),
                Layout(("nav", false), ("footer", ""), ("themeToggle", false)),
                [FreeCanvas(context.Title ?? "Minha página")]),
            "blank" => Page(
                context.Title ?? "Nova página",
                "✦",
                Theme("papel", ("animations", false)),
                Layout(("nav", false), ("footer", "© " + year + " " + (context.Title ?? "Nova página")), ("themeToggle", false)),
                [
                    Section("hero", Obj(("title", context.Title ?? "Nova página"), ("subtitle", "Uma frase curta sobre o que é esta página."), ("height", "auto"), ("layout", "left"), ("buttons", new JsonArray()))),
                    Section("text", Obj(("markdown", "Escreva aqui. Toque em qualquer parte para editar.")))
                ]),
            "landing" => Landing(context, year),
            "portfolio" => Portfolio(context, year),
            "article" => Page(
                context.NoteTitle ?? context.Title ?? "Artigo",
                "✎",
                Theme("papel", ("width", 1000)),
                Layout(("nav", true), ("brand", context.NoteTitle ?? context.Title ?? "Artigo"), ("progress", true), ("footer", "Publicado com Urbe")),
                [Section("note", Obj(("path", context.NotePath ?? ""), ("showTitle", true), ("showMeta", true)))]),
            "vault-site" => FolderSite(context),
            "resume" => Resume(context, year),
            "event" => Event(context, year),
            "docs" => Docs(context),
            "book" => Book(context, year, false),
            "book-folder" => Book(context, year, true),
            "links" => Links(context),
            _ => throw new ArgumentException("Modelo desconhecido: " + id, nameof(id))
        };

        var normalized = PageNormalizer.Normalize(PageDocument.Parse(raw));
        if (!normalized.IsValid)
        {
            throw new InvalidOperationException(
                "Modelo interno inválido: " + id + " — " +
                string.Join("; ", normalized.Errors.Select(e => e.Path + ": " + e.Message)));
        }
        return normalized.Page;
    }

    public static PageDocument Variant(PageDocument source, string? mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(mode) || mode == "full")
            return PageDocument.Parse(source.Raw);
        if (mode is not "simple" and not "skeleton")
            throw new ArgumentException("Variação desconhecida: " + mode, nameof(mode));

        var raw = source.Raw;
        if (raw["sections"] is not JsonArray sections)
            return PageDocument.Parse(raw);

        if (mode == "simple")
        {
            var isBook = PageDocument.StringValue(raw["layout"]?["format"]) == "book";
            var optional = new HashSet<string>(
                ["copyright", "dedication", "about", "colophon", "part"],
                StringComparer.Ordinal);
            var keep = sections
                .OfType<JsonObject>()
                .Where(section =>
                    !isBook ||
                    !optional.Contains(PageDocument.StringValue(section["type"]) ?? string.Empty))
                .Take(isBook ? 6 : 4)
                .Select(section => (JsonObject)section.DeepClone())
                .ToArray();
            var next = new JsonArray();
            foreach (var section in keep)
            {
                if (section["props"] is JsonObject props)
                    TruncateLists(props);
                next.Add(section);
            }
            raw["sections"] = next;
            if (raw["layout"] is JsonObject layout)
            {
                layout["navCta"] = "";
                layout["navCtaUrl"] = "";
                layout["progress"] = false;
            }
        }
        else
        {
            foreach (var section in sections.OfType<JsonObject>())
            {
                if (section["props"] is JsonObject props)
                    Skeletonize(props);
            }
        }

        return PageNormalizer.Normalize(PageDocument.Parse(raw)).Page;
    }

    private static JsonObject Landing(PageTemplateContext c, int year)
    {
        var title = c.Title ?? "Nome do produto";

        var heroButtons = Buttons(
            ("Começar grátis", "#planos", "primary"),
            ("Ver como funciona", "#recursos", "secondary"));
        var stats = List(
            Obj(("value", "10k+"), ("label", "pessoas usando")),
            Obj(("value", "4,9★"), ("label", "avaliação média")));
        var features = List(
            Obj(("icon", "⚡"), ("title", "Rápido"), ("text", "Resposta instantânea.")),
            Obj(("icon", "🔒"), ("title", "Privado"), ("text", "Seus dados ficam com você.")),
            Obj(("icon", "🎨"), ("title", "Bonito"), ("text", "Visual personalizável.")));
        var testimonials = List(
            Obj(("text", "Economizei horas."), ("author", "Marina")),
            Obj(("text", "Simples e funciona."), ("author", "João")));
        var pricing = List(
            Obj(
                ("name", "Grátis"),
                ("price", "R$ 0"),
                ("features", "Essencial"),
                ("buttonLabel", "Começar"),
                ("url", "#")),
            Obj(
                ("name", "Pro"),
                ("price", "R$ 29"),
                ("features", "Tudo"),
                ("buttonLabel", "Assinar"),
                ("url", "#"),
                ("highlight", true)));
        var faq = List(Obj(("q", "Posso cancelar?"), ("a", "Sim.")));

        return Page(
            title,
            "🚀",
            Theme("aurora", ("background", "mesh")),
            Layout(
                ("brand", title),
                ("navCta", "Começar"),
                ("navCtaUrl", "#planos"),
                ("footer", "© " + year + " " + title + " · Feito com Urbe")),
            [
                Section(
                    "hero",
                    Obj(
                        ("eyebrow", "Novo · versão 1.0"),
                        ("title", "O jeito mais simples de fazer **o que importa**"),
                        ("subtitle", "Explique em uma frase o que o produto resolve e para quem ele é."),
                        ("layout", "center"),
                        ("height", "tall"),
                        ("buttons", heroButtons))),
                Section("stats", Obj(("items", stats))),
                Section(
                    "features",
                    Obj(("title", "Tudo o que você precisa"), ("columns", 3), ("items", features))),
                Section(
                    "testimonials",
                    Obj(("title", "Quem usa, recomenda"), ("items", testimonials))),
                Section(
                    "pricing",
                    Obj(("title", "Planos simples"), ("items", pricing))),
                Section(
                    "faq",
                    Obj(("title", "Perguntas frequentes"), ("items", faq))),
                Section(
                    "cta",
                    Obj(
                        ("title", "Pronto para começar?"),
                        ("text", "Leva menos de um minuto."),
                        ("buttons", Buttons(("Criar conta grátis", "#", "primary")))))
            ]);
    }

    private static JsonObject Portfolio(PageTemplateContext c, int year)
    {
        var title = c.Title ?? "Seu Nome";
        return Page(title + " — Portfólio", "◆", Theme("grafite", ("background", "grid")),
            Layout(("brand", title), ("footer", "© " + year + " " + title)),
            [
                Section("hero", Obj(("eyebrow", "Disponível para projetos"), ("title", "Olá, eu sou " + title + "."), ("subtitle", "Designer e desenvolvedor."), ("layout", "left"), ("height", "tall"), ("buttons", Buttons(("Ver projetos", "#projetos", "primary"), ("Contato", "#contato", "secondary"))))),
                Section("cards", Obj(("title", "Projetos selecionados"), ("items", List(Obj(("title", "Projeto A"), ("text", "Descrição."), ("tag", "Produto")), Obj(("title", "Projeto B"), ("text", "Descrição."), ("tag", "Web")))))),
                Section("timeline", Obj(("title", "Trajetória"), ("items", List(Obj(("date", year.ToString()), ("title", "Hoje"), ("text", "Projetos atuais.")))))),
                Section("contact", Obj(("title", "Vamos criar algo juntos?"), ("email", "voce@exemplo.com")))
            ]);
    }

    private static JsonObject FolderSite(PageTemplateContext c)
    {
        var folder = c.Folder ?? "";
        var title = c.Title ?? (folder.Length > 0 ? folder.Split('/').Last() : "Minhas notas");
        return Page(title, "🗂️", Theme("lavanda"), Layout(("brand", title), ("progress", true)),
            [
                Section("hero", Obj(("eyebrow", "Coleção"), ("title", title), ("subtitle", "Notas reunidas num só lugar."), ("layout", "center"), ("height", "auto"))),
                Section("notes", Obj(("title", "Índice"), ("source", "folder"), ("folder", folder), ("layout", "cards"), ("columns", 3), ("limit", 60), ("sort", "title"), ("excerpt", true), ("expand", true)))
            ]);
    }

    private static JsonObject Resume(PageTemplateContext c, int year)
    {
        var title = c.Title ?? "Seu Nome";
        return Page(title + " — Currículo", "💼", Theme("papel", ("mode", "light"), ("headingFont", "fraunces"), ("bodyFont", "dmsans")),
            Layout(("nav", false), ("themeToggle", false), ("footer", "")),
            [
                Section("hero", Obj(("eyebrow", "Profissão · Cidade"), ("title", title), ("subtitle", "Resumo profissional."), ("layout", "left"), ("height", "auto"))),
                Section("timeline", Obj(("title", "Experiência"), ("items", List(Obj(("date", (year - 2) + " – hoje"), ("title", "Cargo · Empresa"), ("text", "Resultado.")))))),
                Section("features", Obj(("title", "Habilidades"), ("items", List(Obj(("icon", "🧭"), ("title", "Liderança"), ("text", "Times.")), Obj(("icon", "🛠️"), ("title", "Técnica"), ("text", "Ferramentas.")))))),
                Section("timeline", Obj(("title", "Formação"), ("items", List(Obj(("date", (year - 8).ToString()), ("title", "Curso · Instituição"), ("text", "")))))),
                Section("contact", Obj(("title", "Contato"), ("email", "voce@exemplo.com")))
            ]);
    }

    private static JsonObject Event(PageTemplateContext c, int year)
    {
        var title = c.Title ?? "Nome do Evento";
        var date = (year + 1) + "-01-01";
        return Page(title, "🎟️", Theme("entardecer"), Layout(("brand", title), ("navCta", "Inscrever-se"), ("navCtaUrl", "#inscricao")),
            [
                Section("hero", Obj(("eyebrow", date + " · Local"), ("title", title), ("subtitle", "Um dia inteiro de ideias."), ("layout", "center"), ("height", "screen"))),
                Section("countdown", Obj(("title", "Faltam"), ("date", date + " 09:00"), ("done", "O evento começou!"))),
                Section("timeline", Obj(("title", "Programação"), ("items", List(Obj(("date", "09:00"), ("title", "Abertura"), ("text", "Boas-vindas.")))))),
                Section("testimonials", Obj(("title", "Palestrantes"), ("items", List(Obj(("text", "Tema da fala."), ("author", "Pessoa")))))),
                Section("faq", Obj(("title", "Dúvidas"), ("items", List(Obj(("q", "Onde será?"), ("a", "Endereço.")))))),
                Section("cta", Obj(("title", "Vagas limitadas"), ("text", "Garanta a sua agora.")))
            ]);
    }

    private static JsonObject Docs(PageTemplateContext c)
    {
        var folder = c.Folder ?? "";
        var title = c.Title ?? (folder.Length > 0 ? folder.Split('/').Last() : "Documentação");
        return Page(title, "📚", Theme("oceano", ("width", 1040)), Layout(("brand", title), ("progress", true)),
            [
                Section("hero", Obj(("title", title), ("subtitle", "Guia completo, organizado a partir das notas."), ("layout", "left"), ("height", "auto"))),
                Section("notes", Obj(("title", "Conteúdo"), ("source", "folder"), ("folder", folder), ("layout", "list"), ("limit", 100), ("sort", "path"), ("excerpt", true), ("expand", true)))
            ]);
    }

    private static JsonObject Book(PageTemplateContext c, int year, bool folder)
    {
        var f = c.Folder ?? "";
        var title = c.Title ?? (folder && f.Length > 0 ? f.Split('/').Last() : "O título do livro");
        var middle = folder
            ? new[] { Section("chapters", Obj(("folder", f), ("sort", "path"), ("dropCap", true))) }
            : new[]
            {
                Section("part", Obj(("title", "O começo"), ("markdown", "Onde tudo começa."))),
                Section("chapter", Obj(("title", "A cidade de papel"), ("markdown", "Escreva aqui o primeiro capítulo."))),
                Section("chapter", Obj(("title", "Ruas e pontes"), ("markdown", "Cada capítulo começa numa página nova.")))
            };

        var sections = new List<JsonObject>
        {
            Section("bookcover", Obj(("title", title), ("subtitle", "Um subtítulo que convida à leitura"), ("author", "Nome do autor"), ("publisher", "Editora"), ("style", "classic"))),
            Section("titlepage", Obj(("title", title), ("subtitle", "Um subtítulo que convida à leitura"), ("author", "Nome do autor"), ("publisher", "Editora"), ("place", "Cidade"), ("year", year.ToString()))),
            Section("copyright", new JsonObject()),
            Section("dedication", Obj(("kind", "dedication"), ("markdown", "Para quem lê."))),
            Section("booktoc", Obj(("title", "Sumário")))
        };
        sections.AddRange(middle);
        sections.Add(Section("about", new JsonObject()));
        sections.Add(Section("colophon", new JsonObject()));

        return Page(title, "📖", Theme("livro"),
            Layout(("format", "book"), ("pageSize", "a5"), ("margins", "normal"), ("pageNumbers", true), ("runningHead", title), ("chapterStyle", "word"), ("nav", false), ("footer", "")),
            sections.ToArray());
    }

    private static JsonObject Links(PageTemplateContext c)
    {
        var title = c.Title ?? "@seunome";
        return Page(title, "🔗", Theme("neon", ("width", 560)),
            Layout(("nav", false), ("backToTop", false), ("footer", "")),
            [Section("hero", Obj(("title", title), ("subtitle", "Criador de conteúdo · links abaixo 👇"), ("layout", "center"), ("height", "screen"), ("stack", true), ("buttons", Buttons(("Meu site", "https://exemplo.com", "primary"), ("Instagram", "https://instagram.com", "secondary"), ("YouTube", "https://youtube.com", "secondary")))))]);
    }

    private static JsonObject FreeCanvas(string title)
    {
        var root = new JsonObject
        {
            ["type"] = "box",
            ["style"] = Obj(("gap", "64px"), ("padding", "0 0 48px")),
            ["children"] = List(
                new JsonObject
                {
                    ["type"] = "box",
                    ["style"] = Obj(("gap", "18px"), ("padding", "56px 20px"), ("textAlign", "center")),
                    ["children"] = List(
                        new JsonObject { ["type"] = "heading", ["content"] = Obj(("text", title), ("level", 1)) },
                        new JsonObject { ["type"] = "text", ["content"] = Obj(("text", "Monte esta página peça por peça.")) })
                },
                new JsonObject
                {
                    ["type"] = "box",
                    ["style"] = Obj(("display", "grid"), ("columns", 3), ("gap", "18px")),
                    ["mobile"] = Obj(("display", "stack")),
                    ["children"] = List(
                        new JsonObject { ["type"] = "text", ["content"] = Obj(("text", "Primeiro destaque")) },
                        new JsonObject { ["type"] = "text", ["content"] = Obj(("text", "Segundo destaque")) },
                        new JsonObject { ["type"] = "text", ["content"] = Obj(("text", "Terceiro destaque")) })
                })
        };
        return Section("free", Obj(("root", root), ("sheet", "page")));
    }

    private static JsonObject Page(
        string title,
        string icon,
        JsonObject theme,
        JsonObject layout,
        JsonObject[] sections) =>
        new()
        {
            ["version"] = 1,
            ["kind"] = "urbe-page",
            ["meta"] = Obj(("title", title), ("icon", icon)),
            ["theme"] = theme,
            ["layout"] = layout,
            ["sections"] = new JsonArray(sections.Select(section => (JsonNode)section).ToArray())
        };

    private static JsonObject Section(string type, JsonObject props) =>
        new() { ["type"] = type, ["props"] = props, ["style"] = new JsonObject() };

    private static JsonObject Theme(string preset, params (string Key, object? Value)[] values)
    {
        var theme = Obj(("preset", preset));
        foreach (var (key, value) in values)
            theme[key] = ToNode(value);
        return theme;
    }

    private static JsonObject Layout(params (string Key, object? Value)[] values)
    {
        var layout = new JsonObject();
        foreach (var (key, value) in values)
            layout[key] = ToNode(value);
        return layout;
    }

    private static JsonArray Buttons(params (string Label, string Url, string Variant)[] buttons) =>
        new(buttons.Select(button =>
            (JsonNode)Obj(("label", button.Label), ("url", button.Url), ("variant", button.Variant))).ToArray());

    private static JsonArray List(params JsonObject[] values) =>
        new(values.Select(value => (JsonNode)value).ToArray());

    private static JsonObject Obj(params (string Key, object? Value)[] values)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in values)
            obj[key] = ToNode(value);
        return obj;
    }

    private static JsonNode? ToNode(object? value) =>
        value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            string text => JsonValue.Create(text),
            bool boolean => JsonValue.Create(boolean),
            int integer => JsonValue.Create(integer),
            double number => JsonValue.Create(number),
            _ => JsonValue.Create(value.ToString())
        };

    private static void TruncateLists(JsonNode node, string? key = null)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToArray())
                if (pair.Value is not null)
                    TruncateLists(pair.Value, pair.Key);
            return;
        }

        if (node is not JsonArray array)
            return;
        var max = key == "buttons" ? 1 : 2;
        while (array.Count > max)
            array.RemoveAt(array.Count - 1);
        foreach (var child in array)
            if (child is not null)
                TruncateLists(child);
    }

    private static void Skeletonize(JsonNode node, string? key = null)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToArray())
            {
                if (pair.Value is null)
                    continue;
                if (pair.Value is JsonArray array)
                {
                    while (array.Count > 1)
                        array.RemoveAt(array.Count - 1);
                    foreach (var child in array)
                        if (child is not null)
                            Skeletonize(child, pair.Key);
                }
                else if (pair.Value is JsonObject nested)
                {
                    Skeletonize(nested, pair.Key);
                }
                else if (pair.Value is JsonValue)
                {
                    var text = PageDocument.StringValue(pair.Value);
                    if (text is null)
                        continue;
                    obj[pair.Key] = pair.Key switch
                    {
                        "title" => "Título",
                        "markdown" or "subtitle" or "text" => "Escreva aqui.",
                        "image" or "url" or "src" => "",
                        _ => text
                    };
                }
            }
        }
    }
}
