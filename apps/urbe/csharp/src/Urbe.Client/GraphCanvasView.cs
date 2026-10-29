using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>Editable native canvas, shared by the dedicated graph screen and read-only Markdown embeds.
/// Never moves a referenced vault object: gestures only change the authored graph's coordinates.</summary>
public enum GraphCanvasTool { Select, Draw, Erase }

public sealed partial class GraphCanvasView : Control
{
    private GraphDocument? _document;
    private double _zoom = 1;
    private Vector _origin;
    private string? _selected;
    private string? _pressedNode;
    private Point? _press;
    private Point _last;
    private bool _moved;
    private bool _fitted;
    private double _pinch = 1;
    private List<GraphPoint>? _drawing;
    private readonly HashSet<string> _selectedIds = new(StringComparer.Ordinal);
    public GraphCanvasTool Tool { get; set; }
    public bool MultipleSelection { get; set; }
    public IReadOnlyCollection<string> SelectedIds => _selectedIds;


    public bool ReadOnly { get; set; }
    public GraphDocument? Document => _document;
    public string? SelectedId => _selected;
    public GraphNode? SelectedNode => _selected is null ? null : _document?.Node(_selected);
    public event Action? Changed;
    public event Action? SelectionChanged;

    public GraphCanvasView()
    {
        ClipToBounds = true;
        Focusable = true;
        AutomationProperties.SetName(this, "Editor visual de grafos");
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, (_, e) => { ZoomAt(e.ScaleOrigin, e.Scale / _pinch); _pinch = e.Scale; e.Handled = true; });
        AddHandler(InputElement.PinchEndedEvent, (_, _) => _pinch = 1);
    }

    /// <summary>CST-066: whether a card's note, file or folder is still in the vault (null: not known, nothing marked).</summary>
    public Func<GraphNode, bool>? Exists { get; set; }

    /// <summary>A card pointing to something no longer in the vault (deleted, or changed outside the Urbe).</summary>
    public bool IsMissing(GraphNode node) =>
        node.Kind is GraphNodeKind.Note or GraphNodeKind.File or GraphNodeKind.Folder && Exists is { } exists && !exists(node);

    public void Show(GraphDocument document, bool readOnly = false)
    {
        _document = document;
        ReadOnly = readOnly;
        _selected = null;
        _selectedIds.Clear();
        Tool = GraphCanvasTool.Select;
        MultipleSelection = false;
        _origin = default;
        _zoom = 1;
        _fitted = false;
        InvalidateVisual();
    }

    public Point WorldCenter => ToWorld(new Point(Bounds.Width / 2, Bounds.Height / 2));

    public void Select(string? id)
    {
        _selected = id;
        _selectedIds.Clear();
        if (id is not null) _selectedIds.Add(id);
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    private Point ToWorld(Point point) => new((point.X - Bounds.Width / 2) / _zoom + _origin.X,
        (point.Y - Bounds.Height / 2) / _zoom + _origin.Y);
    private Point ToScreen(double x, double y) => new((x - _origin.X) * _zoom + Bounds.Width / 2,
        (y - _origin.Y) * _zoom + Bounds.Height / 2);
    private Rect NodeRect(GraphNode node)
    {
        var p = ToScreen(node.X, node.Y);
        return new Rect(p.X, p.Y, node.Width * _zoom, node.Height * _zoom);
    }

    private void Fit()
    {
        _fitted = true;
        if (_document is not { Nodes.Count: > 0 } d || Bounds.Width < 1 || Bounds.Height < 1) return;
        double left = d.Nodes.Min(n => n.X), top = d.Nodes.Min(n => n.Y);
        double right = d.Nodes.Max(n => n.X + n.Width), bottom = d.Nodes.Max(n => n.Y + n.Height);
        _origin = new Vector((left + right) / 2, (top + bottom) / 2);
        _zoom = Math.Clamp(Math.Min(Bounds.Width / (right - left + 120), Bounds.Height / (bottom - top + 120)), .18, 1.6);
    }

    public void ZoomAt(Point at, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var before = ToWorld(at);
        var next = _zoom * factor;
        if (!double.IsFinite(next) || next <= 0) return;
        _zoom = next;
        var after = ToWorld(at);
        _origin += before - after;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // in a note the wheel scrolls the note; Ctrl with it zooms the graph (CST-067)
        if (Cooperative && !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        ZoomAt(e.GetPosition(this), Math.Pow(1.15, e.Delta.Y));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Cooperative && e.Pointer.Type == PointerType.Touch) { TouchPressed(e); return; }
        if (!_fitted) Fit();
        _press = _last = e.GetPosition(this);
        _moved = false;
        e.Pointer.Capture(this);
        if (Tool == GraphCanvasTool.Draw && !ReadOnly)
        {
            var world = ToWorld(_last);
            _drawing = [new GraphPoint(world.X, world.Y)];
            _pressedNode = null;
            return;
        }
        if (Tool == GraphCanvasTool.Erase && !ReadOnly)
        {
            var at = ToWorld(_last);
            var selectedStroke = _document?.Strokes.LastOrDefault(st => st.Points.Any(p =>
                Math.Sqrt(Math.Pow(p.X - at.X, 2) + Math.Pow(p.Y - at.Y, 2)) < 14 / _zoom));
            if (selectedStroke is not null && _document!.RemoveStroke(selectedStroke.Id))
            {
                Changed?.Invoke();
                InvalidateVisual();
            }
            _pressedNode = null;
            return;
        }
        _pressedNode = _document?.Nodes.Where(n => n.Kind == GraphNodeKind.Group)
            .Concat(_document.Nodes.Where(n => n.Kind != GraphNodeKind.Group))
            .Reverse().FirstOrDefault(n => NodeRect(n).Contains(_last))?.Id;
        if (MultipleSelection && _pressedNode is { } id)
        {
            if (!_selectedIds.Add(id)) _selectedIds.Remove(id);
            _selected = _selectedIds.LastOrDefault();
            SelectionChanged?.Invoke();
        }
        else if (_pressedNode != _selected || (_pressedNode is null && _selectedIds.Count > 0)) Select(_pressedNode);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Cooperative && e.Pointer.Type == PointerType.Touch) { TouchMoved(e); return; }
        if (_press is null || _document is null) return;
        var at = e.GetPosition(this);
        if (_drawing is not null)
        {
            var p = ToWorld(at);
            if (_drawing.Count == 0 ||
                Math.Sqrt(Math.Pow(p.X - _drawing[^1].X, 2) + Math.Pow(p.Y - _drawing[^1].Y, 2)) >= 3 / _zoom)
                _drawing.Add(new GraphPoint(p.X, p.Y));
            _last = at;
            InvalidateVisual();
            return;
        }
        if (Tool == GraphCanvasTool.Erase) return;
        var displacement = at - _press.Value;
        if (!_moved && Math.Sqrt(displacement.X * displacement.X + displacement.Y * displacement.Y) < 7) return;
        if (!_moved && _pressedNode is not null && !ReadOnly && !MultipleSelection)
            _document.BeginDrag();
        _moved = true;
        if (_pressedNode is { } id && !ReadOnly && !MultipleSelection)
        {
            if (_document.Node(id) is { } node)
                _document.MoveNode(id, node.X + (at.X - _last.X) / _zoom, node.Y + (at.Y - _last.Y) / _zoom, recordHistory: false);
        }
        else _origin -= (at - _last) / _zoom;
        _last = at;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Cooperative && e.Pointer.Type == PointerType.Touch) { TouchReleased(e); return; }
        // a click on a card of a graph in a note opens what it points to
        if (Cooperative && ReadOnly && !_moved && _pressedNode is { } clicked && _document?.Node(clicked) is { Target: not null } target)
        {
            e.Handled = true;
            NodeOpened?.Invoke(target);
        }
        if (_press is null) return;
        _press = null;
        e.Pointer.Capture(null);
        if (_drawing is { } points)
        {
            _drawing = null;
            if (points.Count > 1 && !ReadOnly)
            {
                _document?.AddStroke(points);
                Changed?.Invoke();
            }
            InvalidateVisual();
        }
        else if (_moved && _pressedNode is not null && !ReadOnly && !MultipleSelection)
        {
            _document?.EndDrag();
            Changed?.Invoke();
        }
        _pressedNode = null;
    }

    private static void DrawLabel(DrawingContext ctx, string value, Point at, Color color, double size, FontWeight weight)
    {
        var display = value.Replace('\n', ' ').Replace('\r', ' ');
        if (display.Length > 29) display = display[..28] + "…";
        var formatted = new FormattedText(display, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            UrbeTheme.UiTypeface(weight), size, new SolidColorBrush(color));
        ctx.DrawText(formatted, at);
    }

    public override void Render(DrawingContext ctx)
    {
        if (!_fitted) Fit();
        var surface = new Rect(Bounds.Size);
        ctx.DrawRectangle(UrbeTheme.Brush(UrbeTheme.Bg), null, surface);
        var grid = 36 * _zoom;
        if (grid >= 13 && grid <= 200)
        {
            var pen = new Pen(UrbeTheme.Brush(UrbeTheme.Line, .32), .65);
            var xStart = ((Bounds.Width / 2 - _origin.X * _zoom) % grid + grid) % grid;
            var yStart = ((Bounds.Height / 2 - _origin.Y * _zoom) % grid + grid) % grid;
            for (double x = xStart; x < Bounds.Width; x += grid) ctx.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
            for (double y = yStart; y < Bounds.Height; y += grid) ctx.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }
        if (_document is null) return;
        var drawnPen = new Pen(UrbeTheme.Brush(UrbeTheme.Text2), 2);
        foreach (var stroke in _document.Strokes)
        {
            var pen = new Pen(UrbeTheme.Brush(UrbeTheme.Text2), Math.Max(double.Epsilon, stroke.Width * _zoom));
            for (int i = 1; i < stroke.Points.Length; i++)
                ctx.DrawLine(pen, ToScreen(stroke.Points[i - 1].X, stroke.Points[i - 1].Y),
                    ToScreen(stroke.Points[i].X, stroke.Points[i].Y));
        }
        if (_drawing is { Count: > 1 })
            for (int i = 1; i < _drawing.Count; i++)
                ctx.DrawLine(drawnPen, ToScreen(_drawing[i - 1].X, _drawing[i - 1].Y),
                    ToScreen(_drawing[i].X, _drawing[i].Y));
        var byId = _document.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        foreach (var edge in _document.Edges)
        {
            if (!byId.TryGetValue(edge.SourceId, out var from) || !byId.TryGetValue(edge.TargetId, out var to)) continue;
            var p = ToScreen(from.X + from.Width / 2, from.Y + from.Height / 2);
            var q = ToScreen(to.X + to.Width / 2, to.Y + to.Height / 2);
            ctx.DrawLine(new Pen(UrbeTheme.Brush(UrbeTheme.Text3, .7), 1.6), p, q);
            if (_zoom > .45 && edge.Label.Length > 0)
                DrawLabel(ctx, edge.Label, new Point((p.X + q.X) / 2, (p.Y + q.Y) / 2 - 14),
                    Color.Parse(UrbeTheme.Text2), 11, FontWeight.Normal);
            if (edge.Directed)
            {
                var dx = q.X - p.X; var dy = q.Y - p.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length > 2)
                {
                    var ux = dx / length; var uy = dy / length;
                    var tip = new Point(q.X - ux * (to.Width * _zoom / 3), q.Y - uy * (to.Height * _zoom / 3));
                    var arrowPen = new Pen(UrbeTheme.Brush(UrbeTheme.Text2), 1.7);
                    ctx.DrawLine(arrowPen, tip, new Point(tip.X - ux * 11 - uy * 5, tip.Y - uy * 11 + ux * 5));
                    ctx.DrawLine(arrowPen, tip, new Point(tip.X - ux * 11 + uy * 5, tip.Y - uy * 11 - ux * 5));
                }
            }
        }
        foreach (var n in _document.Nodes.Where(n => n.Kind == GraphNodeKind.Group)
            .Concat(_document.Nodes.Where(n => n.Kind != GraphNodeKind.Group)))
        {
            var bounds = NodeRect(n);
            if (!surface.Inflate(40).Intersects(bounds)) continue;
            var selected = _selectedIds.Contains(n.Id);
            var stroke = selected ? UrbeTheme.Accent : UrbeTheme.Line2;
            var background = n.Kind switch
            {
                GraphNodeKind.Group => UrbeTheme.Brush(UrbeTheme.Accent, .045),
                GraphNodeKind.Folder => UrbeTheme.Brush(UrbeTheme.Accent, .13),
                GraphNodeKind.Note => UrbeTheme.Brush(UrbeTheme.Accent, .09),
                _ => UrbeTheme.Brush(UrbeTheme.Surface2)
            };
            bool missing = IsMissing(n);
            var outline = new Pen(UrbeTheme.Brush(missing && !selected ? UrbeTheme.Danger : stroke), selected ? 2 : 1, dashStyle: missing ? new DashStyle([4, 3], 0) : null);
            if (n.Kind == GraphNodeKind.Shape)
                ctx.DrawEllipse(background, outline, bounds.Center, bounds.Width / 2, bounds.Height / 2);
            else ctx.DrawRectangle(background, outline, bounds, n.Kind == GraphNodeKind.Group ? 4 : 11,
                n.Kind == GraphNodeKind.Group ? 4 : 11);
            if (_zoom < .38) continue;
            var caption = n.Kind switch
            {
                GraphNodeKind.Note => "NOTA", GraphNodeKind.File => "ARQUIVO",
                GraphNodeKind.Folder => "PASTA", GraphNodeKind.Group => "GRUPO",
                GraphNodeKind.Shape => "FORMA", _ => "TEXTO"
            };
            var px = bounds.X + 12 * _zoom;
            if (missing) caption += " · NÃO ENCONTRADA";
            DrawLabel(ctx, caption, new Point(px, bounds.Y + 11 * _zoom), Color.Parse(missing ? UrbeTheme.Danger : UrbeTheme.Text3), Math.Max(8, 10 * _zoom), FontWeight.Normal);
            DrawLabel(ctx, n.Label, new Point(px, bounds.Y + 33 * _zoom), Color.Parse(UrbeTheme.Text), Math.Max(10, 13 * _zoom), FontWeight.SemiBold);
        }
        RenderHint(ctx);
    }
}

