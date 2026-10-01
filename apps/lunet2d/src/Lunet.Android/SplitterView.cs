using Android.Content;
using Android.Graphics;
using Android.Views;

namespace Lunet.Android;

/// <summary>Divisória arrastável entre o editor e o painel inferior. Arraste para redimensionar.</summary>
internal sealed class SplitterView : View
{
    private readonly Paint _grip = new(PaintFlags.AntiAlias) { Color = Color.Argb(200, 150, 156, 170) };
    private readonly View _body;
    private bool _horizontal;

    /// <param name="body">Contêiner cujas dimensões definem a fração.</param>
    public SplitterView(Context context, View body) : base(context)
    {
        _body = body;
        SetBackgroundColor(Color.Argb(255, 34, 37, 44));
    }

    /// <summary>Verdadeiro quando a divisória é uma barra vertical (painel à direita).</summary>
    public bool Horizontal
    {
        get => _horizontal;
        set
        {
            _horizontal = value;
            Invalidate();
        }
    }

    /// <summary>Nova fração do painel enquanto arrasta, e se o gesto terminou.</summary>
    public event Action<double, bool>? Resized;

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var density = Resources!.DisplayMetrics!.Density;
        var length = 36 * density;
        var thickness = 4 * density;
        if (_horizontal)
            canvas.DrawRoundRect(Width / 2f - thickness / 2, Height / 2f - length / 2, Width / 2f + thickness / 2, Height / 2f + length / 2, thickness / 2, thickness / 2, _grip);
        else
            canvas.DrawRoundRect(Width / 2f - length / 2, Height / 2f - thickness / 2, Width / 2f + length / 2, Height / 2f + thickness / 2, thickness / 2, thickness / 2, _grip);
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return base.OnTouchEvent(e);
        switch (e.Action)
        {
            case MotionEventActions.Down:
                Parent?.RequestDisallowInterceptTouchEvent(true);
                return true;
            case MotionEventActions.Move:
            case MotionEventActions.Up:
            {
                var location = new int[2];
                _body.GetLocationOnScreen(location);
                double fraction;
                if (_horizontal) fraction = (location[0] + _body.Width - e.RawX) / Math.Max(1, _body.Width);
                else fraction = (location[1] + _body.Height - e.RawY) / Math.Max(1, _body.Height);
                Resized?.Invoke(fraction, e.Action == MotionEventActions.Up);
                return true;
            }
        }
        return base.OnTouchEvent(e);
    }
}
