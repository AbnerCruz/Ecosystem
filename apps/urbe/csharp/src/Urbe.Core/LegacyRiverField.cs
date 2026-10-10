namespace Urbe.Core;

/// <summary>
/// River segments and spatial lookup ported from the original Urbe
/// 1.8.4-beta terrain.js (the "segs" array and "bucket" Map).
/// These coordinates are in continental grid cells; WorldSpawn.CellSize
/// converts them into terrain tiles. This class does not draw new art.
/// </summary>
public sealed class LegacyRiverField
{
    public sealed record Segment(
        double Ax, double Ay, double Bx, double By, double Width);

    private readonly List<Segment> _segments = [];
    private readonly Dictionary<int, List<Segment>> _buckets = [];
    private readonly int _gridSize;
    private readonly int _cellSize;
    private readonly double _originX;
    private readonly double _originY;
    private readonly LegacyTerrainMath.GradientNoise _drainageNoise;

    public IReadOnlyList<Segment> Segments => _segments;

    public LegacyRiverField(LegacyHydrologyField hydrology, LegacyWorldSpawn spawn)
    {
        ArgumentNullException.ThrowIfNull(hydrology);
        ArgumentNullException.ThrowIfNull(spawn);
        if (spawn.GridSize != hydrology.GridSize)
            throw new ArgumentException("Spawn and hydrology must belong to the same world.", nameof(spawn));
        _gridSize = hydrology.GridSize;
        _cellSize = spawn.CellSize;
        _originX = spawn.OriginX;
        _originY = spawn.OriginY;
        _drainageNoise = new LegacyTerrainMath.GradientNoise(
            LegacyTerrainMath.Hash32(hydrology.Climate.SeedText) ^ 0x165667b1u);

        double threshold = hydrology.RiverThreshold;
        double Jitter(int index, int salt) =>
            (LegacyTerrainMath.TileHash(index % _gridSize, index / _gridSize, salt) - .5) * .7;
        double Cx(int index) => index % _gridSize + .5 + Jitter(index, 71);
        double Cy(int index) => index / _gridSize + .5 + Jitter(index, 73);

        for (int index = 0; index < _gridSize * _gridSize; index++)
        {
            int x = index % _gridSize, y = index / _gridSize;
            double e = hydrology.Climate.ElevationAt(x, y);
            double accumulation = hydrology.AccumulationAt(x, y);
            int downstream = hydrology.DownstreamAt(x, y);
            if (e < LegacyBiomeRules.SeaLevel ||
                accumulation < threshold || downstream < 0)
                continue;

            int nextX = downstream % _gridSize, nextY = downstream / _gridSize;
            double width = Math.Min(5.5,
                1.35 + Math.Log2(accumulation / threshold) * .62);
            bool nextIsSea = hydrology.Climate.ElevationAt(nextX, nextY) <
                LegacyBiomeRules.SeaLevel;
            var segment = new Segment(
                Cx(index), Cy(index),
                nextIsSea ? nextX + .5 : Cx(downstream),
                nextIsSea ? nextY + .5 : Cy(downstream), width);
            _segments.Add(segment);

            int x0 = (int)Math.Floor(Math.Min(segment.Ax, segment.Bx) - 1);
            int x1 = (int)Math.Floor(Math.Max(segment.Ax, segment.Bx) + 1);
            int y0 = (int)Math.Floor(Math.Min(segment.Ay, segment.By) - 1);
            int y1 = (int)Math.Floor(Math.Max(segment.Ay, segment.By) + 1);
            for (int yy = y0; yy <= y1; yy++)
            for (int xx = x0; xx <= x1; xx++)
            {
                // JS Map key is yy*N+xx (including possible negative keys).
                int key = unchecked(yy * _gridSize + xx);
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    _buckets[key] = bucket;
                }
                bucket.Add(segment);
            }
        }
    }

    /// <summary>Water width at a specific terrain tile, from terrain.js riverAt().</summary>
    public double WidthAt(int tileX, int tileY)
    {
        double qx = (tileX + .5 +
            LegacyTerrainMath.Fbm(_drainageNoise,
                tileX / 11.0, tileY / 11.0, 2, 2, .5) * 1.8 -
            _originX) / _cellSize;
        double qy = (tileY + .5 +
            LegacyTerrainMath.Fbm(_drainageNoise,
                tileX / 11.0 + 40, tileY / 11.0 - 40, 2, 2, .5) * 1.8 -
            _originY) / _cellSize;
        int key = unchecked((int)Math.Floor(qy) * _gridSize + (int)Math.Floor(qx));
        if (!_buckets.TryGetValue(key, out var bucket))
            return 0;
        double best = 0;
        foreach (var segment in bucket)
        {
            double dx = segment.Bx - segment.Ax;
            double dy = segment.By - segment.Ay;
            double projection = ((qx - segment.Ax) * dx +
                (qy - segment.Ay) * dy) / (dx * dx + dy * dy == 0 ? 1 : dx * dx + dy * dy);
            double t = Math.Clamp(projection, 0, 1);
            double px = segment.Ax + dx * t - qx;
            double py = segment.Ay + dy * t - qy;
            double half = Math.Max(.72, segment.Width / 2);
            if ((px * px + py * py) * _cellSize * _cellSize <
                half * half && segment.Width > best)
                best = segment.Width;
        }
        return best;
    }
}