/// <summary>A discrete top bar and interactive canvas; persistence stays with MainView and its verified vault storage.</summary>
public sealed class GraphBoardPanel : Grid
{
    public GraphCanvasView Canvas { get; } = new();
    private readonly TextBlock _title = new() { FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _edit, _link, _delete, _open;
    private readonly Button _undo, _redo, _group, _resize, _select, _draw, _erase;
    public event Action? BackRequested;
    public event Action? SaveRequested;
    public event Action? AddTextRequested;
    public event Action? AddVaultRequested;
    public event Action? EditRequested;
    public event Action? LinkRequested;
    public event Action? DeleteRequested;
    public event Action? OpenRequested;
    public event Action? UndoRequested;
    public event Action? RedoRequested;
    public event Action? ShapeRequested;
    public event Action? GroupRequested;
    public event Action? ResizeRequested;

    private static Button Action(string label) => new()
    {
        Content = label, MinHeight = 40, Padding = new Thickness(12, 6), Margin = new Thickness(0, 0, 5, 0),
        Background = UrbeTheme.Brush(UrbeTheme.Surface2), Foreground = UrbeTheme.Brush(UrbeTheme.Text2)
    };

    public GraphBoardPanel()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("Auto,*");
        var top = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4, Margin = new Thickness(12, 8, 12, 6) };
        var head = new DockPanel();
        var back = Action("← Voltar"); back.Click += (_, _) => BackRequested?.Invoke();
        var save = Action("Salvar"); save.Click += (_, _) => SaveRequested?.Invoke();
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(save, Dock.Right);
        head.Children.Add(back);
        head.Children.Add(save);
        head.Children.Add(_title);
        top.Children.Add(head);

