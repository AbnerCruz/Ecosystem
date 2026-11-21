namespace Urbe.Core;

/// <summary>
/// #956 (owner, 19/11/2026: "o que ficou pronto do vídeo tava simplesmente ruim"): composed scene layouts. Each one
/// places its parts on a grid that stays inside the safe area and above the subtitles, with entrances that come in one
/// after the other (fade and rise, lines and plots drawn). The Assistente or the person only gives the content; colours
/// come from the video's theme. The narration stays in the scene's note.
/// </summary>
public static class VideoLayouts
{
    public sealed record Layout(string Id, string Label, string Description, string[] Required, string[] Optional);

    public static readonly IReadOnlyList<Layout> All =
    [
        new("abertura", "Abertura", "Título grande no centro, sublinhado que se desenha e subtítulo", ["titulo"], ["subtitulo"]),
        new("topico", "Tópico", "Título no alto e um texto curto com uma barra de destaque", ["titulo", "texto"], []),
        new("comparacao", "Comparação", "Título e dois cartões lado a lado, com uma seta entre eles", ["titulo", "esquerda", "direita"], []),
        new("numero", "Número em destaque", "Um número grande e a legenda dele", ["numero", "legenda"], ["titulo"]),
        new("formula", "Fórmula", "Título, uma fórmula TeX no centro e uma nota", ["formula"], ["titulo", "nota"]),
        new("grafico", "Gráfico", "Título e o gráfico de uma função que se traça durante a cena", ["grafico"], ["titulo", "nota"]),
        new("citacao", "Citação", "Uma frase no centro, entre aspas, e o autor", ["texto"], ["autor"]),
    ];

    public static Layout? Find(string? id) => All.FirstOrDefault(l => l.Id == id);

