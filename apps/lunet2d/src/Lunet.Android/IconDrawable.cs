using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;
using APath = Android.Graphics.Path;

namespace Lunet.Android;

/// <summary>Desenha um <see cref="VectorIcon"/> (grade 24×24) com contorno de 2 unidades. A cor segue o texto do botão.</summary>
internal sealed class IconDrawable : Drawable
{
    private static readonly Dictionary<string, (APath Path, bool Filled)[]> Paths = new(StringComparer.Ordinal);
    private readonly (APath Path, bool Filled)[] _parts;
    private readonly int _size;
    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private AColor _color;

    public IconDrawable(Context context, VectorIcon icon, float sizeDp = 22, AColor? color = null)
    {
        _parts = Build(icon);
        _size = LunetUi.Dp(context, sizeDp);
        _color = color ?? LunetUi.Text;
        _paint.StrokeCap = Paint.Cap.Round;
        _paint.StrokeJoin = Paint.Join.Round;
    }

    private static (APath, bool)[] Build(VectorIcon icon)
    {
        lock (Paths)
        {
            if (Paths.TryGetValue(icon.Name, out var cached)) return cached;
            var built = icon.Parts.Select(part =>
            {
                var path = new APath();
                foreach (var s in part.Segments)
                {
                    switch (s.Op)
                    {
                        case VectorOp.Move: path.MoveTo(s.X1, s.Y1); break;
                        case VectorOp.Line: path.LineTo(s.X1, s.Y1); break;
                        case VectorOp.Quad: path.QuadTo(s.X1, s.Y1, s.X2, s.Y2); break;
                        case VectorOp.Cubic: path.CubicTo(s.X1, s.Y1, s.X2, s.Y2, s.X3, s.Y3); break;
                        default: path.Close(); break;
                    }
                }
                return (path, part.Filled);
            }).ToArray();
            return Paths[icon.Name] = built;
        }
    }

    public AColor Color
    {
        get => _color;
        set { if (_color == value) return; _color = value; InvalidateSelf(); }
    }

    public override int IntrinsicWidth => _size;
    public override int IntrinsicHeight => _size;
    public override int Opacity => (int)Format.Translucent;
    public override void SetAlpha(int alpha) { _paint.Alpha = alpha; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) { _paint.SetColorFilter(colorFilter); InvalidateSelf(); }

    public override void Draw(Canvas canvas)
    {
        var bounds = Bounds;
        if (bounds.IsEmpty) return;
        int save = canvas.Save();
        float scale = Math.Min(bounds.Width(), bounds.Height()) / VectorIcon.Grid;
        canvas.Translate(bounds.Left + (bounds.Width() - VectorIcon.Grid * scale) / 2f, bounds.Top + (bounds.Height() - VectorIcon.Grid * scale) / 2f);
        canvas.Scale(scale, scale);
        _paint.Color = _color;
        _paint.StrokeWidth = 2f;
        foreach (var (path, filled) in _parts)
        {
            _paint.SetStyle(filled ? Paint.Style.Fill : Paint.Style.Stroke);
            canvas.DrawPath(path, _paint);
        }
        canvas.RestoreToCount(save);
    }
}

internal enum IconSlot { None, Center, Start, Top }

/// <summary>Botão que pode ter ícone vetorial (sozinho, antes do texto ou acima dele) e mantém a cor do ícone igual à do texto.</summary>
internal sealed class IconButton : Button
{
    private IconDrawable? _icon;
    private IconSlot _slot;

    public IconButton(Context context) : base(context) { }

    public void SetIcon(VectorIcon? icon, IconSlot slot, float sizeDp = 22)
    {
        _slot = icon is null ? IconSlot.None : slot;
        // Ícone sozinho precisa de nome: dica ao segurar o dedo (Android 8+) além do rótulo de acessibilidade.
        if (slot == IconSlot.Center && OperatingSystem.IsAndroidVersionAtLeast(26)) TooltipText = ContentDescription;
        _icon = icon is null ? null : new IconDrawable(Context!, icon, sizeDp, new AColor(CurrentTextColor));
        CompoundDrawablePadding = LunetUi.Dp(Context!, slot == IconSlot.Top ? 2 : 6);
        switch (_slot)
        {
            case IconSlot.Start: SetCompoundDrawablesRelativeWithIntrinsicBounds(_icon, null, null, null); break;
            case IconSlot.Top: SetCompoundDrawablesWithIntrinsicBounds(null, _icon, null, null); break;
            default: SetCompoundDrawables(null, null, null, null); break;
        }
        Invalidate();
    }

    /// <summary>Atualiza texto e ícone a partir de um rótulo como "▶ Rodar" ou "■".</summary>
    public void SetLabel(string text)
    {
        if (LunetIcons.TrySplitLabel(text, out var icon, out var label, out var stacked))
        {
            Text = label;
            SetIcon(icon, label.Length == 0 ? IconSlot.Center : stacked ? IconSlot.Top : IconSlot.Start, label.Length == 0 ? 22 : 20);
        }
        else
        {
            Text = text;
            SetIcon(null, IconSlot.None);
        }
    }

    public void SyncIconColor() { if (_icon is not null) _icon.Color = new AColor(CurrentTextColor); }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (_slot != IconSlot.Center || _icon is null) return;
        int size = _icon.IntrinsicWidth;
        int left = (Width - size) / 2, top = (Height - size) / 2;
        _icon.SetBounds(left, top, left + size, top + size);
        _icon.Draw(canvas);
    }
}
