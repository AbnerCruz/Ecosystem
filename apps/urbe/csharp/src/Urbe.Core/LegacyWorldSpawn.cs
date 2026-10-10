namespace Urbe.Core;

/// <summary>
/// Exact stage 6 from Urbe 1.8.4-beta terrain.js: choose the original
/// temperate valley spawn with distance fields for coast, river, mountain
/// and lakes. World tile coordinates retain the old 16 px tile metric.
/// </summary>
public sealed class LegacyWorldSpawn
{
    private LegacyWorldSpawn(
        int cellIndex, int gridSize, int cellSize, double originX, double originY)
    {
        CellIndex = cellIndex;
        GridSize = gridSize;
        CellSize = cellSize;
        OriginX = originX;
        OriginY = originY;
    }

    public int CellIndex { get; }
    public int GridSize { get; }
    public int CellSize { get; }
    public int CellX => CellIndex % GridSize;
    public int CellY => CellIndex / GridSize;
    public double OriginX { get; }
    public double OriginY { get; }

    private static readonly (int X, int Y)[] Directions =
        [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static LegacyWorldSpawn Choose(
        LegacyHydrologyField hydrology,
        int cellSize = 4, double spawnX = 36, double spawnY = 25)
    {
        ArgumentNullException.ThrowIfNull(hydrology);
        if (cellSize < 1) throw new ArgumentOutOfRangeException(nameof(cellSize));
        int n = hydrology.GridSize, length = n * n;
        var climate = hydrology.Climate;
        float[] elevation = climate.Elevations();
        float[] temperature = climate.Temperatures();
        float[] moisture = climate.Moistures();
        double sea = LegacyBiomeRules.SeaLevel;
        var distanceCoast = Distances(length, n, i => elevation[i] < sea);
        var distanceRiver = Distances(length, n, i =>
        {
            int x = i % n, y = i / n;
            return elevation[i] >= sea &&
                hydrology.AccumulationAt(x, y) >= hydrology.RiverThreshold;
        });
        var distanceMountain = Distances(length, n, i => elevation[i] > .72);
        var distanceLake = Distances(length, n, i =>
            hydrology.IsLakeAt(i % n, i / n));

        int best = -1;
        double highest = -1e9;
        for (int i = 0; i < length; i++)
        {
            if (elevation[i] < sea + .03 ||
                elevation[i] > .6 ||
                hydrology.IsLakeAt(i % n, i / n))
                continue;

            double score =
                -Math.Abs(temperature[i] - .56) * 6
                -Math.Abs(moisture[i] - .56) * 4
                -Math.Abs(distanceRiver[i] - 9) * .18
                -Math.Max(0, 24 - distanceCoast[i]) * .45
                -Math.Max(0, 16 - distanceMountain[i]) * .5
                -Math.Max(0, 6 - distanceLake[i]) * .6
                -Math.Max(0, 5 - distanceRiver[i]) * 2;
            int x = i % n, y = i / n;
            score -= Math.Sqrt((x - n / 2.0) * (x - n / 2.0) +
                               (y - n / 2.0) * (y - n / 2.0)) / n * 1.5;
            if (score > highest)
            {
                highest = score;
                best = i;
            }
        }
        if (best < 0)
            best = (n / 2) * n + (n / 2);
        double originX = spawnX - ((best % n) + .5) * cellSize;
        double originY = spawnY - ((best / n) + .5) * cellSize;
        return new LegacyWorldSpawn(best, n, cellSize, originX, originY);
    }

    private static ushort[] Distances(int length, int n, Func<int, bool> test)
    {
        var distance = new ushort[length];
        Array.Fill(distance, ushort.MaxValue);
        var queue = new int[length];
        int head = 0, tail = 0;
        for (int i = 0; i < length; i++)
        {
            if (!test(i))
                continue;
            distance[i] = 0;
            queue[tail++] = i;
        }
        while (head < tail)
        {
            int index = queue[head++];
            int x = index % n, y = index / n;
            foreach (var (dx, dy) in Directions)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n)
                    continue;
                int next = yy * n + xx;
                if (distance[next] != ushort.MaxValue)
                    continue;
                distance[next] = (ushort)(distance[index] + 1);
                queue[tail++] = next;
            }
        }
        return distance;
    }
}
