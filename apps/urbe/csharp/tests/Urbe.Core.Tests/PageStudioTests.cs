using System.Text.Json.Nodes;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>The editing model of the page studio (studio.js): undo steps, sections, fields, lists, themes, templates, JSON and the free layout.</summary>
public sealed class PageStudioTests
{
    private static readonly PageEngine Engine = new(2026);

    private static PageStudio Open(JsonObject? spec = null) =>
        new(Engine, LegacyCustomize.Stringify(Engine.Compact(spec ?? Engine.Normalize(new JsonObject
        {
            ["meta"] = new JsonObject { ["title"] = "Teste" },
            ["sections"] = new JsonArray(new JsonObject { ["type"] = "hero" }, new JsonObject { ["type"] = "text", ["props"] = new JsonObject { ["markdown"] = "Olá **mundo** e mais" } })
        }).Spec), 2) + "\n");

    [Fact]
    public void TypingInTheSameFieldIsOneUndoStep_AndUndoRedoRestore()
    {
        var st = Open();
        st.Set("sections.0.props.title", "A", key: "sections.0.props.title");
        st.Set("sections.0.props.title", "AB", key: "sections.0.props.title");
        Assert.Equal("AB", (string?)st.Get("sections.0.props.title"));
        Assert.True(st.Undo());
        Assert.Equal("Um título que diz tudo", (string?)st.Get("sections.0.props.title"));
        Assert.False(st.CanUndo);
        Assert.True(st.Redo());
        Assert.Equal("AB", (string?)st.Get("sections.0.props.title"));
        // another field, or the same after 900 ms, is a new step
        st.Set("sections.0.props.eyebrow", "x", key: "sections.0.props.eyebrow");
        Assert.True(st.Undo());
        Assert.Equal("AB", (string?)st.Get("sections.0.props.title"));
    }

    [Fact]
    public void SectionsAreAddedAfterTheSelection_MovedDuplicatedHiddenAndRemoved()
    {
        var st = Open();
        var hero = (string)st.Sections[0]!["id"]!;
        st.Select(hero);
        var faq = st.AddSection("faq");
        Assert.Equal(faq, (string?)st.Sections[1]!["id"]);
        Assert.Equal(faq, st.Selected);
        Assert.Equal("Perguntas", st.Label(st.Section(faq)!).Label.Split(' ')[0]);
        st.MoveSection(faq, 5);
        Assert.Equal(faq, (string?)st.Sections[2]!["id"]);
        var copy = st.DuplicateSection(hero)!;
        Assert.Equal(copy, (string?)st.Sections[1]!["id"]);
        Assert.NotEqual(hero, copy);
        st.ToggleHidden(copy);
        Assert.True((bool)st.Section(copy)!["style"]!["hidden"]!);
        st.Select(copy);
        var removed = st.RemoveSection(copy)!;
        Assert.Equal("Capa", removed.Label);
        Assert.Equal((string?)st.Sections[1]!["id"], st.Selected);
        Assert.Equal(3, st.Sections.Count);
        Assert.Equal("Olá mundo e mais", st.Label((JsonObject)st.Sections[1]!).Title);
    }

    [Fact]
    public void FieldsAreCoercedByTheirDefinition_AndListsHaveTheirOperations()
    {
        var st = Open();
        Assert.Equal("select", (string?)st.FieldAt("theme.preset")!["type"]);
        st.Set("theme.preset", "nao-existe");
        Assert.Equal("aurora", (string?)st.Get("theme.preset"));
        int before = st.Get("sections.0.props.buttons")!.AsArray().Count;
        st.ListOp("sections.0.props.buttons", "add");
        var variant = (string)st.FieldAt("sections.0.props.buttons.0.variant")!["options"]!.AsArray()[^1]!;
        st.Set("sections.0.props.buttons." + before + ".variant", variant);
        Assert.Equal(variant, (string?)st.Get("sections.0.props.buttons." + before + ".variant"));
        st.Set("sections.0.props.buttons." + before + ".variant", "nao-existe");
        Assert.NotEqual("nao-existe", (string?)st.Get("sections.0.props.buttons." + before + ".variant"));
        var buttons = st.Get("sections.0.props.buttons")!.AsArray();
        Assert.Equal(before + 1, buttons.Count);
        Assert.Equal("Saiba mais", (string?)buttons[^1]!["label"]);
        st.ListOp("sections.0.props.buttons", "dup", buttons.Count - 1);
        Assert.Equal(before + 2, st.Get("sections.0.props.buttons")!.AsArray().Count);
        st.Set("sections.0.props.buttons." + (before + 1) + ".label", "Último");
        st.ListOp("sections.0.props.buttons", "up", before + 1);
        Assert.Equal("Último", (string?)st.Get("sections.0.props.buttons")!.AsArray()[before]!["label"]);
        st.ListOp("sections.0.props.buttons", "del", before);
        Assert.Equal(before + 1, st.Get("sections.0.props.buttons")!.AsArray().Count);
    }

