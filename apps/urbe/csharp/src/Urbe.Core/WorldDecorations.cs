namespace Urbe.Core;

/// <summary>
/// A decoration placed in the city (DEC-0045, UC-41): its kind and the tile of its top-left corner; UC-53 adds its
/// variation (0 = the kind's first), whether it is mirrored and whether it is turned a quarter (kinds that allow it).
/// </summary>
public sealed record WorldDecoration(string Id, string Kind, int X, int Y, int Variant = 0, bool Flipped = false, bool Turned = false);

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

    /// <summary>A variation of a kind (UC-53): other colours (palette entries replaced) and/or another drawing.</summary>
    public sealed record Variation(string Label, IReadOnlyDictionary<char, string>? Palette = null, string[]? Rows = null);

    public sealed record Kind(string Id, string Label, string Hint, int W, int H, LegacyLifeSprites.Grid Sprite)
    {
        /// <summary>The variations; the first is the kind's own drawing (the default, also for files without one).</summary>
        public IReadOnlyList<Variation> Variations { get; init; } = [new("Padrão")];
        /// <summary>The drawing turned a quarter (a fence running north-south), or null when the kind does not turn.</summary>
        public string[]? TurnedRows { get; init; }
        /// <summary>Whether it can be mirrored (an arrow, a cart facing the other way).</summary>
        public bool Flips { get; init; }
        public bool Turns => TurnedRows is not null;
    }

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
            Pal("r#8a4f2c", "R#b0683a", "p#5b3a22", "s#9a948a", "S#6e695f", "w#4a8fc4"))) { Variations = [new("Telhado de madeira"), new("Telhado de palha", Pal("r#b8923e", "R#d8b85e"))] },
        new("roca", "Roça", "Plantação cercada", 2, 1, new([Field()],
            Pal("f#8a6a44", "d#6b4a2e", "g#5fa043", "G#8fcf5e"))) { Variations = [new("Verduras"), new("Trigo", Pal("g#c8a23a", "G#e8cc6a")), new("Abóboras", Pal("G#e07b2a"))] },
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
            Pal("o#7a4a26", "O#9a6234", "W#d8b07a", "b#a77a46", "k#3a2a1c"))) { Flips = true },
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
            Pal("b#a0703f", "B#7a5230", "k#3b2b1e"))) { Variations = [new("Madeira"), new("Escuro", Pal("b#5e3a22", "B#3e2614")), new("Pedra", Pal("b#a8a294", "B#7e786c", "k#5e594f"))] },
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
            Pal("s#a8a294", "S#6e695f", "w#5aa0d6", "W#bfe3f7"))) { Variations = [new("Pedra"), new("Musgo", Pal("s#8a9a7a", "S#5e6e4f"))] },
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
            Pal("r#d9483b", "R#f4d35e", "y#f7d046", "Y#ffffff", "p#c879d6", "P#f4f0e6", "g#4f8f3a", "d#6b4a2e"))) { Variations = [new("Vermelho e lilás"), new("Rosa", Pal("r#e86fa0", "R#ffd1e3", "y#f7b7d2", "p#f4a6c8")), new("Branco", Pal("r#f4f0e6", "R#f2c14e", "y#ffffff", "Y#f2c14e", "p#e8e4f4", "P#f2c14e"))] },
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
            Pal("y#f2b632", "Y#7a4a1c", "g#4f8f3a", "d#6b4a2e"))) { Variations = [new("Girassóis"), new("Laranja", Pal("y#e8742a"))] },
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
            Pal("b#5b8fd9", "B#cfe2ff", "l#9a6fd0", "L#e6dbff", "g#4f8f3a", "d#6b4a2e"))) { Variations = [new("Azul"), new("Lilás", Pal("b#b07ad9", "B#efe0ff"))] },
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
            Pal("g#3f7a32", "G#5fa043", "k#2a3a22"))) { Variations = [new("Verde"), new("Florido", Pal("f#f2a7c4"), FloweringBush), new("Outono", Pal("g#a0522d", "G#d98a3a"))] },
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
            Pal("k#2f2a26", "y#f2c14e", "Y#fff3c4"))) { Variations = [new("Ferro"), new("Bronze", Pal("k#7a4a22")), new("Baixo", null, LowLamp)] },
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
            Pal("p#8a5a32", "b#b07a46", "k#3b2b1e"))) { Variations = [new("Madeira"), new("Branca", Pal("p#d8d0c0", "b#f4f0e6", "k#8a857a")), new("Escura", Pal("p#4e3220", "b#6e4a2e"))], TurnedRows = FenceTurned },
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
            Pal("o#5e3a1e", "O#9a6234", "k#3a3a3a"))) { Variations = [new("Claros"), new("Escuros", Pal("O#6e4426"))], Flips = true },
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
            Pal("c#7a5230", "C#c09060", "k#4a321c"))) { Variations = [new("Madeira"), new("Claros", Pal("c#a07a4a", "C#e0c090"))], Flips = true },
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
            Pal("b#8a5a32", "B#b07a46", "k#3b2b1e", "w#5e3a1e", "W#c8a070", "g#4f8f3a", "G#8fcf5e", "Y#f2c14e"))) { Variations = [new("Feira"), new("Feno", Pal("g#c8962e", "G#f2d36b", "Y#d9b44a")), new("Vazia", null, EmptyCart)], Flips = true },
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
            Pal("b#7a5230", "B#c09060", "w#f4ecd8", "k#3b2b1e"))) { Variations = [new("Madeira"), new("Pintada", Pal("b#2f4f7a", "B#5b8fd9")), new("Seta", null, ArrowSign)], Flips = true },
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
            Pal("k#5e3a1e", "l#d8d0c0", "r#d9483b", "b#5b8fd9", "y#f2c14e", "g#5fa043"))) { Variations = [new("Colorido"), new("Branco", Pal("r#f4f0e6", "b#e8e4dc", "y#f4f0e6", "g#e8e4dc"))] },
        // UC-53: a few more kinds
        new("arvore-frutifera", "Árvore frutífera", "Laranjeira, macieira ou limoeiro", 1, 1, new([[
            "....gGGg....",
            "..gGoGGgGg..",
            ".gGGgGGoGGg.",
            ".gGoGGgGGGg.",
            ".gGGGgGoGgg.",
            ".ggGoGGGGgg.",
            "..ggggGggg..",
            "....gttg....",
            ".....tt.....",
            ".....tt.....",
            "....tttt....",
            "............"]],
            Pal("g#3f7a32", "G#5fa043", "o#f08a24", "t#6b4a2e"))) { Variations = [new("Laranjeira"), new("Macieira", Pal("o#d9483b")), new("Limoeiro", Pal("o#f2e04e"))] },
        new("pedras", "Pedras", "Pedras soltas no chão", 1, 1, new([[
            "............",
            "............",
            "............",
            "............",
            "............",
            "......sss...",
            "..ss.sSSSs..",
            ".sSSssSSSSs.",
            ".sSSSsssssk.",
            ".kssskkkkk..",
            "..kkk.......",
            "............"]],
            Pal("s#8a857a", "S#b8b2a4", "k#4e4a42"))) { Variations = [new("Cinza"), new("Musgo", Pal("S#8fa070")), new("Areia", Pal("s#b89a6a", "S#e0c89a", "k#7a6040"))], Flips = true },
        new("espantalho", "Espantalho", "Guarda a roça dos pássaros", 1, 1, new([[
            "....hhhh....",
            "...HHHHHH...",
            ".....ff.....",
            ".....ff.....",
            ".ccccccccc..",
            "..c.cCCc.c..",
            ".....cCc....",
            ".....cCc....",
            ".....pp.....",
            ".....pp.....",
            "....pppp....",
            "............"]],
            Pal("h#c8a050", "H#8a6a30", "f#e8c89a", "c#c84a3a", "C#4a6ab0", "p#6b4a2e"))) { Variations = [new("Vermelho"), new("Azul", Pal("c#4a6ab0", "C#c84a3a"))], Flips = true },
    ];

    public static Kind? Find(string id) => Kinds.FirstOrDefault(k => k.Id == id);

    /// <summary>The variation index to use: an unknown one (out of range) is the default.</summary>
    public static int VariantOf(Kind kind, int variant) => variant >= 0 && variant < kind.Variations.Count ? variant : 0;

    /// <summary>The drawing of a decoration of <paramref name="kind"/> in a variation, turned or not (mirroring is done when rendering).</summary>
    public static LegacyLifeSprites.Grid Sprite(Kind kind, int variant, bool turned)
    {
        var v = kind.Variations[VariantOf(kind, variant)];
        var rows = turned && kind.TurnedRows is { } t ? t : v.Rows ?? kind.Sprite.Frames[0];
        if (v.Palette is null && ReferenceEquals(rows, kind.Sprite.Frames[0])) return kind.Sprite;
        var palette = new Dictionary<char, string>(kind.Sprite.Palette);
        foreach (var (c, hex) in v.Palette ?? new Dictionary<char, string>()) palette[c] = hex;
        return new([rows], palette);
    }

    /// <summary>The drawing of a placed decoration (null for an unknown kind).</summary>
    public static LegacyLifeSprites.Grid? Sprite(WorldDecoration d) =>
        Find(d.Kind) is { } k ? Sprite(k, d.Variant, d.Turned && k.Turns) : null;

    /// <summary>The tiles from (x0, y0) to (x1, y1), one per step, both ends included (the brush's stroke).</summary>
    public static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1) yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

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

    // UC-53 alternative drawings (properties: Kinds is built before static fields further down would be)
    private static string[] FenceTurned =>
    [
        ".....pp.....",
        ".....bb.....",
        ".....bb.....",
        "....pppp....",
        ".....bb.....",
        ".....bb.....",
        "....pppp....",
        ".....bb.....",
        ".....bb.....",
        "....pppp....",
        ".....bb.....",
        ".....kk....."];

    private static string[] FloweringBush =>
    [
        "............",
        "............",
        "............",
        "....gGfg....",
        "..gfGgGGfg..",
        ".gGGgfGgGGg.",
        ".gfgGGGGgfg.",
        ".ggGgfGgGgg.",
        "..gggggggg..",
        "...kkkkkk...",
        "............",
        "............"];

    private static string[] LowLamp =>
    [
        "............",
        "............",
        "............",
        "............",
        ".....kk.....",
        "....kyyk....",
        "....yYYy....",
        "....kyyk....",
        ".....kk.....",
        ".....kk.....",
        "....kkkk....",
        "............"];

    private static string[] ArrowSign =>
    [
        "............",
        "............",
        "..bbbbbbb...",
        "..bBBBBBBb..",
        "..bBwwwwBBb.",
        "..bBBBBBBb..",
        "..bbbbbbb...",
        ".....kk.....",
        ".....kk.....",
        ".....kk.....",
        "....kkkk....",
        "............"];

    private static string[] EmptyCart =>
    [
        "........................",
        "........................",
        "........................",
        "........................",
        "...bbbbbbbbbbbb.........",
        "...bBBBBBBBBBBb.........",
        "...bbbbbbbbbbbbkkkkkkk..",
        "....k..........k........",
        "...wWw......wWw.........",
        "..wkWkw....wkWkw........",
        "...wWw......wWw.........",
        "........................"];

    private static Dictionary<char, string> Pal(params string[] entries) => entries.ToDictionary(e => e[0], e => e[1..]);
}
