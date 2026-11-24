using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private void AskNewMindMap()
    {
        var input = LunetUi.Input(this, "Título do mapa", "");
        new AlertDialog.Builder(this)!.SetTitle("Novo mapa mental")!.SetView(Padded(input))!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try { if (_project is { } project) OpenMindMap(ProjectNotes.CreateMindMap(project, input.Text ?? "")); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
                { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
            })!.Show();
    }

    private void OpenMindMap(string path)
    {
        if (_project is not { } project) return;
        try
        {
            var history = new MindMapHistory(ProjectMindMap.Parse(project.ReadText(path)));
            var root = OverlayScaffold("Mapa mental", out var actions);
            actions.AddView(LunetUi.Button(this, "Notas", ShowNotes));
            Guid selected = history.Current.Root;
            Guid? connectionStart = null;
            var surface = new MindMapCanvas(this);
            var detail = LunetUi.Label(this, "", LunetUi.TextBody, LunetUi.Text);
            root.AddView(detail, LunetUi.FillWidth());
            root.AddView(surface, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
            var toolbar = LunetUi.Row(this, 4);
            var scroll = new HorizontalScrollView(this);
            scroll.AddView(toolbar);
            root.AddView(scroll);
            void ActionButton(string text, Action run) => toolbar.AddView(LunetUi.Button(this, text, run));
            void Persist(ProjectMindMap map) => project.WriteText(path, map.Serialize());
            MindTopic Topic() => history.Current.Topics.First(topic => topic.Id == selected);
            void Refresh()
            {
                if (!history.Current.Topics.Any(topic => topic.Id == selected)) selected = history.Current.Root;
                var visible = history.Current.Walk(history.Current.Root);
                if (!visible.Any(item => item.Topic.Id == selected)) selected = history.Current.Root;
                surface.Show(visible, history.Current.Connections, selected);
                var reference = Topic().Reference;
                detail.Text = Topic().Text + (connectionStart.HasValue ? " · Toque no outro tópico" :
                    reference is null ? "" : project.ListFiles().Contains(reference) ? " · " + reference : " · Referência ausente");
            }
            void TryChange(Action action)
            {
                try { action(); Refresh(); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or ProjectException)
                { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
            }
            void Change(Func<ProjectMindMap, ProjectMindMap> change) => TryChange(() => history.Apply(change(history.Current), Persist));
            surface.Selected = id =>
            {
                if (connectionStart is { } from)
                {
                    connectionStart = null;
                    Change(map => map.ToggleConnection(from, id));
                }
                else { selected = id; Refresh(); }
            };
            surface.Moved = (id, parent) => Change(map => map.Move(id, parent));
            void Edit(bool create)
            {
                var input = LunetUi.Input(this, "Texto do tópico", create ? "" : Topic().Text);
                new AlertDialog.Builder(this)!.SetTitle(create ? "Novo tópico" : "Editar tópico")!.SetView(Padded(input))!
                    .SetNegativeButton("Cancelar", (_, _) => { })!.SetPositiveButton("Salvar", (_, _) =>
                        Change(map => create ? map.Add(selected, input.Text ?? "") : map.Edit(selected, input.Text ?? "", Topic().Reference)))!.Show();
            }
            ActionButton("Tópicos", () =>
            {
                var visible = history.Current.Walk(history.Current.Root);
                new AlertDialog.Builder(this)!.SetTitle("Selecionar tópico")!
                    .SetItems(visible.Select(item => item.Topic.Text).ToArray(), (_, args) =>
                    { selected = visible[args.Which].Topic.Id; Refresh(); })!
                    .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
            });
            ActionButton("+ Tópico", () => Edit(true));
            ActionButton("Editar", () => Edit(false));
            ActionButton("Recolher/expandir", () => Change(map => map.Fold(selected)));
            ActionButton("Ligar", () => { connectionStart = connectionStart.HasValue ? null : selected; Refresh(); });
            ActionButton("Mover", () =>
            {
                var topics = history.Current.Topics.Where(t => t.Id != selected).ToArray();
                if (topics.Length == 0) return;
                new AlertDialog.Builder(this)!.SetTitle("Mover para baixo de")!
                    .SetItems(topics.Select(t => t.Text).ToArray(), (_, e) => Change(map => map.Move(selected, topics[e.Which].Id)))!
                    .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
            });
            ActionButton("Referência", () =>
            {
                var notes = ProjectNotes.List(project).Where(note => note.Kind != NoteKind.MindMap).ToArray();
                var options = new[] { "Remover referência" }.Concat(notes.Select(note => note.Title)).ToArray();
                new AlertDialog.Builder(this)!.SetTitle("Vincular nota ou material")!
                    .SetItems(options, (_, e) => Change(map => map.Edit(selected, Topic().Text, e.Which == 0 ? null : notes[e.Which - 1].Path)))!
                    .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
            });
            ActionButton("Ler referência", () =>
            {
                var note = ProjectNotes.List(project).FirstOrDefault(n => n.Path == Topic().Reference);
                if (note is null) { Toast.MakeText(this, "Escolha ou recupere a referência no botão Referência.", ToastLength.Long)?.Show(); return; }
                if (note.Kind == NoteKind.Text)
                    ShowMindMapText(note.Title, project.ReadText(note.Path));
                else if (note.Kind is NoteKind.Image or NoteKind.Sketch)
                {
                    var view = new ImageView(this);
                    view.SetImageURI(global::Android.Net.Uri.FromFile(new Java.IO.File(System.IO.Path.Combine(project.Directory, note.Path))));
                    new AlertDialog.Builder(this)!.SetTitle(note.Title)!.SetView(view)!.SetPositiveButton("Voltar ao mapa", (_, _) => { })!.Show();
                }
                else OpenOtherNote(note);
            });
            ActionButton("Desfazer", () => TryChange(() => history.Undo(Persist)));
            ActionButton("Refazer", () => TryChange(() => history.Redo(Persist)));
            ActionButton("Apresentar", () => PresentMindMap(history.Current, selected));
            ActionButton("Centralizar", () => { surface.ResetViewport(); Refresh(); });
            ActionButton("Remover", () =>
            {
                if (selected == history.Current.Root) return;
                new AlertDialog.Builder(this)!.SetTitle("Remover tópico e seus ramos?")!
                    .SetNegativeButton("Cancelar", (_, _) => { })!
                    .SetPositiveButton("Remover", (_, _) => Change(map => map.Remove(selected)))!.Show();
            });
            Refresh();
            ShowOverlay(root);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { Toast.MakeText(this, "Não foi possível abrir o mapa: " + ex.Message, ToastLength.Long)?.Show(); }
    }

    private void ShowMindMapText(string title, string content)
    {
        var scroll = new ScrollView(this);
        var label = LunetUi.Label(this, "", LunetUi.TextBody, LunetUi.Text);
        label.TextFormatted = RenderMarkdown(content);
        scroll.AddView(Padded(label));
        new AlertDialog.Builder(this)!.SetTitle(title)!.SetView(scroll)!.SetPositiveButton("Voltar ao mapa", (_, _) => { })!.Show();
    }

    private void PresentMindMap(ProjectMindMap map, Guid start)
    {
        var branches = map.Topics.Where(topic => topic.Parent == start).Select(topic => topic.Id).ToArray();
        if (branches.Length == 0) branches = [start];
        int index = 0;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var layout = LunetUi.Column(this, 8);
        var title = LunetUi.Label(this, "", LunetUi.TextTitle, LunetUi.Text);
        var view = new MindMapCanvas(this);
        void ShowBranch()
        {
            var branch = branches[index];
            title.Text = $"{index + 1}/{branches.Length} · {map.Topics.First(topic => topic.Id == branch).Text}";
            view.ResetViewport();
            view.Show(map.Walk(branch, fold: false), map.Connections, branch);
        }
        layout.AddView(title);
        layout.AddView(view, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        var bar = LunetUi.Row(this, 4);
        bar.AddView(LunetUi.Button(this, "Anterior", () => { if (index > 0) { index--; ShowBranch(); } }));
        bar.AddView(LunetUi.Button(this, "Próximo", () => { if (index + 1 < branches.Length) { index++; ShowBranch(); } }));
        bar.AddView(LunetUi.Button(this, "Fechar", dialog.Dismiss));
        layout.AddView(bar);
        ShowBranch();
        dialog.SetContentView(layout);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }
}

internal sealed class MindMapCanvas : View
{
    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly ScaleGestureDetector _detector;
    private readonly float _density;
    private IReadOnlyList<(MindTopic Topic, RectF Box)> _nodes = [];
    private IReadOnlyList<MindConnection> _links = [];
    private Guid _selection;
    private Guid? _down;
    private float _x, _y, _startX, _startY, _scale = 1;
    private bool _pinched;

    public Action<Guid>? Selected { get; set; }
    public Action<Guid, Guid>? Moved { get; set; }

    public MindMapCanvas(global::Android.Content.Context context) : base(context)
    {
        _density = Resources!.DisplayMetrics!.Density;
        _detector = new ScaleGestureDetector(context, new ZoomListener(this));
        ContentDescription = "Mapa mental, arraste o fundo para navegar e use dois dedos para ampliar";
        Clickable = true;
    }

    public void Show(IReadOnlyList<MindPlacement> places, IReadOnlyList<MindConnection> links, Guid selected)
    {
        _selection = selected;
        _links = links;
        _nodes = places.Select(p =>
        {
            float x = (p.Depth * 180 + 12) * _density;
            float y = (p.Row * 60 + 12) * _density;
            return (p.Topic, new RectF(x, y, x + 148 * _density, y + 46 * _density));
        }).ToArray();
        Invalidate();
    }

    public void ResetViewport() { _x = _y = 0; _scale = 1; }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.DrawColor(LunetUi.Background);
        canvas.Save();
        canvas.Translate(_x, _y);
        canvas.Scale(_scale, _scale);
        var bounds = _nodes.ToDictionary(node => node.Topic.Id, node => node.Box);
        _paint.StrokeWidth = 2 * _density;
        void Connect(Guid from, Guid to, AColor color)
        {
            if (!bounds.TryGetValue(from, out var a) || !bounds.TryGetValue(to, out var b)) return;
            _paint.Color = color;
            canvas.DrawLine(a.Right, a.CenterY(), b.Left, b.CenterY(), _paint);
        }
        foreach (var node in _nodes)
            if (node.Topic.Parent is { } parent) Connect(parent, node.Topic.Id, LunetUi.TextMuted);
        foreach (var link in _links) Connect(link.From, link.To, LunetUi.AccentText);
        foreach (var (topic, box) in _nodes)
        {
            _paint.Color = topic.Id == _selection ? LunetUi.Accent : LunetUi.SurfaceRaised;
            canvas.DrawRoundRect(box, 8 * _density, 8 * _density, _paint);
            _paint.Color = topic.Id == _selection ? AColor.White : LunetUi.Text;
            _paint.TextSize = 14 * _density;
            var text = (topic.Folded ? "+ " : "") + topic.Text.Replace('\n', ' ');
            int count = _paint.BreakText(text, true, box.Width() - 19 * _density, null);
            canvas.DrawText(count < text.Length ? text[..Math.Max(0, count - 1)] + "…" : text,
                box.Left + 8 * _density, box.CenterY() + 5 * _density, _paint);
        }
        canvas.Restore();
    }

    private Guid? Hit(float x, float y)
    {
        foreach (var node in _nodes)
            if (node.Box.Contains((x - _x) / _scale, (y - _y) / _scale)) return node.Topic.Id;
        return null;
    }

    public override bool PerformClick() => base.PerformClick();

    public override bool OnTouchEvent(MotionEvent? motion)
    {
        if (motion is null) return false;
        var x = motion.GetX();
        var y = motion.GetY();
        _detector.OnTouchEvent(motion);
        switch (motion.ActionMasked)
        {
            case MotionEventActions.Down:
                _pinched = false;
                _startX = x;
                _startY = y;
                _down = Hit(x, y);
                return true;
            case MotionEventActions.PointerDown:
                _pinched = true;
                return true;
            case MotionEventActions.Move when !_pinched && _down is null:
                _x += x - _startX;
                _y += y - _startY;
                _startX = x;
                _startY = y;
                Invalidate();
                return true;
            case MotionEventActions.Up when !_pinched && _down is { } old:
                if (Math.Abs(_startX - x) + Math.Abs(_startY - y) < 12 * _density)
                { PerformClick(); Selected?.Invoke(old); }
                else if (Hit(x, y) is { } target && old != target) Moved?.Invoke(old, target);
                return true;
            default: return true;
        }
    }

    private sealed class ZoomListener(MindMapCanvas owner) : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        public override bool OnScale(ScaleGestureDetector detector)
        {
            float previous = owner._scale;
            owner._scale = Math.Clamp(previous * detector.ScaleFactor, 0.25f, 3f);
            float ratio = owner._scale / previous;
            owner._x = detector.FocusX - (detector.FocusX - owner._x) * ratio;
            owner._y = detector.FocusY - (detector.FocusY - owner._y) * ratio;
            owner.Invalidate();
            return true;
        }
    }
}