    [Fact]
    public void AReadyThemeClearsTheColorsAndFonts()
    {
        var st = Open();
        st.Set("theme.primary", "#ff0000");
        st.SetPreset("papel");
        Assert.Equal("papel", (string?)st.Theme["preset"]);
        Assert.Equal("", (string?)st.Theme["primary"]);
        Assert.Null(st.Theme["radius"]);
    }

    [Fact]
    public void TemplatesReplaceAppendOrOnlyChangeTheLook()
    {
        var st = Open();
        var tpl = PageTemplates.Build(Engine, "landing", "Produto", "", null, new DateTime(2026, 10, 15))!;
        st.ApplyTemplate(tpl, "theme");
        Assert.Equal((string?)tpl["theme"]!["preset"], (string?)st.Theme["preset"]);
        Assert.Equal(2, st.Sections.Count);
        st.ApplyTemplate(tpl, "append");
        Assert.Equal(2 + tpl["sections"]!.AsArray().Count, st.Sections.Count);
        st.ApplyTemplate(tpl, "replace");
        Assert.Equal(tpl["sections"]!.AsArray().Count, st.Sections.Count);
        Assert.Equal("Teste", (string?)st.Meta["title"]);
        // the sections got new ids
        Assert.DoesNotContain(st.Sections.Select(s => (string?)s!["id"]), id => tpl["sections"]!.AsArray().Any(t => (string?)t!["id"] == id));
    }

    [Fact]
    public void JsonWithErrorsChangesNothing_AndValidJsonReplacesThePage()
    {
        var st = Open();
        var bad = st.ApplyJson("{\"sections\":[{\"type\":\"nao-existe\"}]}");
        Assert.NotEmpty(bad.Errors);
        Assert.Equal(2, st.Sections.Count);
        var good = st.ApplyJson("{\"meta\":{\"title\":\"Outra\"},\"sections\":[{\"type\":\"quote\",\"props\":{\"text\":\"Frase\"}}]}");
        Assert.Empty(good.Errors);
        Assert.Equal("Outra", (string?)st.Meta["title"]);
        Assert.Single(st.Sections);
        Assert.True(st.Undo());
        Assert.Equal("Teste", (string?)st.Meta["title"]);
    }

    [Fact]
    public void TheFreeLayoutHasItsElementOperations()
    {
        var st = Open(PageStudio.QuickPage(Engine));
        var sec = (string)st.Sections[0]!["id"]!;
        st.Select(sec);
        var root = (JsonObject)st.Get("sections.0.props.root")!;
        var heading = (JsonObject)root["children"]![0]!;
        Assert.Equal("Nova página", (string?)heading["content"]!["text"]);
        st.NodeSelected = (string)heading["id"]!;
        st.Set(PageStudio.FreePath(sec, st.NodeSelected, "content", "text"), "Meu título", key: "t");
        Assert.Equal("Meu título", (string?)st.Get("sections.0.props.root.children.0.content.text"));
        st.FreeOp(sec, "add", "button");
        var kids = st.Get("sections.0.props.root.children")!.AsArray();
        Assert.Equal(3, kids.Count);
        Assert.Equal("button", (string?)kids[1]!["type"]);
        Assert.Equal((string?)kids[1]!["id"], st.NodeSelected);
        st.FreeOp(sec, "wrap");
        Assert.Equal("box", (string?)st.Get("sections.0.props.root.children.1.type"));
        st.FreeOp(sec, "dup");
        Assert.Equal(2, st.Get("sections.0.props.root.children.1.children")!.AsArray().Count);
        st.FreeOp(sec, "del");
        Assert.Single(st.Get("sections.0.props.root.children.1.children")!.AsArray());
        // a style on tablet only lives in the tablet bucket, and emptying it removes the bucket
        st.Device = "tablet";
        st.Set(PageStudio.FreePath(sec, st.NodeSelected!, st.Bucket, "gap"), "8px");
        Assert.Equal("8px", (string?)st.Get("sections.0.props.root.children.1.tablet.gap"));
        st.Set(PageStudio.FreePath(sec, st.NodeSelected!, st.Bucket, "gap"), "");
        Assert.Null(st.Get("sections.0.props.root.children.1.tablet"));
        // a ready composition inside the selected layout
        int count = st.Get("sections.0.props.root.children.1.children")!.AsArray().Count;
        st.InsertPreset("cartao");
        Assert.Equal(count + 1, st.Get("sections.0.props.root.children.1.children")!.AsArray().Count);
        Assert.Contains("Engine", Engine.Render(st.Spec) is { Length: > 0 } ? "Engine" : "");
    }

