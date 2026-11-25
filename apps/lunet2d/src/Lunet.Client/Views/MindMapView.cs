using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Mind maps in the shell (P2), as an outline: on a phone a tree of topics (Workflowy, Dynalist) reads and edits
/// better than a free canvas that needs a pinch for every tap. Each topic is a row, indented by depth; the chevron
/// folds a branch; tapping the text edits it in place; + adds a child; ⋮ adds a sibling, moves out a level or removes.
/// Every change is written at once (ProjectMindMap in Notes/, verified write) with undo and redo.
/// </summary>
public sealed class MindMapView : UserControl
{
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Button _undo, _redo;
    private Guid? _editing;
    private Guid? _menu;

    public MindMapView(LunetShell shell, string path, Action close)
    {
        _shell = shell;
        _path = path;
        _close = close;
        var project = shell.Workspace.Project!;
        var file = Path.Combine(project.Directory, path);
        var map = File.Exists(file) ? ProjectMindMap.Parse(project.ReadText(path))
            : ProjectMindMap.Create(Path.GetFileName(path).Replace(ProjectMindMap.Extension, "", StringComparison.OrdinalIgnoreCase));
        if (!File.Exists(file)) project.WriteText(path, map.Serialize());
        History = new MindMapHistory(map);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);

        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        _undo = LunetUi.IconButton(LunetIcons.Undo, "Desfazer", () => { if (History.CanUndo) Change(h => h.Undo(Persist)); });
        _redo = LunetUi.IconButton(LunetIcons.Redo, "Refazer", () => { if (History.CanRedo) Change(h => h.Redo(Persist)); });
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(_redo, Dock.Right);
        DockPanel.SetDock(_undo, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(_redo);
        bar.Children.Add(_undo);
        bar.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { LunetUi.Title("Mapa mental"), _status },
        });
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = _rows, Padding = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.M, 96) } });
        Content = root;
        Refresh();
    }

    public MindMapHistory History { get; }
    public ProjectMindMap Map => History.Current;

    // ---- edits (public: tests use the same paths as taps) ----

    public Guid AddChild(Guid parent, string text = "Novo tópico")
    {
        // A child of a folded topic opens the branch, so the new topic is seen.
        var basis = Map.Topics.First(t => t.Id == parent).Folded ? Map.Fold(parent) : Map;
        var next = basis.Add(parent, text);
        var id = next.Topics[^1].Id;
        Apply(next);
        _editing = id; // a new topic opens for typing
        Refresh();
        return id;
    }

    public Guid AddSibling(Guid id) => AddChild(Map.Topics.First(t => t.Id == id).Parent ?? Map.Root);
    public void Rename(Guid id, string text) => Apply(Map.Edit(id, text, Map.Topics.First(t => t.Id == id).Reference));
    public void Fold(Guid id) => Apply(Map.Fold(id));
    public void Remove(Guid id) => Apply(Map.Remove(id));

    /// <summary>Moves a topic up one level (it becomes a sibling of its parent).</summary>
    public void Outdent(Guid id)
    {
        var topic = Map.Topics.First(t => t.Id == id);
        if (topic.Parent is not { } parent || parent == Map.Root) return;
        var grand = Map.Topics.First(t => t.Id == parent).Parent ?? Map.Root;
        Apply(Map.Move(id, grand));
    }

    private void Apply(ProjectMindMap next) => Change(h => h.Apply(next, Persist));

    private void Change(Action<MindMapHistory> change)
    {
        try
        {
            change(History);
            _status.Text = $"{Map.Topics.Length} tópicos · salvo";
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or ArgumentException)
        {
            _status.Text = "Não foi possível: " + e.Message;
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
        Refresh();
    }

    private void Persist(ProjectMindMap map) => _shell.Workspace.Project!.WriteText(_path, map.Serialize());

    public bool Close() { _close(); return true; }

    /// <summary>A new mind map in Notes/.</summary>
    public static string NewPath(LunetShell shell)
    {
        var files = shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"{ProjectNotes.Folder}/mapa-{n}{ProjectMindMap.Extension}")) n++;
        return $"{ProjectNotes.Folder}/mapa-{n}{ProjectMindMap.Extension}";
    }

    // ---- rows ----

    private void Refresh()
    {
        // Dimmed, never the theme's grey disabled fill.
        _undo.Opacity = History.CanUndo ? 1 : 0.35;
        _redo.Opacity = History.CanRedo ? 1 : 0.35;
        if (string.IsNullOrEmpty(_status.Text)) _status.Text = $"{Map.Topics.Length} tópicos";
        _rows.Children.Clear();
        var children = Map.Topics.Skip(1).ToLookup(t => t.Parent!.Value);
        foreach (var place in Map.Walk(Map.Root))
            _rows.Children.Add(Row(place, children[place.Topic.Id].Any()));
    }

    private Control Row(MindPlacement place, bool hasChildren)
    {
        var topic = place.Topic;
        var isRoot = topic.Id == Map.Root;
        var row = new DockPanel { MinHeight = LunetTokens.MinTouch, Margin = new Thickness(place.Depth * 20, 0, 0, 0) };

        var chevron = new Button
        {
            Width = 36, Height = LunetTokens.MinTouch, Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            Content = hasChildren ? LunetIcons.Create(topic.Folded ? LunetIcons.ChevronRight : LunetIcons.ChevronDown, LunetTokens.Color.TextSecondary, 18)
                : new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = LunetTokens.Brush(LunetTokens.Color.TextTertiary) },
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsHitTestVisible = hasChildren,
        };
        AutomationProperties.SetName(chevron, topic.Folded ? "Abrir ramo" : "Recolher ramo");
        chevron.Click += (_, _) => Fold(topic.Id);
        DockPanel.SetDock(chevron, Dock.Left);
        row.Children.Add(chevron);

        var add = LunetUi.IconButton(LunetIcons.Plus, "Novo tópico dentro de " + topic.Text, () => AddChild(topic.Id), LunetTokens.Color.TextSecondary);
        add.Name = "Add-" + topic.Id;
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        if (!isRoot)
        {
            var more = LunetUi.IconButton(LunetIcons.More, "Mais ações", () => { _menu = _menu == topic.Id ? null : topic.Id; Refresh(); }, LunetTokens.Color.TextSecondary);
            DockPanel.SetDock(more, Dock.Right);
            row.Children.Add(more);
        }

        if (_editing == topic.Id)
        {
            var box = new TextBox
            {
                Text = topic.Text, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body,
                VerticalContentAlignment = VerticalAlignment.Center, Name = "Edit-" + topic.Id,
            };
            AutomationProperties.SetName(box, "Texto do tópico");
            void Commit() { var text = box.Text ?? ""; _editing = null; if (text != topic.Text) Rename(topic.Id, text); else Refresh(); }
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; var text = box.Text ?? ""; _editing = null; if (text != topic.Text) Rename(topic.Id, text); if (!isRoot) AddSibling(topic.Id); else Refresh(); }
                else if (e.Key == Key.Escape) { e.Handled = true; _editing = null; Refresh(); }
            };
            box.LostFocus += (_, _) => { if (_editing == topic.Id) Commit(); };
            box.AttachedToVisualTree += (_, _) => { box.Focus(); box.SelectAll(); };
            row.Children.Add(box);
        }
        else
        {
            var text = LunetUi.Text(topic.Text, isRoot ? LunetTokens.Type.Title : LunetTokens.Type.Body, LunetTokens.Color.TextPrimary, isRoot ? FontWeight.Bold : FontWeight.Normal);
            text.VerticalAlignment = VerticalAlignment.Center;
            var label = new Button
            {
                Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0), Padding = new Thickness(LunetTokens.Space.Xs, 0), MinHeight = LunetTokens.MinTouch,
                Name = "Topic-" + topic.Id,
            };
            AutomationProperties.SetName(label, "Editar " + topic.Text);
            label.Click += (_, _) => { _editing = topic.Id; Refresh(); };
            row.Children.Add(label);
        }

        if (_menu != topic.Id) return row;
        var menu = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(place.Depth * 20 + 36, 0, 0, LunetTokens.Space.Xs) };
        void Item(string label, Action act, string color = LunetTokens.Color.TextPrimary)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, null, () => { _menu = null; act(); });
            b.MinHeight = 40;
            menu.Children.Add(b);
        }
        Item("Tópico ao lado", () => AddSibling(topic.Id));
        if (topic.Parent is { } p && p != Map.Root) Item("Subir um nível", () => Outdent(topic.Id));
        Item("Apagar (com o ramo)", () => Remove(topic.Id));
        return new StackPanel { Children = { row, menu } };
    }
}
