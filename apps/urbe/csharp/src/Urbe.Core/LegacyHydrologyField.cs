namespace Urbe.Core;

/// <summary>
/// Urbe 1.8.4-beta terrain.js stage 5: depression filling (priority-flood),
/// lake mask, drainage directions and rain-driven flow accumulation.
/// Operates only on portable C# Float32-compatible terrain data.
/// </summary>
public sealed class LegacyHydrologyField
{
    private readonly float[] _microRelief;
    private readonly float[] _filled;
    private readonly int[] _downstream;
    private readonly bool[] _lakes;
    private readonly float[] _accumulation;

    private LegacyHydrologyField(
        LegacyElevationClimateField climate, float[] microRelief, float[] filled,
        int[] downstream, bool[] lakes, float[] accumulation, double riverThreshold)
    {
        Climate = climate;
        _microRelief = microRelief;
        _filled = filled;
        _downstream = downstream;
        _lakes = lakes;
        _accumulation = accumulation;
        RiverThreshold = riverThreshold;
    }

    public LegacyElevationClimateField Climate { get; }
    public int GridSize => Climate.GridSize;
    public double RiverThreshold { get; }
    public float MicroReliefAt(int x, int y) => _microRelief[Index(x, y)];
    public float FilledAt(int x, int y) => _filled[Index(x, y)];
    public int DownstreamAt(int x, int y) => _downstream[Index(x, y)];
    public bool IsLakeAt(int x, int y) => _lakes[Index(x, y)];
    public float AccumulationAt(int x, int y) => _accumulation[Index(x, y)];

    private int Index(int x, int y)
    {
        if (x < 0 || x >= GridSize) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= GridSize) throw new ArgumentOutOfRangeException(nameof(y));
        return y * GridSize + x;
    }

    private static readonly (int X, int Y)[] D4 =
        [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int X, int Y)[] D8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1),
         (1, 1), (1, -1), (-1, 1), (-1, -1)];

    public static LegacyHydrologyField Generate(LegacyElevationClimateField climate)
    {
        ArgumentNullException.ThrowIfNull(climate);
        int n = climate.GridSize, size = checked(n * n);
        float[] elevation = climate.Elevations();
        float[] moisture = climate.Moistures();
        double sea = LegacyBiomeRules.SeaLevel;
        uint seed = LegacyTerrainMath.Hash32(
            // Keep the original climate seed instead of trying to derive it
            // from elevation data. The canonical property is immutable.
            climate.SeedText);
        var drainageNoise = new LegacyTerrainMath.GradientNoise(
            seed ^ 0x165667b1u);

        // Exactly the Float32Array Eh and smoothed land-only micro-relief.
        var micro = new float[size];
        for (int i = 0; i < size; i++)
        {
            int x = i % n, y = i / n;
            double detail = 0;
            if (elevation[i] >= sea)
            {
                detail = (
                    LegacyTerrainMath.Fbm(
                        drainageNoise, x / 7.0 + 411, y / 7.0 - 411, 3, 2, .55) * .012 +
                    LegacyTerrainMath.Fbm(
                        drainageNoise, x / 23.0 - 91, y / 23.0 + 91, 2, 2, .5) * .01) *
                    LegacyTerrainMath.Smooth(sea, sea + .02, elevation[i]);
            }
            micro[i] = (float)(elevation[i] + detail);
        }

        var flood = new float[size];
        var downstream = new int[size];
        Array.Fill(downstream, -1);
        var done = new bool[size];
        var order = new int[size];
        int visited = 0;
        var queue = new OriginalMinHeap(size, flood);

        // All sea cells are terminal; coastal sea pixels seed the flood.
        for (int i = 0; i < size; i++)
        {
            if (!(elevation[i] < sea))
                continue;
            flood[i] = elevation[i];
            done[i] = true;
            int x = i % n, y = i / n;
            bool coastal = false;
            foreach (var (dx, dy) in D4)
            {
                int xx = x + dx, yy = y + dy;
                if (xx >= 0 && yy >= 0 && xx < n && yy < n &&
                    elevation[yy * n + xx] >= sea)
                    coastal = true;
            }
            if (coastal)
                queue.Push(i);
        }

        while (queue.Count > 0)
        {
            int i = queue.Pop();
            int x = i % n, y = i / n;
            foreach (var (dx, dy) in D8)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= n || yy >= n)
                    continue;
                int j = yy * n + xx;
                if (done[j])
                    continue;
                done[j] = true;
                flood[j] = (float)Math.Max(micro[j], flood[i] + 1e-5);
                downstream[j] = i;
                order[visited++] = j;
                queue.Push(j);
            }
        }

        var lakes = new bool[size];
        var flow = new float[size];
        for (int i = 0; i < size; i++)
        {
            lakes[i] = elevation[i] >= sea && flood[i] - micro[i] > .006;
            if (elevation[i] >= sea)
                flow[i] = (float)(.15 + moisture[i]);
        }
        for (int i = visited - 1; i >= 0; i--)
        {
            int cell = order[i];
            if (downstream[cell] >= 0)
            {
                int to = downstream[cell];
                flow[to] = (float)(flow[to] + flow[cell]);
            }
        }

        var landFlow = new List<float>();
        for (int i = 0; i < size; i++)
            if (elevation[i] >= sea && !lakes[i])
                landFlow.Add(flow[i]);
        landFlow.Sort();
        double qRiver = landFlow.Count == 0 ? 1e9 :
            landFlow[(int)Math.Floor(landFlow.Count * .965)];
        if (qRiver == 0)
            qRiver = 1e9;

        return new LegacyHydrologyField(
            climate, micro, flood, downstream, lakes, flow, qRiver);
    }

    /// <summary>
    /// The heap from terrain.js, including its tie-break behaviour.
    /// A framework PriorityQueue is NOT equivalent for identical elevations:
    /// it could change both lake masks and river paths.
    /// </summary>
    private sealed class OriginalMinHeap
    {
        private readonly int[] _items;
        private readonly float[] _keys;
        private int _count;
        public OriginalMinHeap(int size, float[] keys)
        {
            _items = new int[size];
            _keys = keys;
        }
        public int Count => _count;
        public void Push(int value)
        {
            int i = _count++;
            float key = _keys[value];
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_keys[_items[parent]] <= key)
                    break;
                _items[i] = _items[parent];
                i = parent;
            }
            _items[i] = value;
        }
        public int Pop()
        {
            int result = _items[0];
            _count--;
            if (_count == 0)
                return result;
            _items[0] = _items[_count];
            Down(0);
            return result;
        }
        private void Down(int i)
        {
            int value = _items[i];
            float key = _keys[value];
            while (true)
            {
                int left = i * 2 + 1, right = left + 1, min = i;
                if (left < _count && _keys[_items[left]] < key)
                    min = left;
                if (right < _count &&
                    _keys[_items[right]] <
                    (min == i ? key : _keys[_items[min]]))
                    min = right;
                if (min == i)
                    break;
                _items[i] = _items[min];
                i = min;
            }
            _items[i] = value;
        }
    }
}
