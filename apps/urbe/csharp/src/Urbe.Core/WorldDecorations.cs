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
        // UC-53: more kinds and variations (owner: "adicionar variações … uma diversidade maior")
        new("flores-amarelas", "Canteiro amarelo", "Girassóis e margaridas", 1, 1, new([[
            "............",
            "............",
            "............",
            "..y...y...y.",
            ".yYy.yYy.yYy",
            "..y.g.y.g.y.",
            ".g.g.g.g.g..",
            ".gggggggggg.",
            ".gggggggggg.",
            ".dddddddddd.",
            "............",
            "............"]],
            Pal("y#f2b632", "Y#7a4a1c", "g#4f8f3a", "d#6b4a2e"))),
        new("flores-azuis", "Canteiro azul", "Hortênsias e lavandas", 1, 1, new([[
            "............",
            "............",
            "............",
            "..b..l..b...",
            ".bBb.lLl.bBb",
            "..b.gl.gb.g.",
            ".g.g.g.g.g..",
            ".gggggggggg.",
            ".gggggggggg.",
            ".dddddddddd.",
            "............",
            "............"]],
            Pal("b#5b8fd9", "B#cfe2ff", "l#9a6fd0", "L#e6dbff", "g#4f8f3a", "d#6b4a2e"))),
        new("arbusto", "Arbusto", "Verde baixo e redondo", 1, 1, new([[
            "............",
            "............",
            "............",
            "....gGGg....",
            "..gGGgGGGg..",
            ".gGGgGGgGGg.",
            ".gGgGGGGgGg.",
            ".ggGgGGgGgg.",
            "..gggggggg..",
            "...kkkkkk...",
            "............",
            "............"]],
            Pal("g#3f7a32", "G#5fa043", "k#2a3a22"))),
        new("lampiao", "Lampião", "Poste com lanterna", 1, 1, new([[
            ".....kk.....",
            "....kyyk....",
            "....yYYy....",
            "....kyyk....",
            ".....kk.....",
            ".....kk.....",
            ".....kk.....",
            ".....kk.....",
            ".....kk.....",
            "....kkkk....",
            "...kkkkkk...",
            "............"]],
            Pal("k#2f2a26", "y#f2c14e", "Y#fff3c4"))),
        new("cerca", "Cerca", "Madeira entre os quintais", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            ".p...p...p..",
            ".pbbbpbbbpbb",
            ".p...p...p..",
            ".pbbbpbbbpbb",
            ".p...p...p..",
            ".k...k...k..",
            "............",
            "............"]],
            Pal("p#8a5a32", "b#b07a46", "k#3b2b1e"))),
        new("barris", "Barris", "Água da chuva e vinho", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            "..oooo......",
            ".oOkkOo.....",
            ".oOOOOo.ooo.",
            ".okkkko.OkO.",
            ".oOOOOo.OOO.",
            ".oOkkOo.kkk.",
            "..oooo..ooo.",
            "............"]],
            Pal("o#5e3a1e", "O#9a6234", "k#3a3a3a"))),
        new("caixotes", "Caixotes", "Mercadoria empilhada", 1, 1, new([[
            "............",
            "............",
            "............",
            "...cccccc...",
            "...cCkkCc...",
            "...ckCCkc...",
            ".cccccccccc.",
            ".cCkkCcCkkC.",
            ".ckCCkcCCkc.",
            ".cCkkCcCkkC.",
            ".cccccccccc.",
            "............"]],
            Pal("c#7a5230", "C#c09060", "k#4a321c"))),
        new("carroca", "Carroça", "Carga para a feira", 2, 1, new([[
            "........................",
            "........................",
            "........................",
            "....gGgYgGg.............",
            "...bbbbbbbbbbbb.........",
            "...bBBBBBBBBBBb.........",
            "...bbbbbbbbbbbbkkkkkkk..",
            "....k..........k........",
            "...wWw......wWw.........",
            "..wkWkw....wkWkw........",
            "...wWw......wWw.........",
            "........................"]],
            Pal("b#8a5a32", "B#b07a46", "k#3b2b1e", "w#5e3a1e", "W#c8a070", "g#4f8f3a", "G#8fcf5e", "Y#f2c14e"))),
        new("placa", "Placa", "Para indicar o caminho", 1, 1, new([[
            "............",
            "............",
            "..bbbbbbbb..",
            "..bBBBBBBbb.",
            "..bBwwwwBbbb",
            "..bBBBBBBbb.",
            "..bbbbbbbb..",
            ".....kk.....",
            ".....kk.....",
            ".....kk.....",
            "....kkkk....",
            "............"]],
            Pal("b#7a5230", "B#c09060", "w#f4ecd8", "k#3b2b1e"))),
        new("varal", "Varal", "Roupas secando ao sol", 2, 1, new([[
            "........................",
            ".k....................k.",
            ".kllllllllllllllllllllk.",
            ".k.rr..bbb..yy...gg...k.",
            ".k.rr..bbb..yy..gggg..k.",
            ".k.rr..bbb.......gg...k.",
            ".k.....bbb............k.",
            ".k....................k.",
            ".k....................k.",
            ".k....................k.",
            "kkk..................kkk",
            "........................"]],
            Pal("k#5e3a1e", "l#d8d0c0", "r#d9483b", "b#5b8fd9", "y#f2c14e", "g#5fa043"))),
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
