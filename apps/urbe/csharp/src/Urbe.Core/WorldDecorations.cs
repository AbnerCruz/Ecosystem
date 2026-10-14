namespace Urbe.Core;

/// <summary>A decoration placed in the city (DEC-0045, UC-41): its kind and the tile of its top-left corner.</summary>
public sealed record WorldDecoration(string Id, string Kind, int X, int Y);

/// <summary>
/// The decorations the "+" menu places in the city (owner, DEC-0045: "as estruturas estão muito feias, falta decoração").
/// Drawn as pixel art with the same grid-and-palette method as the 1.8.4's sprites (pixel-art.js, <see cref="LegacyLifeSprites"/>),
/// 12 grid cells per tile. They are kept in .urbe/mundo.json (<see cref="WorldPrefs"/>), never in mapa.json, and are also
/// the places where the residents' trades will happen (UC-42: the well, the field, the woodpile).
/// </summary>
public static class WorldDecorations
{
    /// <summary>Grid cells per tile.</summary>
    public const int CellsPerTile = 12;

    public sealed record Kind(string Id, string Label, string Hint, int W, int H, LegacyLifeSprites.Grid Sprite);

    public static IReadOnlyList<Kind> Kinds { get; } =
    [
        new("poco", "Poço", "Água para os moradores", 1, 1, new([[
            "....rrrr....",
            "...rRRRRr...",
            "..rRRRRRRr..",
            "....p..p....",
            "....p..p....",
            "....p..p....",
            "..ssssssss..",
            ".sSwwwwwwSs.",
            ".sSwwwwwwSs.",
            ".ssssssssss.",
            "..SSSSSSSS..",
            "............"]],
            Pal("r#8a4f2c", "R#b0683a", "p#5b3a22", "s#9a948a", "S#6e695f", "w#4a8fc4"))),
        new("roca", "Roça", "Plantação cercada", 2, 1, new([Field()],
            Pal("f#8a6a44", "d#6b4a2e", "g#5fa043", "G#8fcf5e"))),
        new("lenha", "Lenha", "Pilha de lenha cortada", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            "..oOoOoOo...",
            ".oOWOWOWOo..",
            ".OWbWbWbWO..",
            ".oOoOoOoOo..",
            ".OWbWbWbWOo.",
            ".oOoOoOoOoO.",
            "..kkkkkkkkk.",
            "............"]],
            Pal("o#7a4a26", "O#9a6234", "W#d8b07a", "b#a77a46", "k#3a2a1c"))),
        new("banco", "Banco", "Para descansar na praça", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            "............",
            "..bbbbbbbb..",
            "..BBBBBBBB..",
            "............",
            "..bbbbbbbb..",
            "..k......k..",
            "..k......k..",
            "............"]],
            Pal("b#a0703f", "B#7a5230", "k#3b2b1e"))),
        new("fonte", "Fonte", "Água no centro do bairro", 1, 1, new([[
            ".....ww.....",
            "....wWWw....",
            ".....ww.....",
            ".....ss.....",
            "..ssssssss..",
            ".sWwwwwwwWs.",
            ".swwwWWwwws.",
            ".sWwwwwwwWs.",
            "..ssssssss..",
            "...SSSSSS...",
            "............",
            "............"]],
            Pal("s#a8a294", "S#6e695f", "w#5aa0d6", "W#bfe3f7"))),
        new("flores", "Canteiro", "Flores na frente das casas", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            "..r..y..p...",
            ".rRr.yYy.pPp",
            "..r.gy.gp.g.",
            ".g.g.g.g.g..",
            ".gggggggggg.",
            ".dddddddddd.",
            "............",
            "............"]],
            Pal("r#d9483b", "R#f4d35e", "y#f7d046", "Y#ffffff", "p#c879d6", "P#f4f0e6", "g#4f8f3a", "d#6b4a2e"))),
    ];

    public static Kind? Find(string id) => Kinds.FirstOrDefault(k => k.Id == id);

    /// <summary>The tiles a decoration of this kind covers when its corner is at (x, y).</summary>
    public static IEnumerable<(int X, int Y)> Footprint(Kind kind, int x, int y)
    {
        for (int dy = 0; dy < kind.H; dy++)
            for (int dx = 0; dx < kind.W; dx++)
                yield return (x + dx, y + dy);
    }

    // the field: a fence around rows of soil and sprouts, two tiles wide
    private static string[] Field()
    {
        int w = 2 * CellsPerTile, inner = w - 4;
        string Row(char edge, string fill) => "." + edge + fill + edge + ".";
        var rows = new List<string> { new('.', w), "." + new string('f', w - 2) + "." };
        for (int i = 0; i < 8; i++)
        {
            if (i % 2 == 1) { rows.Add(Row('f', new string('d', inner))); continue; }
            var sprouts = string.Concat(Enumerable.Range(0, inner).Select(c => (c + i / 2) % 2 == 0 ? 'g' : 'G'));
            rows.Add(Row('f', sprouts));
        }
        rows.Add("." + new string('f', w - 2) + ".");
        rows.Add(new string('.', w));
        return rows.ToArray();
    }

    private static Dictionary<char, string> Pal(params string[] entries) => entries.ToDictionary(e => e[0], e => e[1..]);
}
