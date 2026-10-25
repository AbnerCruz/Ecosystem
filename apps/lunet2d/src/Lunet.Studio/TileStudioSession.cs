using Lunet.Graphics;
using Lunet.Pathfinding;

namespace Lunet.Studio;

/// <summary>Undoable, source-first editing of the runtime's existing tilemap v1.</summary>
/// <remarks>One history entry per touch gesture. No alternate map format or hidden game logic.</remarks>
public sealed class TileStudioSession
{
    private readonly List<TileMap> _undo = [];
    private readonly List<TileMap> _redo = [];
    private TileMap? _strokeBefore;
    private const int HistoryLimit = 32;

    public TileStudioSession(TileMap map, int maxTileId)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        if (maxTileId < 1) throw new ArgumentOutOfRangeException(nameof(maxTileId));
        MaxTileId = maxTileId;
        for (int layer = 0; layer < map.LayerCount; layer++)
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.GetTile(layer, new GridPoint(x, y)) > maxTileId)
                        throw new InvalidDataException("O tileset não contém todos os tiles usados no mapa.");
    }

    public TileMap Map { get; private set; }
    public int MaxTileId { get; }
    public int Layer { get; private set; }
    public int Tile { get; set; } = 1;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void SelectLayer(int index)
    {
        if ((uint)index >= (uint)Map.LayerCount) throw new ArgumentOutOfRangeException(nameof(index));
        EndStroke();
        Layer = index;
    }

    public void BeginStroke()
    {
        if (_strokeBefore is not null) throw new InvalidOperationException("O gesto anterior não terminou.");
        _strokeBefore = Map;
    }

    public bool Paint(int x, int y)
    {
        if (_strokeBefore is null) throw new InvalidOperationException("Inicie o gesto antes de pintar.");
        if ((uint)x >= (uint)Map.Width || (uint)y >= (uint)Map.Height) return false;
        if (Tile < 0 || Tile > MaxTileId) throw new ArgumentOutOfRangeException(nameof(Tile));
        var next = Map.WithTile(Layer, new GridPoint(x, y), Tile);
        if (ReferenceEquals(next, Map)) return false;
        Map = next;
        return true;
    }

    public bool EndStroke()
    {
        if (_strokeBefore is null) return false;
        bool changed = !ReferenceEquals(Map, _strokeBefore);
        if (changed) Push(_undo, _strokeBefore);
        _strokeBefore = null;
        if (changed) _redo.Clear();
        return changed;
    }

    public void AddLayer(string name, bool collision = false)
        => Change(map => map.AddLayer(name, collision: collision), map => Layer = map.LayerCount - 1);

    public void RemoveLayer()
        => Change(map => map.RemoveLayer(Layer), map => Layer = Math.Min(Layer, map.LayerCount - 1));

    public void RenameLayer(string name) => Change(map => map.RenameLayer(Layer, name));

    public void ToggleCollision()
        => Change(map => map.WithLayerSettings(Layer, map.IsLayerVisible(Layer), !map.IsCollisionLayer(Layer)));

    public void ToggleVisibility()
        => Change(map => map.WithLayerSettings(Layer, !map.IsLayerVisible(Layer), map.IsCollisionLayer(Layer)));

    public void MoveLayer(int direction)
    {
        int target = Layer + direction;
        if ((uint)target >= (uint)Map.LayerCount) return;
        Change(map => map.MoveLayer(Layer, target), _ => Layer = target);
    }

    public bool Undo()
    {
        EndStroke();
        if (!CanUndo) return false;
        Push(_redo, Map);
        Map = Pop(_undo);
        Layer = Math.Min(Layer, Map.LayerCount - 1);
        return true;
    }

    public bool Redo()
    {
        EndStroke();
        if (!CanRedo) return false;
        Push(_undo, Map);
        Map = Pop(_redo);
        Layer = Math.Min(Layer, Map.LayerCount - 1);
        return true;
    }

    private void Change(Func<TileMap, TileMap> edit, Action<TileMap>? after = null)
    {
        EndStroke();
        TileMap previous = Map;
        TileMap next = edit(previous);
        if (ReferenceEquals(previous, next)) return;
        Push(_undo, previous);
        _redo.Clear();
        Map = next;
        after?.Invoke(next);
    }

    private static TileMap Pop(List<TileMap> history)
    {
        var result = history[^1];
        history.RemoveAt(history.Count - 1);
        return result;
    }

    private static void Push(List<TileMap> history, TileMap map)
    {
        if (history.Count == HistoryLimit) history.RemoveAt(0);
        history.Add(map);
    }
}
