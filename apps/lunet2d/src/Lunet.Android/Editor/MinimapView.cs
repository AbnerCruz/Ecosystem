using Android.Content;
using Android.Graphics;
using Android.Views;

namespace Lunet.Android.Editor;

/// <summary>Minimapa do código: uma faixa fina à direita com o "formato" do arquivo e a janela visível. Toque ou arraste para rolar.</summary>
internal sealed class MinimapView : View
{
    private readonly CodeEditText _editor;
    private readonly Paint _bar = new();
    private readonly Paint _window = new() { Color = Color.Argb(60, 200, 210, 230) };
    private readonly Paint _background = new() { Color = Color.Argb(150, 18, 20, 24) };
    private int[] _indent = [];
    private int[] _length = [];
    private bool[] _comment = [];

    public MinimapView(Context context, CodeEditText editor) : base(context)
    {
        _editor = editor;
        editor.ScrollPositionChanged += Invalidate;
    }

    /// <summary>Recalcula o desenho das linhas a partir do texto atual.</summary>
    public void Refresh(string text)
    {
        var lines = text.Split('\n');
        _indent = new int[lines.Length];
        _length = new int[lines.Length];
        _comment = new bool[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();
            _indent[i] = line.Length - trimmed.Length;
            _length[i] = trimmed.Length;
            _comment[i] = trimmed.StartsWith("//", StringComparison.Ordinal);
        }
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        canvas.DrawRect(0, 0, Width, Height, _background);
        var total = _length.Length;
        if (total == 0 || Height == 0) return;

        var rowHeight = Math.Max(1f, Math.Min(3f * Resources!.DisplayMetrics!.Density, (float)Height / total));
        var linesPerRow = Math.Max(1, (int)Math.Ceiling(total / (Height / rowHeight)));
        var scale = (Width - 4f) / 100f; // 100 colunas ocupam a largura toda
        for (var row = 0; row * linesPerRow < total; row++)
        {
            var line = row * linesPerRow;
            if (_length[line] == 0) continue;
            _bar.Color = _comment[line] ? Color.Argb(200, 106, 153, 85) : Color.Argb(170, 190, 196, 208);
            var left = 2f + Math.Min(60, _indent[line]) * scale;
            var right = Math.Min(Width - 2f, left + Math.Min(100, _length[line]) * scale);
            var top = row * rowHeight;
            canvas.DrawRect(left, top, right, top + Math.Max(1f, rowHeight - 0.5f), _bar);
        }

        var first = _editor.FirstVisibleLine;
        var visible = _editor.VisibleLineCount;
        var windowTop = (float)first / total * Height;
        var windowHeight = Math.Max(6f, (float)visible / total * Height);
        canvas.DrawRect(0, windowTop, Width, windowTop + windowHeight, _window);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null || _length.Length == 0) return base.OnTouchEvent(e);
        switch (e.Action)
        {
            case MotionEventActions.Down:
            case MotionEventActions.Move:
                Parent?.RequestDisallowInterceptTouchEvent(true);
                var line = (int)(Math.Clamp(e.GetY() / Math.Max(1, Height), 0f, 1f) * _length.Length) - _editor.VisibleLineCount / 2;
                _editor.ScrollToLine(line);
                return true;
        }
        return true;
    }
}
