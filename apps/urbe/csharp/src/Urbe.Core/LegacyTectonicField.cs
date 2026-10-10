namespace Urbe.Core;

/// <summary>
/// Urbe 1.8.4-beta terrain.js, stages 1 and 2 of createWorld:
/// plates, warped Voronoi, boundary uplift and nearest-boundary BFS.
/// This is NOT a complete terrain map until the climate, elevation and river
/// algorithms have also been ported and parity-tested.
/// </summary>
public sealed class LegacyTectonicField
{
    public sealed record Plate(
        double X, double Y, bool Continental, double Base,
        double VelocityX, double VelocityY);

    private readonly byte[] _plateMap;
    private readonly float[] _uplift;
    private readonly ushort[] _distance;
    private readonly float[] _nearestUplift;

    private LegacyTectonicField(
        int gridSize, Plate[] plates, byte[] plateMap,
        float[] uplift, ushort[] distance, float[] nearestUplift)
    {
        GridSize = gridSize;
        Plates = Array.AsReadOnly(plates);
        _plateMap = plateMap;
        _uplift = uplift;
        _distance = distance;
        _nearestUplift = nearestUplift;
    }

    public int GridSize { get; }
    public IReadOnlyList<Plate> Plates { get; }

    public byte PlateIndexAt(int x, int y) => _plateMap[Index(x, y)];
    public float UpliftAt(int x, int y) => _uplift[Index(x, y)];
    public ushort DistanceToBoundaryAt(int x, int y) => _distance[Index(x, y)];
    public float NearestBoundaryUpliftAt(int x, int y) => _nearestUplift[Index(x, y)];

    private int Index(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        if (x >= GridSize) throw new ArgumentOutOfRangeException(nameof(x));
        if (y >= GridSize) throw new ArgumentOutOfRangeException(nameof(y));
        return y * GridSize + x;
    }

    private static readonly (int X, int Y)[] Neighbors =
        [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static LegacyTectonicField Generate(
        string? seedText, int gridSize = 512, int? plateCount = null)
    {
        if (gridSize < 8 || gridSize > 512 || gridSize % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(gridSize));
        if (plateCount is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(plateCount));

        // Same call order as terrain.js: the count consumes one random value
        // only when opts.plates is not supplied.
        uint seed = LegacyTerrainMath.Hash32(
            string.IsNullOrEmpty(seedText) ? "urbe" : seedText);
        var random = new LegacyTerrainMath.Mulberry32(seed ^ 0x5bd1e995u);
        var warp = new LegacyTerrainMath.GradientNoise(seed ^ 0x27d4eb2fu);
        int count = plateCount ?? (16 + (int)Math.Floor(random.Next() * 6));
        var plates = new Plate[count];

        for (int i = 0; i < count; i++)
        {
            bool continental = random.Next() < .42;
            double angle = random.Next() * Math.PI * 2;
            double velocity = .4 + random.Next() * .6;
            double x = (.06 + random.Next() * .88) * gridSize;
            double y = (.06 + random.Next() * .88) * gridSize;
            double elevation = continental
                ? .62 + random.Next() * .08
                : .22 + random.Next() * .08;
            double vx = Math.Cos(angle) * velocity;
            double vy = Math.Sin(angle) * velocity;
            if (continental)
            {
                x = gridSize / 2.0 + (x - gridSize / 2.0) * .62;
                y = gridSize / 2.0 + (y - gridSize / 2.0) * .62;
            }
            plates[i] = new Plate(x, y, continental, elevation, vx, vy);
        }

        // Original algorithm runs warped Voronoi at half resolution, then
        // duplicates the plate index into a 2x2 fine-grid neighborhood.
        int halfWidth = gridSize >> 1, size = gridSize * gridSize;
        var half = new byte[halfWidth * halfWidth];
        var plateMap = new byte[size];
        for (int y = 0; y < halfWidth; y++)
        for (int x = 0; x < halfWidth; x++)
        {
            double x2 = x * 2 + 1, y2 = y * 2 + 1;
            double warpedX = x2 + LegacyTerrainMath.Fbm(
                warp, x2 / 80.0, y2 / 80.0, 4, 2, .5) * 58;
            double warpedY = y2 + LegacyTerrainMath.Fbm(
                warp, x2 / 80.0 + 19, y2 / 80.0 - 7, 4, 2, .5) * 58;
            int best = 0;
            double minDistance = 1e18;
            for (int p = 0; p < count; p++)
            {
                double dx = warpedX - plates[p].X;
                double dy = warpedY - plates[p].Y;
                double d = dx * dx + dy * dy;
                if (d < minDistance)
                {
                    minDistance = d;
                    best = p;
                }
            }
            half[y * halfWidth + x] = (byte)best;
        }
        for (int y = 0; y < gridSize; y++)
        for (int x = 0; x < gridSize; x++)
            plateMap[y * gridSize + x] = half[(y >> 1) * halfWidth + (x >> 1)];

        var uplift = new float[size];
        var border = new bool[size];
        for (int y = 0; y < gridSize; y++)
        for (int x = 0; x < gridSize; x++)
        {
            int index = y * gridSize + x;
            Plate a = plates[plateMap[index]];
            double sum = 0;
            int crossings = 0;
            foreach (var (dx, dy) in Neighbors)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= gridSize || yy >= gridSize)
                    continue;
                int otherIndex = yy * gridSize + xx;
                if (plateMap[otherIndex] == plateMap[index])
                    continue;
                Plate b = plates[plateMap[otherIndex]];
                double convergence =
                    (a.VelocityX - b.VelocityX) * dx +
                    (a.VelocityY - b.VelocityY) * dy;
                double u;
                if (convergence > 0)
                {
                    if (a.Continental && b.Continental) u = .95 * convergence;
                    else if (a.Continental && !b.Continental) u = .62 * convergence;
                    else if (!a.Continental && b.Continental) u = -.25 * convergence;
                    else u = .42 * convergence;
                }
                else
                {
                    u = a.Continental ? .2 * convergence : -.08 * convergence;
                }
                sum += u;
                crossings++;
            }
            if (crossings > 0)
            {
                border[index] = true;
                uplift[index] = (float)(sum / crossings);
            }
        }

        var distance = new ushort[size];
        Array.Fill(distance, ushort.MaxValue);
        var nearestUplift = new float[size];
        var queue = new int[size];
        int head = 0, tail = 0;
        for (int index = 0; index < size; index++)
        {
            if (!border[index])
                continue;
            distance[index] = 0;
            nearestUplift[index] = uplift[index];
            queue[tail++] = index;
        }
        // JS q is an Int32Array and scans the four neighbours in this order.
        while (head < tail)
        {
            int index = queue[head++];
            int x = index % gridSize, y = index / gridSize;
            foreach (var (dx, dy) in Neighbors)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= gridSize || yy >= gridSize)
                    continue;
                int next = yy * gridSize + xx;
                if (distance[next] != ushort.MaxValue)
                    continue;
                distance[next] = (ushort)(distance[index] + 1);
                nearestUplift[next] = nearestUplift[index];
                queue[tail++] = next;
            }
        }

        return new LegacyTectonicField(
            gridSize, plates, plateMap, uplift, distance, nearestUplift);
    }
}
