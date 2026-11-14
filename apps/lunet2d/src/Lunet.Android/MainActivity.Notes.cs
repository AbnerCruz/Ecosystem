using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// LUNET-485: notes, scripts and sketches inside the project, one tap from the workbench, so the person never has to
/// leave Lunet to read their own plans. Files are ordinary Markdown/PNG in Notes/ (exported, versioned, read by the AI).
/// </summary>
public sealed partial class MainActivity
{
    private const string NotesFolder = ProjectNotes.Folder;
    private View? _overlay;
    private string? _openNote;
    private Action? _overlayClose;

    private bool OverlayOpen => _overlay is not null;

    private void CloseOverlay()
    {
        _overlayClose?.Invoke();
        _overlayClose = null;
        if (_overlay?.Parent is ViewGroup parent) parent.RemoveView(_overlay);
        _overlay = null;
        _openNote = null;
        RefreshAiHeader();
    }

    private void ShowOverlay(View view, Action? onClose = null)
    {
        if (_workspaceFrame is null) return;
        if (_overlay is not null) CloseOverlay();
        _overlay = view;
        _overlayClose = onClose;
        view.Clickable = true;
        // Under the AI panel so the AI stays usable over notes.
        var index = _aiPanel is not null && _aiPanel.Parent == _workspaceFrame ? _workspaceFrame.IndexOfChild(_aiPanel) : -1;
        var layout = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        if (index >= 0) _workspaceFrame.AddView(view, index, layout);
        else _workspaceFrame.AddView(view, layout);
    }

    private LinearLayout OverlayScaffold(string title, out LinearLayout actions)
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(LunetUi.Background);
        var bar = LunetUi.Row(this, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(this, "‹", "Voltar ao código", CloseOverlay), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var label = LunetUi.Label(this, title, LunetUi.TextTitle, LunetUi.Text, bold: true);
        label.SetSingleLine(true);
        label.Ellipsize = TextUtils.TruncateAt.End;
        bar.AddView(label, LunetUi.Weight());
        actions = LunetUi.Row(this, 6);
        bar.AddView(actions);
        root.AddView(bar);
        root.AddView(LunetUi.Divider(this));
        return root;
    }

    // ---------- List ----------