        var strip = new StackPanel { Orientation = Orientation.Horizontal };
        var addText = Action("+ Texto"); addText.Click += (_, _) => AddTextRequested?.Invoke();
        var addVault = Action("+ Vault"); addVault.Click += (_, _) => AddVaultRequested?.Invoke();
        _edit = Action("Editar"); _edit.Click += (_, _) => EditRequested?.Invoke();
        _link = Action("Conectar"); _link.Click += (_, _) => LinkRequested?.Invoke();
        _delete = Action("Remover"); _delete.Click += (_, _) => DeleteRequested?.Invoke();
        _open = Action("Abrir"); _open.Click += (_, _) => OpenRequested?.Invoke();
        var shape = Action("+ Forma"); shape.Click += (_, _) => ShapeRequested?.Invoke();
        _select = Action("Selecionar vários"); _select.Click += (_, _) =>
        {
            Canvas.MultipleSelection = !Canvas.MultipleSelection;
            Canvas.Tool = GraphCanvasTool.Select;
            Refresh();
        };
        _draw = Action("Caneta"); _draw.Click += (_, _) =>
        {
            Canvas.Tool = Canvas.Tool == GraphCanvasTool.Draw ? GraphCanvasTool.Select : GraphCanvasTool.Draw;
            Canvas.MultipleSelection = false;
            Refresh();
        };
        _erase = Action("Borracha"); _erase.Click += (_, _) =>
        {
            Canvas.Tool = Canvas.Tool == GraphCanvasTool.Erase ? GraphCanvasTool.Select : GraphCanvasTool.Erase;
            Canvas.MultipleSelection = false;
            Refresh();
        };
        _group = Action("Agrupar"); _group.Click += (_, _) => GroupRequested?.Invoke();
        _resize = Action("Tamanho"); _resize.Click += (_, _) => ResizeRequested?.Invoke();
        _undo = Action("↶ Desfazer"); _undo.Click += (_, _) => UndoRequested?.Invoke();
        _redo = Action("↷ Refazer"); _redo.Click += (_, _) => RedoRequested?.Invoke();
        foreach (var item in new[] { addText, addVault, shape, _select, _draw, _erase, _group, _resize,
            _edit, _link, _delete, _open, _undo, _redo }) strip.Children.Add(item);
        top.Children.Add(new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        Children.Add(top);
        SetRow(Canvas, 1);
        Children.Add(Canvas);
        Canvas.SelectionChanged += UpdateButtons;
        UpdateButtons();
    }

    public void Show(GraphDocument document)
    {
        _title.Text = document.Title;
        Canvas.Show(document);
        UpdateButtons();
    }

    public void Refresh()
    {
        UpdateButtons();
        Canvas.Refresh();
    }

    private void UpdateButtons()
    {
        var selected = Canvas.SelectedNode;
        _edit.IsEnabled = _delete.IsEnabled = selected is not null;
        _open.IsEnabled = selected?.Target is not null;
        _link.IsEnabled = selected is not null && Canvas.Document?.Nodes.Count > 1;
        _group.IsEnabled = Canvas.SelectedIds.Count >= 2;
        _resize.IsEnabled = selected is not null;
        _undo.IsEnabled = Canvas.Document?.CanUndo == true;
        _redo.IsEnabled = Canvas.Document?.CanRedo == true;
        _select.Content = Canvas.MultipleSelection ? "✓ Seleção múltipla" : "Selecionar vários";
        _draw.Content = Canvas.Tool == GraphCanvasTool.Draw ? "✓ Caneta" : "Caneta";
        _erase.Content = Canvas.Tool == GraphCanvasTool.Erase ? "✓ Borracha" : "Borracha";
    }
}
