namespace Lunet.Pathfinding;

/// <summary>Coordenada inteira de uma célula; não representa pixels ou posição de mundo.</summary>
/// <param name="X">Coluna, a partir de zero.</param>
/// <param name="Y">Linha, a partir de zero.</param>
/// <example><code>
/// var cell = new Lunet.Pathfinding.GridPoint(2, 3);
/// var world = new System.Numerics.Vector2(cell.X * 32 + 16, cell.Y * 32 + 16);
/// </code></example>
public readonly record struct GridPoint(int X, int Y);

/// <summary>Resultado da busca: sucesso, ausência de rota ou saída pequena.</summary>
/// <example><code>
/// var grid = new Lunet.Pathfinding.GridPathfinder(8, 8);
/// var path = new Lunet.Pathfinding.GridPoint[64];
/// var result = grid.FindPath(new(0, 0), new(7, 7), path);
/// bool found = result.Status == Lunet.Pathfinding.PathStatus.Found;
/// </code></example>
public enum PathStatus
{
    /// <summary>Rota escrita na saída, incluindo origem e destino.</summary>
    Found,
    /// <summary>Sem rota, inclusive quando origem ou destino estão bloqueados.</summary>
    NotFound,
    /// <summary>Rota encontrada, mas a saída não comporta Length células; saída preservada.</summary>
    BufferTooSmall
}

/// <summary>Metadados da última busca, sem possuir o buffer da rota.</summary>
/// <param name="Status">Estado da busca.</param>
/// <param name="Length">Células da rota, incluindo extremos; tamanho necessário se BufferTooSmall, zero se NotFound.</param>
/// <param name="Cost">Soma dos custos de entrada, multiplicados pela distância do passo; infinito se NotFound.</param>
/// <param name="VisitedCount">Número de remoções da fila de busca, inclusive destino quando encontrado.</param>
/// <example><code>
/// var grid = new Lunet.Pathfinding.GridPathfinder(4, 4);
/// var path = new Lunet.Pathfinding.GridPoint[16];
/// Lunet.Pathfinding.PathResult result = grid.FindPath(new(0, 0), new(3, 3), path);
/// double cost = result.Cost;
/// </code></example>
public readonly record struct PathResult(PathStatus Status, int Length, double Cost, int VisitedCount);
