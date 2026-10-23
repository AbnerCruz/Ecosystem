using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>A section as the studio lists it (secTitle): the block's label and icon and what identifies this one.</summary>
public sealed record PageSectionLabel(string Label, string Title, string Icon);

/// <summary>An element of a free layout as the studio lists it (fxLabel).</summary>
public sealed record FreeNodeLabel(string Icon, string Label, string Text);

/// <summary>
/// The editing model of the page studio of the 1.8.4 (src/pages/studio.js), without its screen: the page being edited,
/// the selected section and element, undo/redo (typing in the same field within 900 ms is one step; 150 steps kept),
/// the section operations, the fields by path (pathGet/pathSet/fieldAt/coerceVal), lists, ready themes, templates
/// (replace/append/theme), the JSON and the free layout's operations. The file text is what save() writes.
/// </summary>
public sealed class PageStudio
{
    private readonly PageEngine _engine;
    private readonly List<string> _hist = [], _fut = [];
    private string _lastKey = "";
    private long _lastAt;

    public PageStudio(PageEngine engine, string text)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        JsonNode? raw;
        try { raw = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text); InvalidJson = false; }
        catch (System.Text.Json.JsonException) { raw = new JsonObject(); InvalidJson = true; }
        var n = engine.Normalize(raw);
        Spec = n.Spec;
        OpenProblems = n.Errors.Count;
        SavedText = text;
    }

    public PageEngine Engine => _engine;
    public JsonObject Spec { get; private set; }

    /// <summary>The file was not JSON at all (it opens empty; "JSON inválido").</summary>
    public bool InvalidJson { get; }

    /// <summary>Problems corrected when opening ("N problema(s) no arquivo foram corrigidos ao abrir").</summary>
    public int OpenProblems { get; }

    public string? Selected { get; private set; }
    public string? NodeSelected { get; set; }
    public bool CanUndo => _hist.Count > 0;
    public bool CanRedo => _fut.Count > 0;

    /// <summary>The text last written (or read) — the save is skipped when nothing changed.</summary>
    public string SavedText { get; set; }

    public event EventHandler? Changed;

    public JsonObject Meta => (JsonObject)Spec["meta"]!;
    public JsonObject Theme => (JsonObject)Spec["theme"]!;
    public JsonObject Layout => (JsonObject)Spec["layout"]!;
    public JsonArray Sections => (JsonArray)Spec["sections"]!;
    public bool IsBook => (string?)Layout["format"] == "book";

    /// <summary>save(): JSON.stringify(compact(spec), null, 2) + "\n".</summary>
    public string Text() => LegacyCustomize.Stringify(_engine.Compact(Spec), 2) + "\n";

    private string Snapshot() => Spec.ToJsonString();

    /// <summary>commit(fn, {key}): one undo step unless the same key changed within 900 ms.</summary>
    public void Commit(Action<JsonObject> fn, string? key = null, long? nowMs = null)
    {
        var now = nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!(key is { Length: > 0 } && key == _lastKey && now - _lastAt < 900))
        {
            _hist.Add(Snapshot());
            if (_hist.Count > 150) _hist.RemoveAt(0);
            _fut.Clear();
        }
        _lastKey = key ?? "";
        _lastAt = now;
        fn(Spec);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A new step even for the same field (a switch, a choice, a button).</summary>
    public void Break() => _lastKey = "";

    public bool Undo()
    {
        if (_hist.Count == 0) return false;
        _fut.Add(Snapshot());
        Spec = (JsonObject)JsonNode.Parse(_hist[^1])!;
        _hist.RemoveAt(_hist.Count - 1);
        _lastKey = "";
        if (Selected is not null && Section(Selected) is null) Selected = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (_fut.Count == 0) return false;
        _hist.Add(Snapshot());
        Spec = (JsonObject)JsonNode.Parse(_fut[^1])!;
        _fut.RemoveAt(_fut.Count - 1);
        _lastKey = "";
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>The page replaced from outside (the Assistente, another editor): one undo step back to what it was.</summary>
    public void Replace(string text)
    {
        JsonNode? raw;
        try { raw = JsonNode.Parse(text); }
        catch (System.Text.Json.JsonException) { return; }
        _hist.Add(Snapshot());
        Spec = _engine.Normalize(raw).Spec;
        SavedText = text;
        if (Selected is not null && Section(Selected) is null) Selected = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- sections

    public JsonObject? Section(string? id) => id is null ? null : Sections.OfType<JsonObject>().FirstOrDefault(s => (string?)s["id"] == id);
    public int IndexOf(string? id) => Sections.OfType<JsonObject>().ToList().FindIndex(s => (string?)s["id"] == id);
    public JsonObject? SelectedSection => Section(Selected);

    public void Select(string? id)
    {
        bool changed = id != Selected;
        Selected = id is not null && Section(id) is not null ? id : null;
        if (changed) NodeSelected = null;
    }

    /// <summary>
    /// The type of the field at "sections.i.props.key" or "sections.i.props.list.j.key" in the block's own fields (for a
    /// message of the preview to change only what the field is: an image only where an image goes); null when none.
    /// </summary>
    public string? FieldType(string path)
    {
        var m = Regex.Match(path, @"^sections\.(\d+)\.props\.([A-Za-z]\w*)(?:\.(\d+)\.([A-Za-z]\w*))?$");
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var i) || i >= Sections.Count || Sections[i] is not JsonObject sec) return null;
        if (_engine.BlockInfo(S(sec["type"])) is not { } b) return null;
        var field = b.Fields.OfType<JsonObject>().FirstOrDefault(f => S(f["key"]) == m.Groups[2].Value);
        if (field is null || !m.Groups[4].Success) return field is null ? null : S(field["type"]);
        if (S(field["type"]) != "list" || field["fields"] is not JsonArray sub) return null;
        if (sec["props"]?[m.Groups[2].Value] is not JsonArray items || !int.TryParse(m.Groups[3].Value, out var j) || j >= items.Count) return null;
        return sub.OfType<JsonObject>().FirstOrDefault(f => S(f["key"]) == m.Groups[4].Value) is { } inner ? S(inner["type"]) : null;
    }

    private static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is null ? "" : LegacyCustomize.JsToString(n);

    /// <summary>secTitle(s).</summary>
    public PageSectionLabel Label(JsonObject s)
    {
        var type = S(s["type"]);
        var b = _engine.BlockInfo(type);
        var p = s["props"] as JsonObject ?? [];
        string t = Truthy(p["title"]) ? S(p["title"]) : Truthy(p["name"]) ? S(p["name"]) : Truthy(p["q"]) ? S(p["q"]) : "";
        if (t.Length == 0 && type == "note") t = Regex.Replace(S(p["path"]).Split('/')[^1], @"\.md$", "", RegexOptions.IgnoreCase);
        if (t.Length == 0 && type == "text") t = Excerpt(S(p["markdown"]), 40);
        if (t.Length == 0 && type == "quote") t = Excerpt(S(p["text"]), 40);
        return new PageSectionLabel(b?.Label ?? type, t.Replace("**", ""), b?.Icon ?? "?");
    }

    private static bool Truthy(JsonNode? n) => PageEngine.Truthy(n);

    /// <summary>excerpt(md, n) of engine.js.</summary>
    public static string Excerpt(string? md, int n) => PageMarkdown.Excerpt(md, n);

    /// <summary>addBlock: after the selected section, or at the end; the new one is selected.</summary>
    public string AddSection(string type, JsonObject? saved = null)
    {
        var s = saved is not null ? FromSaved(saved) : _engine.NewSection(type);
        int i = Selected is not null ? IndexOf(Selected) + 1 : Sections.Count;
        Break();
        Commit(sp => ((JsonArray)sp["sections"]!).Insert(Math.Clamp(i, 0, ((JsonArray)sp["sections"]!).Count), s));
        Select((string)s["id"]!);
        return (string)s["id"]!;
    }

    /// <summary>fromMine: a saved block (Meus blocos) as a new section.</summary>
    private JsonObject FromSaved(JsonObject saved)
    {
        var n = _engine.Normalize(new JsonObject { ["sections"] = new JsonArray(saved.DeepClone()) }).Spec["sections"]!.AsArray()[0]!.AsObject();
        var copy = (JsonObject)n.DeepClone();
        copy["id"] = PageEngine.NewSectionId();
        if ((string?)copy["type"] == "free" && copy["props"]?["root"] is JsonObject root) copy["props"]!["root"] = PageEngine.FreeClone(root);
        return copy;
    }

    /// <summary>
    /// UC-62 slice 3: a template of the page's structure only — the sections, their look and their lists' length stay,
    /// the texts (titles, paragraphs, Markdown, code, the free nodes' words) go back to their defaults.
    /// </summary>
    public static JsonObject StructureOnly(JsonObject spec, PageEngine engine)
    {
        var copy = (JsonObject)spec.DeepClone();
        static bool IsText(string? type) => type is "text" or "textarea" or "markdown" or "code";
        static void Blank(JsonObject props, JsonArray fields)
        {
            foreach (var f in fields.OfType<JsonObject>())
            {
                var key = (string?)f["key"];
                if (key is null || !props.ContainsKey(key)) continue;
                var type = (string?)f["type"];
                // icons are part of the look, not words
                if (IsText(type) && key != "icon") props.Remove(key);
                else if (type == "list" && props[key] is JsonArray items && f["fields"] is JsonArray sub)
                    foreach (var item in items.OfType<JsonObject>()) Blank(item, sub);
                else if (type == "tree" && props[key] is JsonObject root)
                    PageEngine.FreeWalk(root, (n, _, _) =>
                    {
                        if (n["content"] is JsonObject content)
                            foreach (var k in new[] { "text", "author", "md", "tex", "code" })
                                if (content[k] is JsonValue) content[k] = "";
                        return false;
                    });
            }
        }
        foreach (var sec in (copy["sections"] as JsonArray ?? []).OfType<JsonObject>())
            if (sec["props"] is JsonObject props && engine.BlockInfo((string?)sec["type"]) is { } info) Blank(props, info.Fields);
        return copy;
    }

    public void MoveSection(string id, int delta)
    {
        int i = IndexOf(id), j = Math.Clamp(i + delta, 0, Sections.Count - 1);
        if (i < 0 || i == j) return;
        Break();
        Commit(sp =>
        {
            var list = (JsonArray)sp["sections"]!;
            var x = list[i]!;
            list.RemoveAt(i);
            list.Insert(j, x);
        });
    }

    public string? DuplicateSection(string id)
    {
        int i = IndexOf(id);
        if (i < 0) return null;
        var copy = (JsonObject)Sections[i]!.DeepClone();
        copy["id"] = PageEngine.NewSectionId();
        if ((string?)copy["type"] == "free" && copy["props"]?["root"] is JsonObject root) copy["props"]!["root"] = PageEngine.FreeClone(root);
        if (copy["style"] is JsonObject st && Truthy(st["anchor"])) st["anchor"] = S(st["anchor"]) + " 2";
        Break();
        Commit(sp => ((JsonArray)sp["sections"]!).Insert(i + 1, copy));
        Select((string)copy["id"]!);
        return (string)copy["id"]!;
    }

    /// <summary>removeSection: the selection goes to the next one (or the previous).</summary>
    public PageSectionLabel? RemoveSection(string id)
    {
        int i = IndexOf(id);
        if (i < 0) return null;
        var t = Label((JsonObject)Sections[i]!);
        Break();
        Commit(sp => ((JsonArray)sp["sections"]!).RemoveAt(i));
        if (Selected == id)
            Selected = (Sections.ElementAtOrDefault(i) ?? Sections.ElementAtOrDefault(i - 1)) is JsonObject next ? (string?)next["id"] : null;
        return t;
    }

    public void ToggleHidden(string id)
    {
        Break();
        Commit(sp =>
        {
            var x = (JsonObject)((JsonArray)sp["sections"]!)[IndexOf(id)]!;
            var st = (JsonObject)x["style"]!;
            st["hidden"] = !Truthy(st["hidden"]);
        });
    }

    /// <summary>Trocar tipo de bloco: the same id and look, the props carried over where the fields match.</summary>
    public void ChangeType(string id, string type, JsonObject? saved = null)
    {
        var old = Section(id);
        if (old is null) return;
        var nb = saved is not null ? FromSaved(saved) : _engine.NewSection(type, old["props"] as JsonObject);
        nb["id"] = id;
        nb["style"] = old["style"]!.DeepClone();
        Break();
        Commit(sp => ((JsonArray)sp["sections"]!)[IndexOf(id)] = nb);
    }

    /// <summary>convertToFree: the block becomes a free layout (same id and look); false when it does not convert.</summary>
    public bool ConvertToFree(string id, Func<string?, PageNote?>? note = null)
    {
        var sec = Section(id);
        if (sec is null || PageEngine.FreeFromBlock(sec, note) is not { } root) return false;
        var book = IsBook;
        var nsec = _engine.NewSection("free", new JsonObject { ["root"] = root, ["sheet"] = book && (string?)sec["type"] == "bookcover" ? "full" : "page" });
        nsec["id"] = id;
        nsec["style"] = sec["style"]!.DeepClone();
        Break();
        Commit(s => ((JsonArray)s["sections"]!)[IndexOf(id)] = nsec);
        NodeSelected = null;
        Selected = id;
        return true;
    }

    /// <summary>A ready theme: its preset, and the colors/fonts/mode/radius back to the theme's.</summary>
    public void SetPreset(string key)
    {
        Break();
        Commit(sp =>
        {
            var th = (JsonObject)sp["theme"]!;
            th["preset"] = key;
            foreach (var x in new[] { "bg", "surface", "text", "muted", "primary", "accent", "background", "headingFont", "bodyFont", "mode" }) th[x] = "";
            th["radius"] = null;
        });
    }

    // ---------------------------------------------------------------- fields by path

    /// <summary>pathGet(spec, "sections.2.props.title").</summary>
    public JsonNode? Get(string path)
    {
        JsonNode? cur = Spec;
        foreach (var k in path.Split('.'))
        {
            cur = cur switch
            {
                JsonObject o => o[k],
                JsonArray a when int.TryParse(k, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < a.Count => a[i],
                _ => null
            };
            if (cur is null) return null;
        }
        return cur;
    }

    private static void SetAt(JsonObject root, string path, JsonNode? value)
    {
        var ks = path.Split('.');
        JsonNode? t = root;
        for (int i = 0; i < ks.Length - 1; i++)
            t = t switch { JsonObject o => o[ks[i]], JsonArray a => a[int.Parse(ks[i], CultureInfo.InvariantCulture)], _ => null };
        var last = ks[^1];
        if (t is JsonObject to) to[last] = value;
        else if (t is JsonArray ta) ta[int.Parse(last, CultureInfo.InvariantCulture)] = value;
    }

    /// <summary>fieldAt(path): the field that describes this path (theme, meta, layout, a section's props or style, list items).</summary>
    public JsonObject? FieldAt(string path)
    {
        var ks = path.Split('.');
        static JsonObject? Find(JsonArray list, string key) => list.OfType<JsonObject>().FirstOrDefault(f => (string?)f["key"] == key);
        switch (ks[0])
        {
            case "theme": return Find(_engine.ThemeFields, ks[1]);
            case "meta": return Find(_engine.MetaFields, ks[1]);
            case "layout": return Find(_engine.LayoutFields, ks[1]);
        }
        if (ks[0] != "sections" || ks.Length < 4 || !int.TryParse(ks[1], out var si) || Sections.ElementAtOrDefault(si) is not JsonObject sec) return null;
        var list = ks[2] == "style" ? _engine.SectionFields : _engine.BlockInfo(S(sec["type"]))?.Fields;
        if (list is null) return null;
        JsonObject? f = null;
        for (int i = 3; i < ks.Length; i++)
        {
            if (Regex.IsMatch(ks[i], @"^\d+$")) continue;
            f = Find(list, ks[i]);
            if (f is null) return null;
            if ((string?)f["type"] == "list") list = f["fields"]!.AsArray();
        }
        return f;
    }

    /// <summary>coerceVal(f, raw): booleans, numbers clamped to the field, select options; the rest as typed.</summary>
    public static JsonNode? Coerce(JsonObject? f, JsonNode? raw)
    {
        if (f is null) return raw?.DeepClone();
        switch ((string?)f["type"])
        {
            case "boolean":
                return JsonValue.Create(PageEngine.Truthy(raw));
            case "number":
            {
                var text = raw is null ? "" : S(raw);
                if (text.Length == 0) return f["default"]?.DeepClone();
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n)) return f["default"]?.DeepClone();
                double min = f["min"] is JsonValue mi ? mi.GetValue<double>() : -1e9, max = f["max"] is JsonValue ma ? ma.GetValue<double>() : 1e9;
                return JsonValue.Create(Math.Max(min, Math.Min(max, n)));
            }
            case "select":
            {
                var s = raw is null ? "" : S(raw);
                var o = f["options"]?.AsArray().FirstOrDefault(x => S(x) == s);
                return o is null ? f["default"]?.DeepClone() : o.DeepClone();
            }
            default:
                return raw?.DeepClone();
        }
    }

    /// <summary>onInput/onChange: the value set at the path, coerced by its field.</summary>
    /// <returns>false when the value is the one already there (no step, nothing to save).</returns>
    public bool Set(string path, JsonNode? raw, string? key = null)
    {
        if (path.StartsWith("fx|", StringComparison.Ordinal)) return SetFree(path, raw, key);
        var f = FieldAt(path);
        var value = Coerce(f, raw);
        if (JsonNode.DeepEquals(Get(path), value)) return false;
        if (key is null) Break();
        Commit(sp => SetAt(sp, path, value), key);
        return true;
    }

    /// <summary>The value of a free element's field (content, or the style of the bucket).</summary>
    private JsonNode? GetFree(string path)
    {
        var q = path.Split('|');
        if (q.Length < 5 || FreeRoot(Spec, q[1]) is not { } root || PageEngine.FreeFind(root, q[2]) is not { } hit) return null;
        return hit.Node[q[3]] is JsonObject b ? b[q[4]] : null;
    }

    /// <summary>listOp(path, op, k): add (with the item's defaults), del, dup, up, down.</summary>
    public void ListOp(string path, string op, int k = 0)
    {
        var f = FieldAt(path);
        Break();
        Commit(sp =>
        {
            var arr = GetIn(sp, path) as JsonArray;
            if (arr is null) { arr = []; SetAt(sp, path, arr); }
            switch (op)
            {
                case "add":
                {
                    var item = new JsonObject();
                    foreach (var sf in (f?["fields"] as JsonArray ?? []).OfType<JsonObject>())
                        item[(string)sf["key"]!] = sf["default"] is { } d ? d.DeepClone() : (string?)sf["type"] == "boolean" ? false : "";
                    arr.Add(item);
                    break;
                }
                case "del" when k >= 0 && k < arr.Count: arr.RemoveAt(k); break;
                case "dup" when k >= 0 && k < arr.Count: arr.Insert(k + 1, arr[k]!.DeepClone()); break;
                case "up" when k > 0 && k < arr.Count: { var x = arr[k]!; arr.RemoveAt(k); arr.Insert(k - 1, x); break; }
                case "down" when k >= 0 && k < arr.Count - 1: { var x = arr[k]!; arr.RemoveAt(k); arr.Insert(k + 1, x); break; }
            }
        });
    }

    private static JsonNode? GetIn(JsonObject root, string path)
    {
        JsonNode? cur = root;
        foreach (var k in path.Split('.'))
        {
            cur = cur switch { JsonObject o => o[k], JsonArray a when int.TryParse(k, out var i) && i < a.Count => a[i], _ => null };
            if (cur is null) return null;
        }
        return cur;
    }

    // ---------------------------------------------------------------- templates and JSON

    /// <summary>The page is still "empty" (no sections, or the one free section of a new page): a template replaces it without asking.</summary>
    public bool IsEmpty => Sections.Count == 0 || Sections.Count == 1 && Sections[0] is JsonObject s && (string?)s["type"] == "free" &&
        (s["props"]?["root"]?.ToJsonString().Length ?? 0) < 420;

    private static JsonArray Reid(JsonArray list) => new(list.Select(x =>
    {
        var c = (JsonObject)x!.DeepClone();
        c["id"] = PageEngine.NewSectionId();
        if ((string?)c["type"] == "free" && c["props"]?["root"] is JsonObject root) c["props"]!["root"] = PageEngine.FreeClone(root);
        return (JsonNode)c;
    }).ToArray());

    /// <summary>applyTemplate: "replace" (sections, theme and structure; title kept), "append" (its sections at the end) or "theme" (only the look).</summary>
    public void ApplyTemplate(JsonObject spec, string mode)
    {
        Break();
        Commit(s =>
        {
            if (mode == "replace")
            {
                var keepTitle = s["meta"]?["title"]?.DeepClone();
                s["theme"] = spec["theme"]!.DeepClone();
                s["layout"] = spec["layout"]!.DeepClone();
                s["sections"] = Reid((JsonArray)spec["sections"]!);
                var meta = (JsonObject)spec["meta"]!.DeepClone();
                meta["title"] = keepTitle;
                s["meta"] = meta;
            }
            else if (mode == "append")
                foreach (var x in Reid((JsonArray)spec["sections"]!).ToList()) { var copy = x!.DeepClone(); ((JsonArray)s["sections"]!).Add(copy); }
            else s["theme"] = spec["theme"]!.DeepClone();
        });
        Selected = null;
        NodeSelected = null;
    }

    /// <summary>The text of the JSON panel: compact, 2 spaces.</summary>
    public string Json() => LegacyCustomize.Stringify(_engine.Compact(Spec), 2);

    /// <summary>validateJson + applyJson: with errors nothing changes; else the page becomes it (one undo step).</summary>
    public PageNormalized ApplyJson(string text)
    {
        var n = _engine.Normalize(text);
        if (n.Errors.Count > 0) return n;
        Break();
        Commit(s =>
        {
            foreach (var k in s.Select(p => p.Key).ToList()) s.Remove(k);
            foreach (var (k, v) in n.Spec) s[k] = v?.DeepClone();
        });
        if (Selected is not null && Section(Selected) is null) Selected = null;
        return n;
    }

    // ---------------------------------------------------------------- free layout (fx…)

    /// <summary>"desktop" edits the base style; "tablet"/"mobile" only what changes on that size and smaller.</summary>
    public string Device { get; set; } = "desktop";
    public string Bucket => Device == "mobile" ? "mobile" : Device == "tablet" ? "tablet" : "style";

    public static FreeNodeLabel FreeLabel(JsonObject n)
    {
        var t = PageEngine.FreeElement((string?)n["type"]);
        var c = n["content"] as JsonObject ?? [];
        string text = "";
        foreach (var k in new[] { "text", "label", "emoji", "caption", "alt", "path" })
            if (Truthy(c[k])) { text = S(c[k]); break; }
        if (text.Length == 0 && Truthy(c["items"])) text = S(c["items"]).Split('\n')[0];
        text = Regex.Replace(text, @"[*_`#>\[\]]", "").Trim();
        return new FreeNodeLabel(t?.Icon ?? "?", t?.Label ?? (string?)n["type"] ?? "", text.Length > 34 ? text[..32] + "…" : text);
    }

    /// <summary>fxCurrent: the selected element of this layout, or its root.</summary>
    public (JsonObject Node, JsonObject? Parent) FreeCurrent(JsonObject root) =>
        PageEngine.FreeFind(root, NodeSelected) ?? (root, null);

    private JsonObject? FreeRoot(JsonObject spec, string secId) =>
        ((JsonArray)spec["sections"]!).OfType<JsonObject>().FirstOrDefault(x => (string?)x["id"] == secId)?["props"]?["root"] as JsonObject;

    /// <summary>The path of a free element's field: fx|section|node|bucket|key ("content" for its content).</summary>
    public static string FreePath(string sec, string node, string bucket, string key) => "fx|" + sec + "|" + node + "|" + bucket + "|" + key;

    /// <summary>fxField: the content field of the element or the style field.</summary>
    public JsonObject? FreeField(string path)
    {
        var q = path.Split('|');
        if (q.Length < 5 || FreeRoot(Spec, q[1]) is not { } root || PageEngine.FreeFind(root, q[2]) is not { } hit) return null;
        if (q[3] == "content")
            return PageEngine.FreeElement((string?)hit.Node["type"])?.Content.OfType<JsonObject>().FirstOrDefault(f => (string?)f["key"] == q[4]);
        return PageEngine.FreeStyleFields.OfType<JsonObject>().FirstOrDefault(f => (string?)f["key"] == q[4]);
    }

    /// <summary>fxSet: content always set; a style key emptied is removed (and an empty bucket too).</summary>
    private bool SetFree(string path, JsonNode? raw, string? key)
    {
        var value = Coerce(FreeField(path), raw);
        var current = GetFree(path);
        if (JsonNode.DeepEquals(current, value) || current is null && value is JsonValue e && e.TryGetValue<string>(out var es) && es.Length == 0 && !path.Contains("|content|", StringComparison.Ordinal)) return false;
        if (key is null) Break();
        Commit(s =>
        {
            var q = path.Split('|');
            if (FreeRoot(s, q[1]) is not { } root || PageEngine.FreeFind(root, q[2]) is not { } hit) return;
            var n = hit.Node;
            if (q[3] == "content")
            {
                if (n["content"] is not JsonObject c) n["content"] = c = [];
                c[q[4]] = value;
                return;
            }
            if (n[q[3]] is not JsonObject b) n[q[3]] = b = [];
            if (value is null || value is JsonValue v && v.TryGetValue<string>(out var str) && str.Length == 0) b.Remove(q[4]);
            else b[q[4]] = value;
            if (b.Count == 0) n.Remove(q[3]);
        }, key);
        return true;
    }

    /// <summary>fxOp: up, down, out, in, wrap, dup, del, reset; add (an element type) inside a container or after the element.</summary>
    public void FreeOp(string secId, string op, string? type = null)
    {
        var sec = Section(secId);
        if (sec?["props"]?["root"] is not JsonObject root0) return;
        var id = (string)FreeCurrent(root0).Node["id"]!;
        Break();
        if (op is "add" or "preset")
        {
            var nn = op == "add" ? PageEngine.MakeFree(type ?? "text") : PageEngine.BuildPreset(type ?? "");
            if (nn is null) return;
            Commit(s =>
            {
                var h = PageEngine.FreeFind(FreeRoot(s, secId), id);
                if (h is null) return;
                if (PageEngine.FreeElement((string?)h.Value.Node["type"])?.Box == true) ((JsonArray)h.Value.Node["children"]!).Add(nn);
                else if (h.Value.Parent?["children"] is JsonArray arr) arr.Insert(arr.IndexOf(h.Value.Node) + 1, nn);
            });
            NodeSelected = (string)nn["id"]!;
            return;
        }
        if (op == "reset")
        {
            var bucket = Bucket;
            Commit(s => PageEngine.FreeFind(FreeRoot(s, secId), id)?.Node.Remove(bucket));
            return;
        }
        if (PageEngine.FreeFind(root0, id) is not { Parent: not null }) return;
        Commit(s =>
        {
            var r = FreeRoot(s, secId)!;
            var h = PageEngine.FreeFind(r, id)!.Value;
            var n = h.Node;
            var arr = (JsonArray)h.Parent!["children"]!;
            int i = arr.IndexOf(n);
            switch (op)
            {
                case "up" when i > 0: arr.RemoveAt(i); arr.Insert(i - 1, n); break;
                case "down" when i < arr.Count - 1: arr.RemoveAt(i); arr.Insert(i + 1, n); break;
                case "out":
                    if (PageEngine.FreeFind(r, (string?)h.Parent["id"])?.Parent is { } gp && gp["children"] is JsonArray ga)
                    {
                        arr.RemoveAt(i);
                        ga.Insert(ga.IndexOf(h.Parent) + 1, n);
                    }
                    break;
                case "in":
                    if (i > 0 && arr[i - 1] is JsonObject pv && PageEngine.FreeElement((string?)pv["type"])?.Box == true)
                    {
                        arr.RemoveAt(i);
                        ((JsonArray)pv["children"]!).Add(n);
                    }
                    break;
                case "wrap":
                {
                    arr.RemoveAt(i);
                    var box = PageEngine.MakeFree("box", [], [], new JsonArray(n));
                    arr.Insert(i, box);
                    break;
                }
                case "dup":
                {
                    var cp = PageEngine.FreeClone(n);
                    arr.Insert(i + 1, cp);
                    NodeSelected = (string)cp["id"]!;
                    break;
                }
                case "del":
                    arr.RemoveAt(i);
                    NodeSelected = (string?)h.Parent["id"];
                    break;
            }
        });
    }

    /// <summary>insertPreset: inside the selected free layout (in a container or after the element), else in a new free section.</summary>
    public void InsertPreset(string key)
    {
        var nn = PageEngine.BuildPreset(key);
        if (nn is null) return;
        if (SelectedSection is { } sec && (string?)sec["type"] == "free" && sec["props"]?["root"] is JsonObject)
        {
            NodeSelected ??= null;
            var secId = (string)sec["id"]!;
            FreeOp(secId, "preset", key);
            return;
        }
        var ns = _engine.NewSection("free", new JsonObject { ["root"] = PageEngine.MakeFree("box", [], new JsonObject { ["gap"] = "16px" }, new JsonArray(nn)) });
        int i = Selected is not null ? IndexOf(Selected) + 1 : Sections.Count;
        Break();
        Commit(s => ((JsonArray)s["sections"]!).Insert(i, ns));
        Select((string)ns["id"]!);
        NodeSelected = (string)nn["id"]!;
    }

    /// <summary>quickPage(): the new page of "Nova página" — a free layout ready to write in.</summary>
    public static JsonObject QuickPage(PageEngine engine) => engine.Normalize(new JsonObject
    {
        ["meta"] = new JsonObject { ["title"] = "Nova página", ["icon"] = "✦" },
        ["theme"] = new JsonObject { ["preset"] = "aurora" },
        ["layout"] = new JsonObject { ["nav"] = false, ["footer"] = "" },
        ["sections"] = new JsonArray(new JsonObject
        {
            ["type"] = "free",
            ["props"] = new JsonObject
            {
                ["root"] = PageEngine.MakeFree("box", [], new JsonObject { ["gap"] = "18px", ["padding"] = "32px 0" }, new JsonArray(
                    PageEngine.MakeFree("heading", new JsonObject { ["text"] = "Nova página", ["level"] = 1 }),
                    PageEngine.MakeFree("text", new JsonObject { ["text"] = "Toque aqui para escrever. Use **＋** para inserir blocos, composições prontas ou carregar um modelo." })))
            }
        })
    }).Spec;

    /// <summary>cleanName: what a file name may hold (80 characters).</summary>
    public static string CleanName(string? s)
    {
        var t = Regex.Replace((s ?? "").Trim(), "[\\\\/:*?\"<>|\u0000-\u001f]", "-");
        t = Regex.Replace(t, @"^\.+|\.+$", "");
        return t.Length > 80 ? t[..80] : t;
    }

    /// <summary>uniquePath(base): "x (2).page.json", "x (3)…" while the path is taken.</summary>
    public static string UniquePath(string path, Func<string, bool> exists)
    {
        var p = path;
        int i = 2;
        while (exists(p)) p = Regex.Replace(path, @"(\.(page|template|block)\.json)$", " (" + (i++).ToString(CultureInfo.InvariantCulture) + ")$1", RegexOptions.IgnoreCase);
        return p;
    }

    /// <summary>renameToTitle(before): the file follows the title while its name was still automatic or the previous title.</summary>
    public static string? RenameTarget(string path, string title, string before, Func<string, bool> exists)
    {
        var t = CleanName(title);
        if (t.Length == 0) return null;
        var dir = path[..(path.LastIndexOf('/') + 1)];
        var file = path[dir.Length..];
        var name = Regex.Replace(file, @"\.(page|template)\.json$", "", RegexOptions.IgnoreCase);
        var ext = file[name.Length..];
        if (name == t || !(Regex.IsMatch(name, @"^Nova página( \(\d+\))?$") || name == CleanName(before))) return null;
        var target = dir + t + ext;
        return exists(target) ? UniquePath(target, exists) : target;
    }
}