    [Fact]
    public void NamesAndPathsFollowTheOriginal()
    {
        Assert.Equal("a-b", PageStudio.CleanName("  a/b "));
        var taken = new HashSet<string> { "Páginas/X.page.json", "Páginas/X (2).page.json" };
        Assert.Equal("Páginas/X (3).page.json", PageStudio.UniquePath("Páginas/X.page.json", taken.Contains));
        Assert.Equal("Páginas/Meu site.page.json", PageStudio.RenameTarget("Páginas/Nova página.page.json", "Meu site", "Nova página", _ => false));
        Assert.Null(PageStudio.RenameTarget("Páginas/Escolhido.page.json", "Meu site", "Outro", _ => false));
        Assert.Equal("Páginas/Novo.page.json", PageStudio.RenameTarget("Páginas/Velho.page.json", "Novo", "Velho", _ => false));
    }
}

public sealed class PageStudioConvertTests
{
    [Fact]
    public void ABlockConvertsToAFreeLayoutKeepingItsIdAndLook()
    {
        var engine = new PageEngine(2026);
        var st = new PageStudio(engine, "{\"sections\":[{\"type\":\"text\",\"props\":{\"title\":\"Sobre\",\"markdown\":\"Um.\\n\\nDois.\"},\"style\":{\"padding\":\"l\"}}]}");
        var id = (string)st.Sections[0]!["id"]!;
        Assert.True(st.ConvertToFree(id));
        var sec = st.Section(id)!;
        Assert.Equal("free", (string?)sec["type"]);
        Assert.Equal("l", (string?)sec["style"]!["padding"]);
        Assert.Equal(3, sec["props"]!["root"]!["children"]!.AsArray().Count);
        Assert.True(st.Undo());
        Assert.Equal("text", (string?)st.Section(id)!["type"]);
        Assert.False(new PageStudio(engine, "{\"sections\":[{\"type\":\"faq\"}]}") is var other && other.ConvertToFree((string)other.Sections[0]!["id"]!));
    }
}

public sealed class PageMathTests
{
    [Fact]
    public void FormulasAreDrawnWhenTheAppHasMath_AndKeptAsTextWithout()
    {
        var spec = "{\"sections\":[{\"type\":\"text\",\"props\":{\"markdown\":\"Área $a^2$ e preço $5.\\n\\n$$\\\\int_0^1 x\\\\,dx$$\"}}]}";
        var plain = new PageEngine(2026).Render(spec);
        Assert.Contains("$a^2$", plain);
        var engine = new PageEngine(2026) { MathHtml = (tex, display) => "<b data-d=\"" + display + "\">" + tex + "</b>" };
        var html = engine.Render(spec);
        Assert.Contains("<span class=\"umath\" data-tex=\"a^2\" data-open=\"$\" data-close=\"$\"><b data-d=\"False\">a^2</b></span>", html);
        Assert.Contains("<div class=\"umath umath-block\" data-tex=\"\\int_0^1 x\\,dx\"", html);
        Assert.Contains("preço $5.", html);
        Assert.DoesNotContain("<p><div", html);
    }
}
