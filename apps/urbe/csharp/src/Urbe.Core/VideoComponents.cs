using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// #1004 slice 5: reusable components. <c>video.components</c> names groups of elements made once; a scene element of
/// kind "use" places one (its x, y, scale, rotation, opacity, keyframes and motions move the whole component, as a
/// group) and fills its <c>{{parameters}}</c>. The project keeps the component once; the timeline and the review see it
/// expanded, so a component is drawn and checked exactly like elements written by hand.
/// </summary>
public static partial class VideoComponents
{
    public const int MaxComponents = 64, MaxElements = 500;

    /// <summary>The composition with every "use" replaced by a group and the component's elements under it.</summary>
    public static VideoComposition Expand(VideoComposition video)
    {
        ArgumentNullException.ThrowIfNull(video);
        bool any = video.Scenes?.Any(s => s?.Elements?.Any(e => e?.Kind == "use") == true) == true;
        var components = video.Components;
        if (components is not null)
        {
            if (components.Count > MaxComponents) throw Bad(components.Count + " componentes; até " + MaxComponents);
            foreach (var (name, elements) in components)
            {
                if (!NameRe().IsMatch(name ?? "")) throw Bad("o nome de componente \"" + name + "\" usa letras minúsculas, números, - e _");
                if (elements is null || elements.Count == 0 || elements.Count > MaxElements) throw Bad("o componente " + name + " precisa de 1 a " + MaxElements + " elementos");
                if (elements.Any(e => e is null || e.Kind == "use")) throw Bad("o componente " + name + " não pode usar outro componente");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in elements) if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id)) throw Bad("os elementos do componente " + name + " precisam de ids únicos");
            }
        }
        if (!any) return video;
        return video with { Scenes = video.Scenes!.Select(scene => scene is null ? scene! : ExpandScene(scene, components)).ToList() };
    }

    /// <summary>The scene with each "use" replaced by a group and the component's elements under it.</summary>
    public static VideoScene ExpandScene(VideoScene scene, Dictionary<string, List<VideoElement>>? components) =>
        scene.Elements is null || !scene.Elements.Any(e => e?.Kind == "use") ? scene : scene with
        {
            Elements = scene.Elements.SelectMany(e => e?.Kind == "use" ? Place(e, components) : [e!]).ToList()
        };

    private static IEnumerable<VideoElement> Place(VideoElement use, Dictionary<string, List<VideoElement>>? components)
    {
        if (use.Component is not { } name || components is null || !components.TryGetValue(name, out var parts))
            throw Bad("o elemento " + use.Id + " usa o componente \"" + use.Component + "\", que não existe em video.components" +
                      (components is { Count: > 0 } ? " (há: " + string.Join(", ", components.Keys) + ")" : ""));
        var values = use.Params ?? [];
        string? Fill(string? text) => text is null ? null : ParamRe().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        string Prefixed(string id) => use.Id + "." + id;
        yield return use with { Kind = "group", Component = null, Params = null, Text = null };
        foreach (var e in parts)
            yield return e with
            {
                Id = Prefixed(e.Id),
                Parent = e.Parent is { } p ? Prefixed(p) : use.Id,
                Clip = e.Clip is { } c ? Prefixed(c) : null,
                Text = Fill(e.Text),
                Source = Fill(e.Source),
                Start = use.Start + e.Start,
                End = e.End is { } end ? use.Start + end : use.End,
            };
    }

    private static InvalidDataException Bad(string why) => new("Vídeo inválido: " + why);

    [GeneratedRegex(@"^[a-z0-9_-]{1,40}$")] private static partial Regex NameRe();
    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")] private static partial Regex ParamRe();
}
