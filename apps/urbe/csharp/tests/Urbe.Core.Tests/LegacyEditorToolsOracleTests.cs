using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The note editor rules against the unmodified 1.8.4-beta app.js / visual-tools.js / markdown.js
/// run by Node (tools/editor-oracle.mjs).</summary>
public sealed class LegacyEditorToolsOracleTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        return root!.FullName;
    }

    private static async Task<JsonElement[]> Run(object[] cases)
    {
        var casesFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(casesFile, JsonSerializer.Serialize(cases));
        try
        {
            var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Path.Combine(Root, "csharp", "tools", "editor-oracle.mjs"));
            start.ArgumentList.Add(Path.Combine(Root, "src"));
            start.ArgumentList.Add(casesFile);
            using var process = Process.Start(start)!;
            var token = TestContext.Current.CancellationToken;
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
            return JsonDocument.Parse(await stdout).RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
        }
        finally { File.Delete(casesFile); }
    }

    private static void SameEdit(JsonElement js, LegacyEditorTools.TextEdit? cs, string why)
    {
        Assert.NotNull(cs);
        Assert.Equal(js.GetProperty("value").GetString(), cs!.Text);
        Assert.Equal(js.GetProperty("start").GetInt32(), cs.SelectionStart);
        Assert.Equal(js.GetProperty("end").GetInt32(), cs.SelectionEnd);
        _ = why;
    }

    private static int Safe(string t, int i) => i > 0 && i < t.Length && char.IsLowSurrogate(t[i]) ? i - 1 : i;

    private static readonly string[] Texts =
        ["", "abc def", "linha um\nlinha dois", "olá [[Ca", "x [[Nota]] y [[Out", "[[a\nb", "[[a]] [[", "ç[[\n", "😀 [[é", "# T\n\n- a\n- b"];

    [Fact]
    public async Task ToolbarInsertionsMatchTheOriginal()
    {
        var cases = new List<object>();
        var expected = new List<(string, int, int, LegacyEditorTools.ToolbarButton)>();
        foreach (var t in Texts)
            foreach (var (a, b) in new[] { (0, 0), (t.Length, t.Length), (Safe(t, Math.Min(1, t.Length)), t.Length), (Safe(t, t.Length / 2), Safe(t, t.Length / 2)) })
                foreach (var button in LegacyEditorTools.Toolbar.Where(x => x.Special != "noteLink"))
                {
                    var (prefix, suffix) = button.Special == "link" ? ("[", "](url)") : button.Wrap is { } w ? (w, w) : (button.Md ?? "", "");
                    cases.Add(new { op = "insert", value = t, start = a, end = b, prefix, suffix });
                    expected.Add((t, a, b, button));
                }
        var js = await Run([.. cases]);
        for (var i = 0; i < js.Length; i++)
        {
            var (t, a, b, button) = expected[i];
            SameEdit(js[i], LegacyEditorTools.ApplySource(button, t, a, b), button.Id);
        }
        // reversed selection (anchor after focus) is normalised the same way by selectionStart/End
        Assert.Equal(LegacyEditorTools.InsertAtCursor("abcdef", 1, 4, "**", "**"), LegacyEditorTools.InsertAtCursor("abcdef", 4, 1, "**", "**"));
        Assert.Equal(12, LegacyEditorTools.Toolbar.Count);
    }

    [Fact]
    public async Task WikilinkCompletionContextAndCommitMatchTheOriginal()
    {
        var cases = new List<object>();
        var args = new List<(string, int)>();
        foreach (var t in Texts)
            for (var c = 0; c <= t.Length; c++)
            {
                if (c != Safe(t, c)) continue;
                cases.Add(new { op = "wiki", value = t, caret = c });
                cases.Add(new { op = "commitWiki", value = t, caret = c, name = "Minha nota" });
                args.Add((t, c));
            }
        var js = await Run([.. cases]);
        for (var i = 0; i < args.Count; i++)
        {
            var (t, c) = args[i];
            var ctx = LegacyEditorTools.FindWikiContext(t, c);
            var jw = js[2 * i];
            if (jw.ValueKind == JsonValueKind.Null) Assert.Null(ctx);
            else
            {
                Assert.NotNull(ctx);
                Assert.Equal(jw.GetProperty("start").GetInt32(), ctx!.Start);
                Assert.Equal(jw.GetProperty("query").GetString(), ctx.Query);
            }
            var jc = js[2 * i + 1];
            var commit = LegacyEditorTools.CommitWiki(t, c, "Minha nota");
            if (jc.ValueKind == JsonValueKind.Null) Assert.Null(commit);
            else SameEdit(jc, commit, t);
        }
    }

    [Fact]
    public async Task CodeToolsStatsAndSlashMenuMatchTheOriginal()
    {
        var indent = new[] { ("a\nb\nc", 0, 5), ("a\nb\nc", 2, 2), ("", 0, 0), ("x\n\ny", 1, 3) };
        var json = new[] { "{\"a\":[1,2.50,{}],\"b\":\"é\\n\",\"c\":1e21,\"d\":0.1,\"e\":[],\"f\":null,\"g\":-0}", "[1,2", "", "  [ ]  ", "\"s\"", "{\"a\":1,\"a\":2}" };
        var css = new[] { "a{color:red;margin:0}\n\nb { x:y }", "  ", "a { x:1;;}" };
        var stats = new[] { "", "   ", "um dois\ntrês", "a b　c\n", "\n\n", "x\u0085y" };
        var queries = new[] { "", "tit", "TÍTULO", "link", "xyz", "citação", "h2", "  " };
        var slashTexts = new[] { "/", "/ti", "/a b", "/a/b", "​/h​", "/" + new string('x', 24), "/" + new string('x', 25), "x/", "" };

        var cases = new List<object>();
        foreach (var (t, a, b) in indent) cases.Add(new { op = "indent", value = t, start = a, end = b });
        foreach (var j in json) cases.Add(new { op = "format", value = j, ext = "json" });
        foreach (var c in css) cases.Add(new { op = "format", value = c, ext = "css" });
        cases.Add(new { op = "format", value = "x", ext = "txt" });
        foreach (var s in stats) cases.Add(new { op = "stats", value = s });
        foreach (var q in queries) cases.Add(new { op = "slash", query = q });
        foreach (var s in slashTexts) cases.Add(new { op = "slashQuery", text = s });
        var js = await Run([.. cases]);
        var n = 0;
        foreach (var (t, a, b) in indent) SameEdit(js[n++], LegacyEditorTools.IndentSelection(t, a, b), t);
        foreach (var j in json) Format(js[n++], j, "json");
        foreach (var c in css) Format(js[n++], c, "css");
        Format(js[n++], "x", "txt");
        foreach (var s in stats)
        {
            var (lines, words) = LegacyEditorTools.Count(s);
            Assert.Equal(js[n].GetProperty("lines").GetInt32(), lines);
            Assert.Equal(js[n].GetProperty("words").GetInt32(), words);
            n++;
        }
        foreach (var q in queries)
            Assert.Equal(js[n++].EnumerateArray().Select(e => e.GetString()), LegacyEditorTools.FilterSlash(q).Select(x => x.Act));
        foreach (var s in slashTexts)
        {
            var e = js[n++];
            Assert.Equal(e.ValueKind == JsonValueKind.Null ? null : e.GetString(), LegacyEditorTools.SlashQuery(s));
        }
    }

    private static void Format(JsonElement js, string text, string ext)
    {
        var (result, message) = LegacyEditorTools.Format(text, ext);
        var changed = js.GetProperty("changed").GetInt32() > 0;
        Assert.Equal(changed, result is not null);
        if (changed) Assert.Equal(js.GetProperty("value").GetString(), result);
        else Assert.Equal(text, js.GetProperty("value").GetString());
        var toast = js.GetProperty("toast");
        if (toast.ValueKind == JsonValueKind.Null) Assert.Equal("", message);
        else Assert.Equal(toast.GetString(), message);
    }

    [Fact]
    public async Task MarkdownRendererMatchesTheOriginalOverTheRepositoryCorpus()
    {
        var corpus = new List<string>();
        foreach (var dir in new[] { Path.Combine(Root, "tutorial"), Path.Combine(Root, "docs"), Path.Combine(Root, "csharp", "docs") })
            if (Directory.Exists(dir))
                corpus.AddRange(Directory.EnumerateFiles(dir, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText));
        corpus.AddRange(
        [
            "", "# ", "#\n# a", "---\ntags: [a, b]\nx: y\n---\n# T", "> [!tip] Dica\n> corpo\n> mais", "| a | b |\n|:-|-:|\n| 1 | 2 |",
            "- [ ] a\n  - [x] b\n1. um\n2) dois", "```js\nlet a = 1 < 2;\n```\n```", "[a](javascript:x) ![i](data:text/html;x) [[N|r]] `[[x]]` ~~d~~ **b** _i_",
            "* a\n\n* b\n---\n***", "tab\t- x\n\t- y"
        ]);
        var inputs = corpus.Where(c => !c.Contains('$')).ToArray();
        Assert.True(inputs.Length > 40);
        var js = await Run(inputs.Select(c => (object)new { op = "markdown", md = c }).ToArray());
        for (var i = 0; i < inputs.Length; i++)
            Assert.Equal(js[i].GetString(), MarkdownEngine.Render(inputs[i]));
    }
}
