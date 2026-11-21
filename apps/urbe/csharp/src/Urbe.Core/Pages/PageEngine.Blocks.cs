using System.Text.Json.Nodes;

namespace Urbe.Core.Pages;

/// <summary>A block of the engine (BLOCKS[type]): its label, icon, group, description and the fields of its props.</summary>
public sealed record PageBlockInfo(string Type, string Label, string Icon, string Group, string Description, JsonArray Fields);

/// <summary>
/// What the rest of the app reads about the blocks of an old page (CST-061: the page studio that also read the
/// fields, themes and free-layout presets from here is gone; pages are shown, exported or converted).
/// </summary>
public sealed partial class PageEngine
{
    public PageBlockInfo? BlockInfo(string? type) =>
        type is not null && _blocks.TryGetValue(type, out var b) ? new PageBlockInfo(b.Type, b.Label, b.Icon, b.Group, b.Description, b.Fields) : null;

    /// <summary>UrbeMath: draws a formula (tex, display) as HTML; null (the default, and the oracles) leaves $…$ as text.</summary>
    public Func<string, bool, string>? MathHtml { get; set; }
}