    private void ShowNotes()
    {
        if (_project is null) return;
        SaveCurrent();
        var root = OverlayScaffold("Notas e esboços", out var actions);
        actions.AddView(LunetUi.Button(this, "＋ Nota", AskNewNote, ButtonStyle.Primary), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        actions.AddView(LunetUi.Button(this, "✎ Esboço", () => OpenSketch(null)), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        actions.AddView(LunetUi.Icon(this, "⇩", "Importar imagens ou arquivos para Notes/", () => PickFileForImport(NotesImportRequestCode)), new LinearLayout.LayoutParams(Dp(44), Dp(44)));

        var list = LunetUi.Column(this, 8);
        list.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        var notes = ProjectNotes.List(_project);
        if (notes.Count == 0)
        {
            var empty = LunetUi.Label(this,
                "Guarde aqui ideias, roteiros, listas de tarefas, esboços e referências importadas (⇩).\n\n" +
                "As notas são arquivos Markdown em Notes/ do projeto: vão junto no ZIP e no Git, e a IA consegue lê-las.",
                LunetUi.TextBody, LunetUi.TextMuted);
            empty.SetPadding(Dp(8), Dp(24), Dp(8), Dp(24));
            list.AddView(empty);
        }
        foreach (var note in notes)
        {
            var card = LunetUi.Row(this, 12);
            card.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
            card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.Clickable = true;
            var captured = note;
            card.Click += (_, _) =>
            {
                switch (captured.Kind)
                {
                    case NoteKind.Sketch: OpenSketch(captured.Path); break;
                    case NoteKind.Image: OpenImageNote(captured.Path); break;
                    case NoteKind.File: OpenOtherNote(captured); break;
                    default: OpenNote(captured.Path); break;
                }
            };
            card.LongClick += (_, e) => { ConfirmDeleteNote(captured); e.Handled = true; };
            card.AddView(LunetUi.Label(this, note.Kind switch { NoteKind.Sketch => "✎", NoteKind.Image => "▦", NoteKind.File => "📎", _ => "¶" }, 22, LunetUi.AccentText));
            var text = LunetUi.Column(this, 2);
            text.AddView(LunetUi.Label(this, note.Title, LunetUi.TextBody, LunetUi.Text, bold: true));
            text.AddView(LunetUi.Label(this, (note.Kind switch { NoteKind.Sketch => "Esboço · ", NoteKind.Image => "Imagem · ", NoteKind.File => "Arquivo · ", _ => "Nota · " }) +
                note.ModifiedUtc.ToLocalTime().ToString("dd/MM HH:mm"), LunetUi.TextCaption, LunetUi.TextMuted));
            card.AddView(text, LunetUi.Weight());
            list.AddView(card, LunetUi.FillWidth());
        }
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        ShowOverlay(root);
    }

    private void AskNewNote()
    {
        var input = LunetUi.Input(this, "Título da nota");
        var box = new FrameLayout(this);
        box.SetPadding(Dp(20), Dp(8), Dp(20), 0);
        box.AddView(input);
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Nova nota")!
            .SetView(box)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                if (_project is null) return;
                try { OpenNote(ProjectNotes.CreateNote(_project, input.Text ?? "")); }
                catch (Exception ex) { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
            })!.Show();
    }

    private void ConfirmDeleteNote(ProjectNote note)
    {
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Apagar “" + note.Title + "”?")!
            .SetMessage("O arquivo " + note.Path + " será removido do projeto.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Apagar", (_, _) =>
            {
                try { _project?.DeleteFile(note.Path); ShowNotes(); }
                catch (Exception ex) { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
            })!.Show();
    }

    // ---------- Note editor ----------

    private void OpenNote(string path)
    {
        if (_project is null) return;
        SaveCurrent();
        string content;
        try { content = _project.ReadText(path); }
        catch (Exception ex) { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); return; }
        var project = _project;
        var root = OverlayScaffold(ProjectNotes.TitleOf(path, content), out var actions);
        var editor = new EditText(this) { Text = content, TextSize = 16, Gravity = GravityFlags.Top | GravityFlags.Start };
        editor.SetTextColor(LunetUi.Text);
        editor.SetHintTextColor(LunetUi.TextFaint);
        editor.Hint = "Escreva em Markdown: # título, - lista, **negrito**";
        editor.Background = null;
        editor.SetPadding(Dp(18), Dp(14), Dp(18), Dp(14));
        editor.SetLineSpacing(0, 1.3f);
        editor.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences;
        var preview = new TextView(this) { TextSize = 16 };
        preview.SetTextColor(LunetUi.Text);
        preview.SetPadding(Dp(18), Dp(14), Dp(18), Dp(14));
        preview.SetLineSpacing(0, 1.3f);
        preview.SetTextIsSelectable(true);
        var previewScroll = new ScrollView(this) { Visibility = ViewStates.Gone };
        previewScroll.AddView(preview);
        var status = LunetUi.Label(this, "Salvo", LunetUi.TextCaption, LunetUi.TextFaint);
        var handler = new Handler(Looper.MainLooper!);
        string saved = content;
        void Save()
        {
            var text = editor.Text ?? "";
            if (text == saved) return;
            try { project.WriteText(path, text); saved = text; status.Text = "Salvo"; }
            catch (Exception ex) { status.Text = "Não salvo: " + ex.Message; }
        }
        var pending = new Java.Lang.Runnable(Save);
        editor.TextChanged += (_, _) =>
        {
            status.Text = "Editando…";
            handler.RemoveCallbacks(pending);
            handler.PostDelayed(pending, 700);
        };
        Button? toggle = null;
        toggle = LunetUi.Button(this, "Ler", () =>
        {
            bool reading = previewScroll.Visibility != ViewStates.Visible;
            if (reading) { Save(); preview.SetText(RenderMarkdown(editor.Text ?? ""), TextView.BufferType.Spannable); }
            previewScroll.Visibility = reading ? ViewStates.Visible : ViewStates.Gone;
            editor.Visibility = reading ? ViewStates.Gone : ViewStates.Visible;
            toggle!.Text = reading ? "Editar" : "Ler";
        });
        actions.AddView(status);
        actions.AddView(toggle, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        actions.AddView(LunetUi.Button(this, "✦ IA", () =>
        {
            Save();
            OpenAiPanel("Sobre a nota " + path + ": ");
        }, ButtonStyle.Selected), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        root.AddView(editor, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        root.AddView(previewScroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        ShowOverlay(root, () => { handler.RemoveCallbacks(pending); Save(); });
        _openNote = path;
        RefreshAiHeader();
    }

    /// <summary>Readable Markdown: headings, bullets, checkboxes, bold, inline code and fenced code.</summary>
    private ISpannable RenderMarkdown(string text)
    {
        var builder = new SpannableStringBuilder();
        bool code = false;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw;
            int start = builder.Length();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { code = !code; continue; }
            if (code)
            {
                builder.Append(line + "\n");
                builder.SetSpan(new TypefaceSpan("monospace"), start, builder.Length(), SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new BackgroundColorSpan(LunetUi.Surface), start, builder.Length(), SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), start, builder.Length(), SpanTypes.ExclusiveExclusive);
                continue;
            }
            int level = 0;
            while (level < line.Length && level < 3 && line[level] == '#') level++;
            if (level > 0 && line.Length > level && line[level] == ' ')
            {
                builder.Append(line[(level + 1)..] + "\n");
                builder.SetSpan(new RelativeSizeSpan(level == 1 ? 1.5f : level == 2 ? 1.25f : 1.1f), start, builder.Length(), SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, builder.Length(), SpanTypes.ExclusiveExclusive);
                continue;
            }
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal)) line = "☐ " + trimmed[6..];
            else if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase)) line = "☑ " + trimmed[6..];
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal)) line = "•  " + trimmed[2..];
            AppendInline(builder, line);
            builder.Append("\n");
        }
        return builder;
    }

    // ---------- Sketch ----------

    private void OpenSketch(string? path)
    {
        if (_project is null) return;
        var project = _project;
        path ??= ProjectNotes.NewSketchPath(project, "esboco");
        var target = path;
        var root = OverlayScaffold(System.IO.Path.GetFileNameWithoutExtension(path), out var actions);
        Bitmap? initial = null;
        var full = System.IO.Path.Combine(project.Directory, path);
        if (File.Exists(full)) initial = BitmapFactory.DecodeFile(full);
        var canvas = new SketchView(this, initial);
        void Save()
        {
            if (!canvas.Dirty) return;
            try
            {
                using var stream = new MemoryStream();
                canvas.Export().Compress(Bitmap.CompressFormat.Png!, 100, stream);
                stream.Position = 0;
                project.Import(NotesFolder, System.IO.Path.GetFileName(target), stream);
                canvas.Dirty = false;
            }
            catch (Exception ex) { Toast.MakeText(this, "Esboço não salvo: " + ex.Message, ToastLength.Long)?.Show(); }
        }
        actions.AddView(LunetUi.Icon(this, "↶", "Desfazer traço", canvas.Undo), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        actions.AddView(LunetUi.Button(this, "Salvar", () => { Save(); Toast.MakeText(this, "Salvo em " + target, ToastLength.Short)?.Show(); }, ButtonStyle.Primary),
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));

        var tools = LunetUi.Row(this, 6);
        tools.SetPadding(Dp(10), Dp(6), Dp(10), Dp(8));
        var swatches = new List<(TextView View, AColor Color)>();
        void Paint(AColor color, bool eraser)
        {
            canvas.Color = color; canvas.Eraser = eraser;
            foreach (var (view, c) in swatches)
                view.Background = LunetUi.Shape(this, c, 99, !eraser && c == color ? LunetUi.Accent : LunetUi.Border);
        }
        foreach (var color in new[] { AColor.Rgb(230, 236, 245), AColor.Rgb(124, 108, 255), AColor.Rgb(56, 189, 248),
                     AColor.Rgb(63, 185, 80), AColor.Rgb(250, 204, 21), AColor.Rgb(248, 81, 73) })
        {
            var swatch = new TextView(this) { Clickable = true, ContentDescription = "Cor" };
            var c = color;
            swatch.Click += (_, _) => Paint(c, false);
            swatches.Add((swatch, c));
            tools.AddView(swatch, new LinearLayout.LayoutParams(Dp(34), Dp(34)));
        }
        tools.AddView(LunetUi.Chip(this, "Borracha", () => Paint(canvas.Color, true)));
        tools.AddView(LunetUi.Chip(this, "Fino", () => canvas.StrokeDp = 3));
        tools.AddView(LunetUi.Chip(this, "Grosso", () => canvas.StrokeDp = 9));
        Paint(swatches[0].Color, false);
        var toolScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        toolScroll.AddView(tools);
        root.AddView(canvas, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        root.AddView(toolScroll);
        ShowOverlay(root, Save);
    }

    /// <summary>Freehand sketch surface: smooth strokes, eraser and stroke undo.</summary>
    private sealed class SketchView : View
    {
        private readonly List<(global::Android.Graphics.Path Path, Paint Paint)> _strokes = [];
        private readonly Bitmap? _base;
        private global::Android.Graphics.Path? _current;
        private float _lastX, _lastY;
        public AColor Color { get; set; } = AColor.White;
        public bool Eraser { get; set; }
        public float StrokeDp { get; set; } = 4;
        public bool Dirty { get; set; }

        public SketchView(Context context, Bitmap? initial) : base(context)
        {
            _base = initial;
            SetBackgroundColor(AColor.Rgb(17, 21, 29));
        }

        private Paint NewPaint()
        {
            var density = Resources!.DisplayMetrics!.Density;
            var paint = new Paint(PaintFlags.AntiAlias) { StrokeWidth = (Eraser ? StrokeDp * 4 : StrokeDp) * density };
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeCap = Paint.Cap.Round;
            paint.StrokeJoin = Paint.Join.Round;
            paint.Color = Eraser ? AColor.Rgb(17, 21, 29) : Color;
            return paint;
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null) return false;
            float x = e.GetX(), y = e.GetY();
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    Parent?.RequestDisallowInterceptTouchEvent(true);
                    _current = new global::Android.Graphics.Path();
                    _current.MoveTo(x, y);
                    _strokes.Add((_current, NewPaint()));
                    _lastX = x; _lastY = y;
                    Dirty = true;
                    break;
                case MotionEventActions.Move when _current is not null:
                    for (int i = 0; i < e.HistorySize; i++) Smooth(e.GetHistoricalX(i), e.GetHistoricalY(i));
                    Smooth(x, y);
                    break;
                case MotionEventActions.Up or MotionEventActions.Cancel:
                    _current?.LineTo(x, y);
                    _current = null;
                    break;
            }
            Invalidate();
            return true;
        }

        private void Smooth(float x, float y)
        {
            _current!.QuadTo(_lastX, _lastY, (x + _lastX) / 2, (y + _lastY) / 2);
            _lastX = x; _lastY = y;
        }

        public void Undo()
        {
            if (_strokes.Count == 0) return;
            _strokes.RemoveAt(_strokes.Count - 1);
            Dirty = true;
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            if (_base is not null) canvas.DrawBitmap(_base, 0, 0, null);
            foreach (var (path, paint) in _strokes) canvas.DrawPath(path, paint);
        }

        public Bitmap Export()
        {
            var bitmap = Bitmap.CreateBitmap(Math.Max(1, Width), Math.Max(1, Height), Bitmap.Config.Argb8888!)!;
            var canvas = new Canvas(bitmap);
            Draw(canvas);
            return bitmap;
        }
    }
}
