namespace Urbe.Core;

/// <summary>
/// What world/life.js and the app.js fauna see of the world (app.js 'world.life.host').
/// Coordinates are tiles; implementations floor them as the original host does.
/// </summary>
public interface ILegacyLifeHost
{
    /// <summary>MUNDO.isWater: biome ≤ lake.</summary>
    bool Water(int x, int y);
    /// <summary>MUNDO.biome, or -1 while the chunk is not generated (H.bioma).</summary>
    int Biome(int x, int y);
    /// <summary>world.roads.has(K(x,y)).</summary>
    bool Road(int x, int y);
    /// <summary>bAt: a house on the tile.</summary>
    bool House(int x, int y);
    /// <summary>regAt: a neighbourhood on the tile.</summary>
    bool District(int x, int y);
    IReadOnlyList<LegacyCityBuilding> Buildings { get; }
    /// <summary>world.roads in insertion order.</summary>
    IReadOnlyCollection<(int X, int Y)> Roads { get; }
    /// <summary>world.regions with w, their urbeCentroide and whether they have a parent.</summary>
    IEnumerable<LegacyLifeDistrict> Districts { get; }
    /// <summary>v25Povo: the villagers (empty until they are ported).</summary>
    IReadOnlyList<ILegacyLifePerson> People { get; }
}

public readonly record struct LegacyLifeDistrict(string Name, bool HasParent, double X, double Y);

/// <summary>A villager as world/life.js reads it (v25Povo entries).</summary>
public interface ILegacyLifePerson
{
    /// <summary>False while the villager has no position (JS a.x falsy).</summary>
    bool Placed { get; }
    double X { get; }
    double Y { get; }
    double DX { get; }
    double DY { get; }
    double Pause { get; }
    double Talk { get; }
    /// <summary>tipo === 'andarilho'.</summary>
    bool Wanderer { get; }
    /// <summary>pausa&gt;0 &amp;&amp; rota &amp;&amp; (s&lt;=0 || s&gt;=rota.length-1): inside a house.</summary>
    bool Indoors { get; }
    /// <summary>a.look.shirt.length when present (umbrella colour), else null.</summary>
    int? ShirtLength { get; }
}

/// <summary>The camera as life.js view() sees it: zoom, screen size and faixaVisivel().</summary>
public readonly record struct LegacyLifeView(double CameraX, double CameraY, double Zoom, double Width, double Height,
    int X0, int Y0, int X1, int Y1)
{
    public const int Tile = 32;
    /// <summary>s = TILE·z/16 (one art pixel).</summary>
    public double S => Tile * Zoom / 16;
    /// <summary>t = TILE·z (one tile).</summary>
    public double T => Tile * Zoom;

    /// <summary>P(x,y) = w2s(x·TILE, y·TILE).</summary>
    public (double X, double Y) P(double x, double y) =>
        ((x * Tile - CameraX) * Zoom + Width / 2, (y * Tile - CameraY) * Zoom + Height / 2);

    /// <summary>life.js dentro(f,x,y,m).</summary>
    public bool Inside(double x, double y, double m = 0) => x >= X0 - m && x <= X1 + m && y >= Y0 - m && y <= Y1 + m;
}

/// <summary>urbeOpcoes switches read by the life of the world.</summary>
public sealed class LegacyLifeOptions
{
    public bool People { get; set; } = true;
    public bool Fauna { get; set; } = true;
    public bool Weather { get; set; } = true;
    public bool Events { get; set; } = true;
    /// <summary>ambiente: real, ciclo, dia, entardecer or noite (app.js default 'ciclo').</summary>
    public string TimeMode { get; set; } = "ciclo";
}