    /// <summary>The elements of <paramref name="layout"/> with <paramref name="slots"/>; ids start with <paramref name="prefix"/>.</summary>
    public static List<VideoElement> Build(string layout, IReadOnlyDictionary<string, string> slots, string prefix)
    {
        var l = Find(layout) ?? throw new ArgumentException("Layout desconhecido: " + layout + ". Layouts: " + string.Join(", ", All.Select(x => x.Id)) + ".");
        var missing = l.Required.Where(s => !slots.TryGetValue(s, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        if (missing.Count > 0) throw new ArgumentException("O layout " + l.Id + " precisa de: " + string.Join(", ", missing) + ".");
        string? Slot(string name) => slots.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        var list = new List<VideoElement>();
        // every part comes in after the one before: a fade with a short rise, 0.25 s apart
        double at = 0.2;
        VideoElement Enter(VideoElement e, double rise = 0.03, double opacity = 1)
        {
            var t = at;
            at += 0.25;
            return e with
            {
                Id = prefix + "_" + e.Id,
                Keyframes =
                [
                    new() { Property = "opacity", Time = t, Value = 0 }, new() { Property = "opacity", Time = t + 0.5, Value = opacity, Easing = "easeOut" },
                    new() { Property = "y", Time = t, Value = e.Y + rise }, new() { Property = "y", Time = t + 0.5, Value = e.Y, Easing = "easeOut" },
                    .. e.Keyframes
                ]
            };
        }
        VideoElement Draw(VideoElement e, double seconds)
        {
            var t = at;
            at += 0.25;
            return e with
            {
                Id = prefix + "_" + e.Id,
                Keyframes = [new() { Property = "progress", Time = t, Value = 0 }, new() { Property = "progress", Time = t + seconds, Value = 1, Easing = "easeInOut" }]
            };
        }
        VideoElement Title(double y, double size = 0.075) => new() { Id = "titulo", Kind = "title", Text = Slot("titulo"), X = 0.5, Y = y, Width = 0.84, Height = size * 2, FontSize = size };

        switch (l.Id)
        {
            case "abertura":
                list.Add(Enter(Title(0.4, 0.1)));
                list.Add(Draw(new VideoElement { Id = "sublinhado", Kind = "path", Text = "M 0 12 C 30 2, 70 22, 100 8", X = 0.5, Y = 0.53, Width = 0.32, Height = 0.04, FontSize = 0.04 }, 0.7));
                if (Slot("subtitulo") is { } sub) list.Add(Enter(new VideoElement { Id = "subtitulo", Kind = "text", Text = sub, X = 0.5, Y = 0.63, Width = 0.7, Height = 0.1, FontSize = 0.045 }));
                break;
            case "topico":
                list.Add(Enter(Title(0.18)));
                list.Add(Enter(new VideoElement { Id = "barra", Kind = "shape", Text = "rect", X = 0.12, Y = 0.47, Width = 0.012, Height = 0.3 }, 0));
                list.Add(Enter(new VideoElement { Id = "texto", Kind = "text", Text = Slot("texto"), X = 0.52, Y = 0.47, Width = 0.72, Height = 0.32, FontSize = 0.05 }));
                break;
            case "comparacao":
                list.Add(Enter(Title(0.16)));
                // the cards are a light veil of the theme's diagram colour: the text on them keeps its contrast
                list.Add(Enter(new VideoElement { Id = "cartao_e", Kind = "shape", Text = "rect", X = 0.27, Y = 0.5, Width = 0.36, Height = 0.36 }, 0.02, 0.18));
                list.Add(Enter(new VideoElement { Id = "esquerda", Kind = "text", Text = Slot("esquerda"), X = 0.27, Y = 0.5, Width = 0.3, Height = 0.3, FontSize = 0.045 }, 0.02));
                list.Add(Draw(new VideoElement { Id = "seta", Kind = "path", Text = "M 0 50 L 90 50 M 66 26 L 90 50 L 66 74", X = 0.5, Y = 0.5, Width = 0.07, Height = 0.08, FontSize = 0.04 }, 0.5));
                list.Add(Enter(new VideoElement { Id = "cartao_d", Kind = "shape", Text = "rect", X = 0.73, Y = 0.5, Width = 0.36, Height = 0.36 }, 0.02, 0.18));
                list.Add(Enter(new VideoElement { Id = "direita", Kind = "text", Text = Slot("direita"), X = 0.73, Y = 0.5, Width = 0.3, Height = 0.3, FontSize = 0.045 }, 0.02));
                break;
            case "numero":
                if (Slot("titulo") is not null) list.Add(Enter(Title(0.17, 0.06)));
                list.Add(Enter(new VideoElement { Id = "numero", Kind = "title", Text = Slot("numero"), X = 0.5, Y = 0.43, Width = 0.8, Height = 0.3, FontSize = 0.16 } with { Keyframes = [new() { Property = "scale", Time = at, Value = 0.7 }, new() { Property = "scale", Time = at + 0.6, Value = 1, Easing = "back" }] }, 0));
                list.Add(Enter(new VideoElement { Id = "legenda", Kind = "text", Text = Slot("legenda"), X = 0.5, Y = 0.64, Width = 0.7, Height = 0.12, FontSize = 0.045 }));
                break;
            case "formula":
                if (Slot("titulo") is not null) list.Add(Enter(Title(0.17)));
                list.Add(Enter(new VideoElement { Id = "formula", Kind = "math", Text = Slot("formula"), X = 0.5, Y = 0.45, Width = 0.7, Height = 0.22 }, 0.02));
                if (Slot("nota") is { } nota) list.Add(Enter(new VideoElement { Id = "nota", Kind = "text", Text = nota, X = 0.5, Y = 0.66, Width = 0.7, Height = 0.1, FontSize = 0.04 }));
                break;
            case "grafico":
                if (Slot("titulo") is not null) list.Add(Enter(Title(0.15, 0.065)));
                list.Add(Draw(new VideoElement { Id = "grafico", Kind = "plot", Text = Slot("grafico"), X = 0.5, Y = 0.46, Width = 0.62, Height = 0.4 }, 2.5));
                if (Slot("nota") is { } n) list.Add(Enter(new VideoElement { Id = "nota", Kind = "text", Text = n, X = 0.5, Y = 0.72, Width = 0.7, Height = 0.08, FontSize = 0.038 }));
                break;
            case "citacao":
                list.Add(Enter(new VideoElement { Id = "aspas", Kind = "title", Text = "“", X = 0.5, Y = 0.22, Width = 0.2, Height = 0.16, FontSize = 0.14 }, 0));
                list.Add(Enter(new VideoElement { Id = "texto", Kind = "text", Text = Slot("texto"), X = 0.5, Y = 0.43, Width = 0.76, Height = 0.24, FontSize = 0.055 }));
                if (Slot("autor") is { } autor) list.Add(Enter(new VideoElement { Id = "autor", Kind = "text", Text = "— " + autor, X = 0.5, Y = 0.63, Width = 0.6, Height = 0.08, FontSize = 0.04 }));
                break;
        }
        return list;
    }
}
