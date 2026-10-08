namespace Lunet.Pathfinding;

/// <summary>A* em grade retangular finita com custos por célula e memória reutilizável.</summary>
/// <remarks>Busca síncrona; não é thread-safe nem reentrante. Construção aloca O(largura × altura).
/// Cada busca limpa os buffers em O(células); expansão usa heap binário, sem alocação por busca.
/// Não carrega mapas, não move entidades e não modifica dados de projeto. Diagonais nunca cortam cantos bloqueados.</remarks>
/// <example><code>
/// var grid = new Lunet.Pathfinding.GridPathfinder(10, 10);
/// grid.SetCost(new(4, 4), 0); // parede
/// var path = new Lunet.Pathfinding.GridPoint[100]; // reutilizar
/// var result = grid.FindPath(new(0, 0), new(9, 9), path, allowDiagonals: true);
/// </code></example>
public sealed class GridPathfinder
{
    private static readonly double Diagonal = Math.Sqrt(2);
    private readonly float[] _costs;
    private readonly double[] _scores;
    private readonly double[] _heuristics;
    private readonly int[] _parents;
    private readonly int[] _positions;
    private readonly int[] _heap;
    private int _heapCount;

    /// <summary>Cria uma grade com todas as células transitáveis, custo 1.</summary>
    /// <param name="width">Colunas positivas; o produto das dimensões deve caber em um array .NET.</param>
    /// <param name="height">Linhas positivas.</param>
    public GridPathfinder(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0 || (long)width * height > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(height));
        Width = width; Height = height;
        int count = width * height;
        _costs = new float[count]; Array.Fill(_costs, 1f);
        _scores = new double[count]; _heuristics = new double[count];
        _parents = new int[count]; _positions = new int[count]; _heap = new int[count];
    }

    /// <summary>Número de colunas.</summary>
    public int Width { get; }
    /// <summary>Número de linhas.</summary>
    public int Height { get; }

    /// <summary>Lê o custo de entrada da célula; zero significa bloqueada.</summary>
    /// <param name="cell">Coordenada dentro da grade.</param>
    /// <returns>Custo finito e não negativo.</returns>
    public float GetCost(GridPoint cell) => _costs[Index(cell)];

    /// <summary>Define o custo de entrada, sem alterar rotas já copiadas para o chamador.</summary>
    /// <param name="cell">Coordenada dentro da grade.</param>
    /// <param name="cost">Zero bloqueia; valores positivos finitos permitem trânsito, inclusive menores que 1.</param>
    public void SetCost(GridPoint cell, float cost)
    {
        int index = Index(cell);
        if (!float.IsFinite(cost) || cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        _costs[index] = cost;
    }

    /// <summary>Encontra uma rota de menor custo e a escreve da origem ao destino, sem alocação.</summary>
    /// <param name="start">Origem dentro da grade. Seu custo não é cobrado.</param>
    /// <param name="goal">Destino dentro da grade.</param>
    /// <param name="path">Buffer reutilizável; preservado integralmente quando sem rota ou pequeno demais.</param>
    /// <param name="allowDiagonals">Oito vizinhos se true, quatro se false. Passos diagonais custam √2 vezes o custo de entrada.</param>
    /// <returns>Estado, tamanho necessário, custo e visitas. Extremos fora da grade lançam ArgumentOutOfRangeException.</returns>
    public PathResult FindPath(GridPoint start, GridPoint goal, Span<GridPoint> path, bool allowDiagonals = false)
    {
        int source = Index(start), target = Index(goal);
        if (_costs[source] == 0 || _costs[target] == 0)
            return new(PathStatus.NotFound, 0, double.PositiveInfinity, 0);
        Array.Fill(_scores, double.PositiveInfinity);
        Array.Fill(_positions, -1);
        Array.Fill(_parents, -1);
        _heapCount = 0;
        float minimum = float.PositiveInfinity;
        foreach (float cost in _costs)
            if (cost > 0 && cost < minimum) minimum = cost;
        _scores[source] = 0;
        _heuristics[source] = Estimate(start.X, start.Y, goal, minimum, allowDiagonals);
        Insert(source);
        int visited = 0;
        while (_heapCount > 0)
        {
            int current = Remove(); visited++;
            if (current == target)
            {
                int length = 1;
                for (int node = target; node != source; node = _parents[node]) length++;
                if (path.Length < length) return new(PathStatus.BufferTooSmall, length, _scores[target], visited);
                int cursor = target;
                for (int i = length - 1; i >= 0; i--)
                {
                    path[i] = new(cursor % Width, cursor / Width);
                    cursor = _parents[cursor];
                }
                return new(PathStatus.Found, length, _scores[target], visited);
            }
            int x = current % Width, y = current / Width;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                bool diagonal = dx != 0 && dy != 0;
                if (diagonal && !allowDiagonals) continue;
                int nx = x + dx, ny = y + dy;
                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) continue;
                int next = ny * Width + nx;
                if (_costs[next] == 0) continue;
                if (diagonal && (_costs[y * Width + nx] == 0 || _costs[ny * Width + x] == 0)) continue;
                double score = _scores[current] + _costs[next] * (diagonal ? Diagonal : 1);
                if (score >= _scores[next]) continue;
                _scores[next] = score; _parents[next] = current;
                _heuristics[next] = Estimate(nx, ny, goal, minimum, allowDiagonals);
                if (_positions[next] >= 0) Up(_positions[next]);
                else Insert(next); // reabre se necessário por arredondamento numérico
            }
        }
        return new(PathStatus.NotFound, 0, double.PositiveInfinity, visited);
    }

    private int Index(GridPoint cell)
    {
        if ((uint)cell.X >= (uint)Width || (uint)cell.Y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(cell));
        return cell.Y * Width + cell.X;
    }

    private static double Estimate(int x, int y, GridPoint goal, float minimum, bool diagonal)
    {
        int dx = Math.Abs(goal.X - x), dy = Math.Abs(goal.Y - y);
        return minimum * (diagonal ? Math.Max(dx, dy) + (Diagonal - 1) * Math.Min(dx, dy) : (double)dx + dy);
    }

    // Desempate por distância restante e índice torna a saída reproduzível na mesma grade/opções.
    private bool Before(int a, int b)
    {
        double fa = _scores[a] + _heuristics[a], fb = _scores[b] + _heuristics[b];
        if (fa != fb) return fa < fb;
        if (_heuristics[a] != _heuristics[b]) return _heuristics[a] < _heuristics[b];
        return a < b;
    }

    private void Insert(int node)
    {
        _heap[_heapCount] = node; _positions[node] = _heapCount;
        Up(_heapCount++);
    }

    private void Up(int slot)
    {
        int node = _heap[slot];
        while (slot > 0)
        {
            int parent = (slot - 1) / 2;
            if (!Before(node, _heap[parent])) break;
            _heap[slot] = _heap[parent]; _positions[_heap[slot]] = slot; slot = parent;
        }
        _heap[slot] = node; _positions[node] = slot;
    }

    private int Remove()
    {
        int result = _heap[0]; _positions[result] = -1;
        int node = _heap[--_heapCount];
        if (_heapCount == 0) return result;
        int slot = 0;
        while (slot < _heapCount / 2)
        {
            int child = slot * 2 + 1;
            if (child + 1 < _heapCount && Before(_heap[child + 1], _heap[child])) child++;
            if (!Before(_heap[child], node)) break;
            _heap[slot] = _heap[child]; _positions[_heap[slot]] = slot; slot = child;
        }
        _heap[slot] = node; _positions[node] = slot;
        return result;
    }
}
